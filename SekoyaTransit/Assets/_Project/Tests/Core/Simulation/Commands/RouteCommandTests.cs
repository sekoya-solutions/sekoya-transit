#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.Tests.World;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Simulation.Commands
{
    [TestFixture]
    public sealed class RouteCommandTests
    {
        private static readonly string[] _forestOutputCargoIds = { "wood" };

        private static readonly bool[] _waitAtThirdStop = { false, false, true, false };

        [Test]
        public void CreateRoute_ValidName_AddsTrimmedEmptyRoute()
        {
            Fixture fixture = Fixture.Create();

            RouteTestSupport.Accept(fixture.Processor, new CreateRouteCommand("  Wheat to Rome "));

            Assert.That(fixture.World.Routes.Count, Is.EqualTo(1));
            Route route = fixture.World.Routes[0];
            Assert.That(route.Id, Is.EqualTo(GeneratedWorld.FirstRouteId));
            Assert.That(route.Name, Is.EqualTo("Wheat to Rome"));
            Assert.That(route.Mode, Is.EqualTo(RouteMode.None));
            Assert.That(route.IsValid, Is.False);
            Assert.That(route.HasLastMonth, Is.False);
            Assert.That(fixture.World.NextRouteId, Is.EqualTo(GeneratedWorld.FirstRouteId + 1));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("123456789012345678901234567890123")]
        [TestCase("WHEAT TO ROME")]
        public void CreateRoute_InvalidOrTakenName_IsRejected(string name)
        {
            Fixture fixture = Fixture.Create();
            RouteTestSupport.Accept(fixture.Processor, new CreateRouteCommand("Wheat to Rome"));

            CommandProcessingResult result = fixture.Processor.Process(new Tick(0), new CreateRouteCommand(name));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(fixture.World.Routes.Count, Is.EqualTo(1));
        }

        [Test]
        public void RenameRoute_OwnNameInOtherCase_IsAcceptedButOtherRouteNameIsRejected()
        {
            Fixture fixture = Fixture.Create();
            RouteTestSupport.Accept(fixture.Processor, new CreateRouteCommand("Wheat"));
            RouteTestSupport.Accept(fixture.Processor, new CreateRouteCommand("Iron"));
            int wheatId = fixture.World.Routes[0].Id;

            RouteTestSupport.Accept(fixture.Processor, new RenameRouteCommand(wheatId, "WHEAT"));
            CommandProcessingResult taken = fixture.Processor.Process(new Tick(0), new RenameRouteCommand(wheatId, "iron"));

            Assert.That(fixture.World.Routes[0].Name, Is.EqualTo("WHEAT"));
            Assert.That(taken.IsAccepted, Is.False);
        }

        [Test]
        public void AddRouteStop_FirstStop_FixesModeAndOtherModeIsRejected()
        {
            Fixture fixture = Fixture.Create();
            int routeId = fixture.CreateRoute();

            RouteTestSupport.Accept(fixture.Processor, new AddRouteStopCommand(routeId, RouteMode.Road, fixture.RoadStations[0]));
            CommandProcessingResult mixed = fixture.Processor.Process(
                new Tick(0),
                new AddRouteStopCommand(routeId, RouteMode.Rail, fixture.RailStations[0]));

            Assert.That(fixture.Route(routeId).Mode, Is.EqualTo(RouteMode.Road));
            Assert.That(mixed.IsAccepted, Is.False);
            Assert.That(
                RouteRules.CheckAppendStop(fixture.World, routeId, RouteMode.Rail, fixture.RailStations[0]),
                Is.EqualTo(RouteEditRefusal.ModeMismatch));
        }

        [Test]
        public void AddRouteStop_SameStationTwiceInARow_IsRejectedButLaterRepeatIsAccepted()
        {
            Fixture fixture = Fixture.Create();
            int routeId = fixture.CreateRoute();
            int a = fixture.RoadStations[0];
            int b = fixture.RoadStations[1];
            RouteTestSupport.Accept(fixture.Processor, new AddRouteStopCommand(routeId, RouteMode.Road, a));

            CommandProcessingResult repeated = fixture.Processor.Process(new Tick(0), new AddRouteStopCommand(routeId, RouteMode.Road, a));
            RouteTestSupport.Accept(fixture.Processor, new AddRouteStopCommand(routeId, RouteMode.Road, b));
            RouteTestSupport.Accept(fixture.Processor, new AddRouteStopCommand(routeId, RouteMode.Road, a));

            Assert.That(repeated.IsAccepted, Is.False);
            Assert.That(fixture.StationIds(routeId), Is.EqualTo(new[] { a, b, a }));
        }

        [Test]
        public void AddRouteStop_MissingStation_IsRejected()
        {
            Fixture fixture = Fixture.Create();
            int routeId = fixture.CreateRoute();

            CommandProcessingResult result = fixture.Processor.Process(new Tick(0), new AddRouteStopCommand(routeId, RouteMode.Road, 999));

            Assert.That(result.IsAccepted, Is.False);
        }

        [Test]
        public void MoveRouteStop_Down_SwapsStopsAndUpdatesVehicleOrders()
        {
            Fixture fixture = Fixture.Create();
            int a = fixture.RoadStations[0];
            int b = fixture.RoadStations[1];
            int c = fixture.RoadStations[2];
            int vehicleId = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, a, b, c);
            int routeId = fixture.RoadVehicle(vehicleId).RouteId;

            RouteTestSupport.Accept(fixture.Processor, new MoveRouteStopCommand(routeId, 1, up: false));

            Assert.That(fixture.StationIds(routeId), Is.EqualTo(new[] { a, c, b }));
            Assert.That(fixture.RoadVehicle(vehicleId).OrderStationIds, Is.EqualTo(new[] { a, c, b }));
        }

        [Test]
        public void MoveRouteStop_CreatingRepeatedNeighbours_IsRejected()
        {
            Fixture fixture = Fixture.Create();
            int a = fixture.RoadStations[0];
            int b = fixture.RoadStations[1];
            int routeId = RouteTestSupport.CreateRoute(fixture.Processor, fixture.World, RouteMode.Road, new[] { a, b, a, b });

            CommandProcessingResult result = fixture.Processor.Process(new Tick(0), new MoveRouteStopCommand(routeId, 1, up: false));
            CommandProcessingResult outOfRange = fixture.Processor.Process(new Tick(0), new MoveRouteStopCommand(routeId, 0, up: true));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(outOfRange.IsAccepted, Is.False);
            Assert.That(fixture.StationIds(routeId), Is.EqualTo(new[] { a, b, a, b }));
        }

        [Test]
        public void SetRouteStopWaitForFull_UpdatesStopAndVehicleOrders()
        {
            Fixture fixture = Fixture.Create();
            int vehicleId = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, fixture.RoadStations[0], fixture.RoadStations[1]);
            int routeId = fixture.RoadVehicle(vehicleId).RouteId;

            RouteTestSupport.Accept(fixture.Processor, new SetRouteStopWaitForFullCommand(routeId, 0, true));

            Assert.That(fixture.Route(routeId).Stops[0].WaitForFull, Is.True);
            Assert.That(fixture.RoadVehicle(vehicleId).Orders[0].WaitForFull, Is.True);
            Assert.That(fixture.RoadVehicle(vehicleId).Orders[1].WaitForFull, Is.False);
        }

        [Test]
        public void RemoveRouteStop_MiddleStopBetweenSameStation_MergesNeighbours()
        {
            Fixture fixture = Fixture.Create();
            int a = fixture.RoadStations[0];
            int b = fixture.RoadStations[1];
            int c = fixture.RoadStations[2];
            int routeId = RouteTestSupport.CreateRoute(
                fixture.Processor,
                fixture.World,
                RouteMode.Road,
                new[] { a, b, a, c },
                _waitAtThirdStop);

            Assert.That(RouteRules.RemovingStopUnassignsVehicles(fixture.World, routeId, 1), Is.False);
            RouteTestSupport.Accept(fixture.Processor, new RemoveRouteStopCommand(routeId, 1));

            Assert.That(fixture.StationIds(routeId), Is.EqualTo(new[] { a, c }));
            Assert.That(fixture.Route(routeId).Stops[0].WaitForFull, Is.True);
        }

        [Test]
        public void RemoveRouteStop_BelowTwoStops_UnassignsVehiclesAndFlagsRoute()
        {
            Fixture fixture = Fixture.Create();
            int vehicleId = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, fixture.RoadStations[0], fixture.RoadStations[1]);
            int routeId = fixture.RoadVehicle(vehicleId).RouteId;
            Assert.That(RouteRules.RemovingStopUnassignsVehicles(fixture.World, routeId, 1), Is.True);

            RouteTestSupport.Accept(fixture.Processor, new RemoveRouteStopCommand(routeId, 1));

            Assert.That(fixture.Route(routeId).IsValid, Is.False);
            Assert.That(fixture.World.RoadVehicles, Is.Empty);
            Assert.That(fixture.World.UnassignedRoadVehicles.Count, Is.EqualTo(1));
            RoadVehicle parked = fixture.World.UnassignedRoadVehicles[0];
            Assert.That(parked.Id, Is.EqualTo(vehicleId));
            Assert.That(parked.RouteId, Is.EqualTo(RoadVehicle.NoRouteId));
            Assert.That(parked.Orders, Is.Empty);
            Assert.That(parked.Cargo.TotalAmount, Is.EqualTo(0));
        }

        [Test]
        public void RemoveRouteStop_LastStop_ClearsModeSoOtherModeIsAccepted()
        {
            Fixture fixture = Fixture.Create();
            int routeId = RouteTestSupport.CreateRoute(fixture.Processor, fixture.World, RouteMode.Road, new[] { fixture.RoadStations[0] });

            RouteTestSupport.Accept(fixture.Processor, new RemoveRouteStopCommand(routeId, 0));
            RouteTestSupport.Accept(fixture.Processor, new AddRouteStopCommand(routeId, RouteMode.Rail, fixture.RailStations[0]));

            Assert.That(fixture.Route(routeId).Mode, Is.EqualTo(RouteMode.Rail));
        }

        [Test]
        public void BuyVehicleForRoute_RoadRoute_PlacesEmptyTruckAtFirstStop()
        {
            Fixture fixture = Fixture.Create();
            int a = fixture.RoadStations[0];
            int b = fixture.RoadStations[1];
            int routeId = RouteTestSupport.CreateRoute(fixture.Processor, fixture.World, RouteMode.Road, new[] { b, a });
            long cashBefore = fixture.World.CompanyCash;

            int vehicleId = RouteTestSupport.BuyOnRoute(
                fixture.Processor,
                fixture.World,
                routeId,
                fixture.Catalog,
                fixture.Catalog.StartingRoadVehicle.Id);

            RoadVehicle vehicle = fixture.RoadVehicle(vehicleId);
            Assert.That(fixture.World.CompanyCash, Is.EqualTo(cashBefore - fixture.Catalog.StartingRoadVehicle.Purchase));
            Assert.That(vehicle.RouteId, Is.EqualTo(routeId));
            Assert.That(vehicle.OrderStationIds, Is.EqualTo(new[] { b, a }));
            Assert.That(vehicle.IsOnEdge, Is.False);
            Assert.That(vehicle.EdgeFromNodeId, Is.EqualTo(fixture.World.Map.GetTileId(fixture.RoadStationTile(b))));
            Assert.That(vehicle.Cargo.TotalAmount, Is.EqualTo(0));
        }

        [Test]
        public void BuyVehicleForRoute_RailRoute_BuysLocomotiveWithOneWagonAtCombinedPrice()
        {
            Fixture fixture = Fixture.Create();
            int routeId = RouteTestSupport.CreateRoute(
                fixture.Processor,
                fixture.World,
                RouteMode.Rail,
                new[] { fixture.RailStations[0], fixture.RailStations[1] });
            VehicleDefinition locomotive = fixture.Catalog.StartingRailLocomotive;
            long cashBefore = fixture.World.CompanyCash;

            int trainId = RouteTestSupport.BuyOnRoute(fixture.Processor, fixture.World, routeId, fixture.Catalog, locomotive.Id);

            Assert.That(fixture.World.TryGetRailVehicle(trainId, out RailVehicle? train), Is.True);
            Assert.That(train!.WagonCount, Is.EqualTo(1));
            Assert.That(train.RouteId, Is.EqualTo(routeId));
            long price = BuyVehicleForRouteCommand.GetPrice(fixture.Catalog, locomotive);
            Assert.That(price, Is.EqualTo(locomotive.Purchase + fixture.Catalog.RailWagon.Purchase));
            Assert.That(fixture.World.CompanyCash, Is.EqualTo(cashBefore - price));
        }

        [Test]
        public void BuyVehicleForRoute_InvalidRouteOrWrongModeOrNoCash_IsRejected()
        {
            Fixture fixture = Fixture.Create();
            int oneStop = RouteTestSupport.CreateRoute(fixture.Processor, fixture.World, RouteMode.Road, new[] { fixture.RoadStations[0] });
            int road = RouteTestSupport.CreateRoute(
                fixture.Processor,
                fixture.World,
                RouteMode.Road,
                new[] { fixture.RoadStations[0], fixture.RoadStations[1] });
            string truck = fixture.Catalog.StartingRoadVehicle.Id;
            string locomotive = fixture.Catalog.StartingRailLocomotive.Id;

            CommandProcessingResult invalid = fixture.Processor.Process(
                new Tick(0),
                new BuyVehicleForRouteCommand(oneStop, fixture.Catalog, truck, GameDate.FirstYear));
            CommandProcessingResult wrongMode = fixture.Processor.Process(
                new Tick(0),
                new BuyVehicleForRouteCommand(road, fixture.Catalog, locomotive, GameDate.FirstYear));
            fixture.World.DebitCompanyCashClamped(fixture.World.CompanyCash, FinanceCategory.RunningCosts);
            CommandProcessingResult broke = fixture.Processor.Process(
                new Tick(0),
                new BuyVehicleForRouteCommand(road, fixture.Catalog, truck, GameDate.FirstYear));

            Assert.That(invalid.IsAccepted, Is.False);
            Assert.That(wrongMode.IsAccepted, Is.False);
            Assert.That(broke.IsAccepted, Is.False);
            Assert.That(fixture.World.RoadVehicles, Is.Empty);
            Assert.That(fixture.World.RailVehicles, Is.Empty);
        }

        [Test]
        public void UnassignThenAssign_MovesVehicleToOtherRouteFirstStop()
        {
            Fixture fixture = Fixture.Create();
            int a = fixture.RoadStations[0];
            int b = fixture.RoadStations[1];
            int c = fixture.RoadStations[2];
            int vehicleId = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, a, b);
            int firstRoute = fixture.RoadVehicle(vehicleId).RouteId;
            int secondRoute = RouteTestSupport.CreateRoute(fixture.Processor, fixture.World, RouteMode.Road, new[] { c, a });

            RouteTestSupport.Accept(fixture.Processor, new UnassignVehicleCommand(firstRoute, vehicleId));
            Assert.That(fixture.World.TryGetUnassignedRoadVehicle(vehicleId, out _), Is.True);
            Assert.That(RouteRules.CountVehicles(fixture.World, firstRoute), Is.EqualTo(0));
            RouteTestSupport.Accept(fixture.Processor, new AssignVehicleToRouteCommand(secondRoute, vehicleId));

            RoadVehicle vehicle = fixture.RoadVehicle(vehicleId);
            Assert.That(vehicle.RouteId, Is.EqualTo(secondRoute));
            Assert.That(vehicle.OrderStationIds, Is.EqualTo(new[] { c, a }));
            Assert.That(vehicle.EdgeFromNodeId, Is.EqualTo(fixture.World.Map.GetTileId(fixture.RoadStationTile(c))));
            Assert.That(fixture.World.UnassignedRoadVehicles, Is.Empty);
        }

        [Test]
        public void AssignVehicleToRoute_InvalidRouteOrWrongMode_IsRejected()
        {
            Fixture fixture = Fixture.Create();
            int vehicleId = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, fixture.RoadStations[0], fixture.RoadStations[1]);
            int roadRoute = fixture.RoadVehicle(vehicleId).RouteId;
            RouteTestSupport.Accept(fixture.Processor, new UnassignVehicleCommand(roadRoute, vehicleId));
            int oneStop = RouteTestSupport.CreateRoute(fixture.Processor, fixture.World, RouteMode.Road, new[] { fixture.RoadStations[2] });
            int railRoute = RouteTestSupport.CreateRoute(
                fixture.Processor,
                fixture.World,
                RouteMode.Rail,
                new[] { fixture.RailStations[0], fixture.RailStations[1] });

            CommandProcessingResult invalid = fixture.Processor.Process(new Tick(0), new AssignVehicleToRouteCommand(oneStop, vehicleId));
            CommandProcessingResult wrongMode = fixture.Processor.Process(new Tick(0), new AssignVehicleToRouteCommand(railRoute, vehicleId));
            CommandProcessingResult notOnRoute = fixture.Processor.Process(new Tick(0), new UnassignVehicleCommand(roadRoute, vehicleId));

            Assert.That(invalid.IsAccepted, Is.False);
            Assert.That(wrongMode.IsAccepted, Is.False);
            Assert.That(notOnRoute.IsAccepted, Is.False);
            Assert.That(fixture.World.UnassignedRoadVehicles.Count, Is.EqualTo(1));
        }

        [Test]
        public void DeleteRoute_UnassignsItsVehicles()
        {
            Fixture fixture = Fixture.Create();
            int vehicleId = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, fixture.RoadStations[0], fixture.RoadStations[1]);
            int routeId = fixture.RoadVehicle(vehicleId).RouteId;

            RouteTestSupport.Accept(fixture.Processor, new DeleteRouteCommand(routeId));

            Assert.That(fixture.World.Routes, Is.Empty);
            Assert.That(fixture.World.RoadVehicles, Is.Empty);
            Assert.That(fixture.World.TryGetUnassignedRoadVehicle(vehicleId, out _), Is.True);
            Assert.That(fixture.World.OwnedRoadVehicleCount, Is.EqualTo(1));
        }

        [Test]
        public void DemolishStation_RemovesItFromRoutesAndUnassignsVehiclesOfInvalidRoutes()
        {
            Fixture fixture = Fixture.Create();
            int a = fixture.RoadStations[0];
            int b = fixture.RoadStations[1];
            int c = fixture.RoadStations[2];
            int shortRouteVehicle = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, a, b);
            int longRouteVehicle = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, a, b, c);
            int shortRoute = fixture.RoadVehicle(shortRouteVehicle).RouteId;
            int longRoute = fixture.RoadVehicle(longRouteVehicle).RouteId;

            RouteTestSupport.Accept(fixture.Processor, new DemolishRoadStationCommand(fixture.RoadStationTile(b)));

            Assert.That(fixture.StationIds(shortRoute), Is.EqualTo(new[] { a }));
            Assert.That(fixture.StationIds(longRoute), Is.EqualTo(new[] { a, c }));
            Assert.That(fixture.World.TryGetUnassignedRoadVehicle(shortRouteVehicle, out _), Is.True);
            Assert.That(fixture.RoadVehicle(longRouteVehicle).OrderStationIds, Is.EqualTo(new[] { a, c }));
        }

        [Test]
        public void SellRoadVehicle_UnassignedVehicle_IsSold()
        {
            Fixture fixture = Fixture.Create();
            int vehicleId = RouteTestSupport.PlaceRoadVehicle(fixture.Processor, fixture.World, fixture.RoadStations[0], fixture.RoadStations[1]);
            RouteTestSupport.Accept(fixture.Processor, new UnassignVehicleCommand(fixture.RoadVehicle(vehicleId).RouteId, vehicleId));

            RouteTestSupport.Accept(fixture.Processor, new SellRoadVehicleCommand(vehicleId, fixture.Catalog));

            Assert.That(fixture.World.OwnedRoadVehicleCount, Is.EqualTo(0));
        }

        private sealed class Fixture
        {
            private Fixture(GeneratedWorld world)
            {
                World = world;
                Processor = new SimulationCommandProcessor<GeneratedWorld>(world);
                Catalog = RouteTestSupport.CreatePlaceholderCatalog();
                RoadStations = new int[3];
                RailStations = new int[2];
            }

            public GeneratedWorld World { get; }

            public SimulationCommandProcessor<GeneratedWorld> Processor { get; }

            public ContentCatalog Catalog { get; }

            public int[] RoadStations { get; }

            public int[] RailStations { get; }

            public static Fixture Create()
            {
                var settings = new WorldGenerationSettings(
                    new TownPlacementSettings(MapSize.Small, 3, 16),
                    4,
                    new[] { new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _forestOutputCargoIds, 1, 0) });
                var fixture = new Fixture(WorldGenerator.Generate(settings, 61002UL));
                for (int index = 0; index < fixture.RoadStations.Length; index++)
                {
                    TileCoordinate tile = fixture.FindEmptyNeighbor(fixture.World.TownAnchors[index]);
                    RouteTestSupport.Accept(fixture.Processor, new BuildRoadStationCommand(tile));
                    Assert.That(fixture.World.TryGetRoadStationAt(tile, out RoadStation? station), Is.True);
                    fixture.RoadStations[index] = station!.Id;
                }

                for (int index = 0; index < fixture.RailStations.Length; index++)
                {
                    TileCoordinate tile = fixture.FindEmptyNeighbor(fixture.World.TownAnchors[index]);
                    RouteTestSupport.Accept(fixture.Processor, new BuildRailStationCommand(tile));
                    Assert.That(fixture.World.TryGetRailStationAt(tile, out RailStation? station), Is.True);
                    fixture.RailStations[index] = station!.Id;
                }

                return fixture;
            }

            public int CreateRoute()
            {
                int routeId = World.NextRouteId;
                RouteTestSupport.Accept(Processor, new CreateRouteCommand("Route " + routeId));
                return routeId;
            }

            public Route Route(int routeId)
            {
                Assert.That(World.TryGetRoute(routeId, out Route? route), Is.True);
                return route!;
            }

            public int[] StationIds(int routeId)
            {
                Route route = Route(routeId);
                var ids = new int[route.Stops.Count];
                for (int index = 0; index < ids.Length; index++)
                {
                    ids[index] = route.Stops[index].StationId;
                }

                return ids;
            }

            public RoadVehicle RoadVehicle(int vehicleId)
            {
                Assert.That(World.TryGetRoadVehicle(vehicleId, out RoadVehicle? vehicle), Is.True);
                return vehicle!;
            }

            public TileCoordinate RoadStationTile(int stationId)
            {
                Assert.That(World.TryGetRoadStation(stationId, out RoadStation? station), Is.True);
                return station!.Coordinate;
            }

            // Searches the tiles beside the edges of the 2x2 site (not its corners, which are not orthogonally adjacent),
            // so road and rail stations of the same town fit.
            private TileCoordinate FindEmptyNeighbor(TileCoordinate anchor)
            {
                for (int dy = -1; dy <= 2; dy++)
                {
                    for (int dx = -1; dx <= 2; dx++)
                    {
                        bool corner = (dx == -1 || dx == 2) && (dy == -1 || dy == 2);
                        bool inside = dx >= 0 && dx <= 1 && dy >= 0 && dy <= 1;
                        var candidate = new TileCoordinate(anchor.X + dx, anchor.Y + dy);
                        if (!corner && !inside && World.Map.Contains(candidate)
                            && World.Map.GetObjectId(candidate) == TileMap.NoObjectId
                            && World.Map.GetRoadTypeId(candidate) == TileMap.NoRoadTypeId
                            && World.Map.GetRailTypeId(candidate) == TileMap.NoRailTypeId
                            && World.Map.GetStationId(candidate) == TileMap.NoStationId
                            && World.Map.GetRailStationId(candidate) == TileMap.NoRailStationId)
                        {
                            return candidate;
                        }
                    }
                }

                throw new InvalidOperationException("No empty tile next to the site.");
            }
        }
    }
}
