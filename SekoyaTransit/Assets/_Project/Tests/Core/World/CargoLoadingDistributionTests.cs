#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    /// <summary>
    /// Pins the order in which waiting vehicles load, across several sites and both modes (Issue #308): each waiting
    /// vehicle takes one unit per tick while the stock lasts (Issue #332), in turn when it runs short, road before rail
    /// and by ascending id.
    /// </summary>
    [TestFixture]
    public sealed class CargoLoadingDistributionTests
    {
        private static readonly bool[] _waitAtFirstStop = { true, false };
        private static readonly string[] _woodCargoIds = { "wood" };
        private static readonly GameDate _january = new GameDate(1900, 1, 1);

        [Test]
        public void IndustryDistribute_RoadAndRailAtTwoSites_EachSiteServesItsLoadersInTurn()
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ContentCatalog catalog = RouteTestSupport.CreatePlaceholderCatalog();
            PlacedIndustry forestA = world.Industries[0];
            PlacedIndustry forestB = world.Industries[1];
            int roadRouteA = RoadRoute(processor, world, forestA.Coordinate, world.TownAnchors[0]);
            int railRouteA = RailRoute(processor, world, forestA.Coordinate, world.TownAnchors[0]);
            int roadRouteB = RoadRoute(processor, world, forestB.Coordinate, world.TownAnchors[1]);

            // Bought rail first: the order must come from the mode and the id, not from when a vehicle was bought.
            int train = RouteTestSupport.BuyOnRoute(processor, world, railRouteA, catalog, catalog.StartingRailLocomotive.Id);
            int truckA1 = RouteTestSupport.BuyOnRoute(processor, world, roadRouteA, catalog, catalog.StartingRoadVehicle.Id);
            int truckB1 = RouteTestSupport.BuyOnRoute(processor, world, roadRouteB, catalog, catalog.StartingRoadVehicle.Id);
            int truckA2 = RouteTestSupport.BuyOnRoute(processor, world, roadRouteA, catalog, catalog.StartingRoadVehicle.Id);
            int truckB2 = RouteTestSupport.BuyOnRoute(processor, world, roadRouteB, catalog, catalog.StartingRoadVehicle.Id);
            ArriveAll(world);
            forestA.Cargo.Clear();
            forestB.Cargo.Clear();

            // An empty industry serves nobody and keeps its turn.
            IndustryCargoLoading.Distribute(world, null, _january);
            Assert.That(forestA.LoadRoundRobinIndex, Is.Zero);
            Assert.That(Road(world, truckA1), Is.Zero);

            forestA.TryAddCargo("wood", 2);
            forestB.TryAddCargo("wood", 3);

            // Every waiting vehicle takes 1 unit per tick while the stock lasts (Issue #332): forest A runs out after
            // its two trucks, so the train waits; forest B serves both its trucks and keeps 1 unit.
            IndustryCargoLoading.Distribute(world, null, _january);
            Assert.That(string.Join(",", Road(world, truckA1), Road(world, truckA2), Rail(world, train), Road(world, truckB1), Road(world, truckB2)), Is.EqualTo("1,1,0,1,1"));

            // The next unit at forest A goes to the train, left without one last tick; forest B starts again with its
            // first truck and runs out.
            forestA.TryAddCargo("wood", 1);
            IndustryCargoLoading.Distribute(world, null, _january);
            Assert.That(string.Join(",", Road(world, truckA1), Road(world, truckA2), Rail(world, train), Road(world, truckB1), Road(world, truckB2)), Is.EqualTo("1,1,1,2,1"));

            // Both forests are empty: nothing moves and loading never exceeds the stock.
            IndustryCargoLoading.Distribute(world, null, _january);
            Assert.That(string.Join(",", Road(world, truckA1), Road(world, truckA2), Rail(world, train), Road(world, truckB1), Road(world, truckB2)), Is.EqualTo("1,1,1,2,1"));
            Assert.That(forestA.MonthTransported, Is.EqualTo(3L));
            Assert.That(forestB.MonthTransported, Is.EqualTo(3L));
        }

        [Test]
        public void TownDistribute_RoadAndRailAtTwoSites_EachSiteServesItsLoadersInTurn()
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ContentCatalog catalog = CreatePassengerCatalog();
            Town townA = world.Towns[0];
            Town townB = world.Towns[1];
            int roadRouteA = RoadRoute(processor, world, townA.Coordinate, world.TownAnchors[2]);
            int railRouteA = RailRoute(processor, world, townA.Coordinate, world.TownAnchors[2]);
            int roadRouteB = RoadRoute(processor, world, townB.Coordinate, world.TownAnchors[3]);
            int train = RouteTestSupport.BuyOnRoute(processor, world, railRouteA, catalog, catalog.StartingRailLocomotive.Id);
            int truckA = RouteTestSupport.BuyOnRoute(processor, world, roadRouteA, catalog, catalog.StartingRoadVehicle.Id);
            int truckB = RouteTestSupport.BuyOnRoute(processor, world, roadRouteB, catalog, catalog.StartingRoadVehicle.Id);
            ArriveAll(world);
            townA.WaitingCargo.Clear();
            townB.WaitingCargo.Clear();
            townA.WaitingCargo.Add("passengers", 3);
            townB.WaitingCargo.Add("passengers", 1);

            // Every waiting vehicle takes 1 passenger per tick while they last (Issue #332).
            TownCargoLoading.Distribute(world, catalog, _january);
            Assert.That(string.Join(",", Road(world, truckA), Rail(world, train), Road(world, truckB)), Is.EqualTo("1,1,1"));

            // One passenger left at town A: it goes to the truck, first in turn again; town B is empty.
            TownCargoLoading.Distribute(world, catalog, _january);
            Assert.That(string.Join(",", Road(world, truckA), Rail(world, train), Road(world, truckB)), Is.EqualTo("2,1,1"));

            TownCargoLoading.Distribute(world, catalog, _january);
            Assert.That(string.Join(",", Road(world, truckA), Rail(world, train), Road(world, truckB)), Is.EqualTo("2,1,1"));
            Assert.That(townA.WaitingCargo.TotalAmount, Is.Zero);
            Assert.That(townB.WaitingCargo.TotalAmount, Is.Zero);
        }

        [Test]
        public void IndustryDistribute_FullVehicle_LeavesTheTurnToTheOthers()
        {
            GeneratedWorld world = CreateWorld();
            var processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            ContentCatalog catalog = RouteTestSupport.CreatePlaceholderCatalog();
            PlacedIndustry forest = world.Industries[0];
            int route = RoadRoute(processor, world, forest.Coordinate, world.TownAnchors[0]);
            int full = RouteTestSupport.BuyOnRoute(processor, world, route, catalog, catalog.StartingRoadVehicle.Id);
            int free = RouteTestSupport.BuyOnRoute(processor, world, route, catalog, catalog.StartingRoadVehicle.Id);
            ArriveAll(world);
            forest.Cargo.Clear();
            Assert.That(world.TryGetRoadVehicle(full, out RoadVehicle? fullVehicle), Is.True);
            fullVehicle!.Cargo.Add("wood", fullVehicle.FreeCargoCapacity);
            forest.TryAddCargo("wood", 2);

            IndustryCargoLoading.Distribute(world, null, _january);
            IndustryCargoLoading.Distribute(world, null, _january);

            Assert.That(Road(world, free), Is.EqualTo(2));
        }

        private static int Road(GeneratedWorld world, int vehicleId)
        {
            Assert.That(world.TryGetRoadVehicle(vehicleId, out RoadVehicle? vehicle), Is.True);
            return vehicle!.Cargo.TotalAmount;
        }

        private static int Rail(GeneratedWorld world, int vehicleId)
        {
            Assert.That(world.TryGetRailVehicle(vehicleId, out RailVehicle? vehicle), Is.True);
            return vehicle!.Cargo.TotalAmount;
        }

        // Vehicles start at their first stop, so a few movement passes put them in service there, waiting for a full load.
        private static void ArriveAll(GeneratedWorld world)
        {
            for (int pass = 0; pass < 200; pass++)
            {
                RoadVehicleMovement.AdvanceUnloadOrTravelAll(world, null, _january);
                RailVehicleMovement.AdvanceUnloadOrTravelAll(world, null, _january);
            }

            foreach (RoadVehicle vehicle in world.RoadVehicles)
            {
                Assert.That(vehicle.IsServicingStop && vehicle.IsUnloadComplete, Is.True, "truck must be waiting to load");
            }

            foreach (RailVehicle vehicle in world.RailVehicles)
            {
                Assert.That(vehicle.IsServicingStop && vehicle.IsUnloadComplete, Is.True, "train must be waiting to load");
            }
        }

        private static int RoadRoute(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            TileCoordinate from,
            TileCoordinate to)
        {
            return RouteTestSupport.CreateRoute(
                processor,
                world,
                RouteMode.Road,
                new[] { RoadStationNear(processor, world, from), RoadStationNear(processor, world, to) },
                _waitAtFirstStop);
        }

        private static int RailRoute(
            SimulationCommandProcessor<GeneratedWorld> processor,
            GeneratedWorld world,
            TileCoordinate from,
            TileCoordinate to)
        {
            return RouteTestSupport.CreateRoute(
                processor,
                world,
                RouteMode.Rail,
                new[] { RailStationNear(processor, world, from), RailStationNear(processor, world, to) },
                _waitAtFirstStop);
        }

        private static int RoadStationNear(SimulationCommandProcessor<GeneratedWorld> processor, GeneratedWorld world, TileCoordinate anchor)
        {
            TileCoordinate tile = FindStationTile(world, anchor);
            if (world.TryGetRoadStationAt(tile, out RoadStation? existing) && existing != null)
            {
                return existing.Id;
            }

            RouteTestSupport.Accept(processor, new BuildRoadStationCommand(tile));
            Assert.That(world.TryGetRoadStationAt(tile, out RoadStation? station), Is.True);
            return station!.Id;
        }

        private static int RailStationNear(SimulationCommandProcessor<GeneratedWorld> processor, GeneratedWorld world, TileCoordinate anchor)
        {
            TileCoordinate tile = FindStationTile(world, anchor);
            RouteTestSupport.Accept(processor, new BuildRailStationCommand(tile));
            Assert.That(world.TryGetRailStationAt(tile, out RailStation? station), Is.True);
            return station!.Id;
        }

        // The first free tile touching the site at this anchor; road stations are reused, so a rail station gets a new tile.
        private static TileCoordinate FindStationTile(GeneratedWorld world, TileCoordinate anchor)
        {
            int objectId = world.Map.GetObjectId(anchor);
            for (int y = anchor.Y - 5; y <= anchor.Y + 5; y++)
            {
                for (int x = anchor.X - 5; x <= anchor.X + 5; x++)
                {
                    var tile = new TileCoordinate(x, y);
                    if (world.Map.Contains(tile) &&
                        world.Map.GetObjectId(tile) == TileMap.NoObjectId &&
                        world.Map.GetStationId(tile) == TileMap.NoStationId &&
                        world.Map.GetRailStationId(tile) == TileMap.NoRailStationId &&
                        Touches(world, tile, objectId))
                    {
                        return tile;
                    }
                }
            }

            Assert.Fail("No free tile next to the site.");
            return anchor;
        }

        private static bool Touches(GeneratedWorld world, TileCoordinate tile, int objectId)
        {
            return IsObject(world, new TileCoordinate(tile.X + 1, tile.Y), objectId) ||
                   IsObject(world, new TileCoordinate(tile.X - 1, tile.Y), objectId) ||
                   IsObject(world, new TileCoordinate(tile.X, tile.Y + 1), objectId) ||
                   IsObject(world, new TileCoordinate(tile.X, tile.Y - 1), objectId);
        }

        private static bool IsObject(GeneratedWorld world, TileCoordinate tile, int objectId)
        {
            return world.Map.Contains(tile) && world.Map.GetObjectId(tile) == objectId;
        }

        private static ContentCatalog CreatePassengerCatalog()
        {
            return new ContentCatalog(
                new[]
                {
                    new CargoDefinition("passengers", "cargo.passengers", CargoClass.Passengers, 10, 0),
                    new CargoDefinition("mail", "cargo.mail", CargoClass.Passengers, 6, 0)
                },
                Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());
        }

        private static GeneratedWorld CreateWorld()
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, Array.Empty<string>(), _woodCargoIds, 2, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 4, 16),
                5,
                industryRules);
            return WorldGenerator.Generate(settings, 46001UL);
        }
    }
}
