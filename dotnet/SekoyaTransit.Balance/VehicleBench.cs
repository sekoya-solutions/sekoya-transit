#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// One vehicle at one distance: its trip time and the income it earned per game year.
    /// </summary>
    internal sealed class VehicleBenchRow
    {
        public VehicleBenchRow(string vehicleId, int distance, double ticksPerTrip, int deliveries, double incomePerYear)
        {
            VehicleId = vehicleId;
            Distance = distance;
            TicksPerTrip = ticksPerTrip;
            Deliveries = deliveries;
            IncomePerYear = incomePerYear;
        }

        public string VehicleId { get; }

        public int Distance { get; }

        public double TicksPerTrip { get; }

        public int Deliveries { get; }

        public double IncomePerYear { get; }
    }

    /// <summary>
    /// Measures one vehicle of a model, carrying one raw cargo from an always stocked producer to a consumer, at each
    /// station distance on a Small map. Income comes from the real delivery pay; because a vehicle never waits for
    /// cargo here, this is the strictest test of whether a longer route earns more per year.
    /// </summary>
    internal static class VehicleBench
    {
        public const int DefaultLastDistance = 180;

        private const int _ticksPerYear = 36000;
        private const int _seedBatch = 256;
        private const int _maximumSeed = 6000;
        private const int _candidatesWanted = 20;
        private const int _maximumProducerDistance = 120;
        private const int _townSpacing = 8;
        private const int _industrySpacing = 5;
        private const int _minimumDeliveries = 3;
        private const int _cargoAddedPerTick = 5;
        private const long _benchCash = 1_000_000_000_000L;
        private static readonly int[] _offsetX = { 0, 1, 0, -1 };
        private static readonly int[] _offsetY = { -1, 0, 1, 0 };

        /// <summary>
        /// Returns the vehicle models a bench can measure: every truck and locomotive.
        /// </summary>
        public static IReadOnlyList<VehicleDefinition> Models(ContentCatalog catalog)
        {
            return catalog.Vehicles.Where(vehicle => Capacity(vehicle) > 0).ToList();
        }

        /// <summary>
        /// Measures each model at every distance in the range; a distance no map could build is left out.
        /// </summary>
        public static IReadOnlyList<VehicleBenchRow> Run(
            ContentCatalog catalog,
            IReadOnlyList<VehicleDefinition> models,
            int firstDistance,
            int lastDistance,
            int years)
        {
            Chain chain = FindChain(catalog);
            Dictionary<int, List<Candidate>> candidates = FindCandidates(catalog, chain, firstDistance, lastDistance);
            var rows = new ConcurrentBag<VehicleBenchRow>();
            foreach (VehicleDefinition model in models)
            {
                Parallel.For(firstDistance, lastDistance + 1, distance =>
                {
                    if (!candidates.TryGetValue(distance, out List<Candidate>? options))
                    {
                        return;
                    }

                    foreach (Candidate candidate in options)
                    {
                        VehicleBenchRow? row = Measure(catalog, chain, model, distance, candidate, years);
                        if (row != null)
                        {
                            rows.Add(row);
                            return;
                        }
                    }
                });
            }

            return rows.OrderBy(row => row.VehicleId, StringComparer.Ordinal).ThenBy(row => row.Distance).ToList();
        }

        /// <summary>
        /// Writes the Markdown report: one summary line per model, then its measurements.
        /// </summary>
        public static string WriteReport(IReadOnlyList<VehicleBenchRow> rows, int firstDistance, int lastDistance, int years)
        {
            var report = new StringBuilder();
            report.AppendLine("# One-vehicle income test");
            report.AppendLine();
            report.AppendLine(Invariant($"One vehicle per model, always stocked with one raw cargo, on a Small map, {years} game years per distance, from {firstDistance} to {lastDistance} tiles. Income comes from the real delivery pay. A step that does not rise can come from whole-dollar pay when the rise is a few cents; a longer run settles that."));
            report.AppendLine();
            report.AppendLine("| Vehicle | First distance | Income | Last distance | Income | Gap | Steps | Steps not rising |");
            report.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
            List<IGrouping<string, VehicleBenchRow>> models = rows.GroupBy(row => row.VehicleId, StringComparer.Ordinal).ToList();
            foreach (IGrouping<string, VehicleBenchRow> model in models)
            {
                List<VehicleBenchRow> measured = model.ToList();
                VehicleBenchRow first = measured[0];
                VehicleBenchRow last = measured[measured.Count - 1];
                int notRising = Enumerable.Range(1, measured.Count - 1).Count(index => measured[index].IncomePerYear <= measured[index - 1].IncomePerYear);
                report.AppendLine(Invariant($"| {model.Key} | {first.Distance} | {first.IncomePerYear:N2} | {last.Distance} | {last.IncomePerYear:N2} | {last.IncomePerYear - first.IncomePerYear:N2} | {measured.Count - 1} | {notRising} |"));
            }

            foreach (IGrouping<string, VehicleBenchRow> model in models)
            {
                report.AppendLine();
                report.AppendLine("## " + model.Key);
                report.AppendLine();
                report.AppendLine("| Distance | Ticks per trip | Deliveries | Income per year |");
                report.AppendLine("| ---: | ---: | ---: | ---: |");
                foreach (VehicleBenchRow row in model)
                {
                    report.AppendLine(Invariant($"| {row.Distance} | {row.TicksPerTrip:F2} | {row.Deliveries} | {row.IncomePerYear:N2} |"));
                }
            }

            return report.ToString();
        }

        private static int Capacity(VehicleDefinition vehicle)
        {
            return vehicle.Mode == VehicleMode.Road ? vehicle.CargoCapacity : vehicle.MaxWagons * vehicle.CapacityPerWagon;
        }

        private static string Invariant(FormattableString text)
        {
            return text.ToString(CultureInfo.InvariantCulture);
        }

        private static Chain FindChain(ContentCatalog catalog)
        {
            foreach (IndustryDefinition producer in catalog.Industries.Where(industry => industry.Inputs.Count == 0))
            {
                foreach (CargoAmountDefinition output in producer.Outputs)
                {
                    IndustryDefinition? consumer = catalog.Industries.FirstOrDefault(industry =>
                        industry.Inputs.Count == 1 && string.Equals(industry.Inputs[0].CargoId, output.CargoId, StringComparison.Ordinal));
                    if (consumer != null)
                    {
                        return new Chain(producer, consumer, output.CargoId);
                    }
                }
            }

            throw new InvalidOperationException("The content has no raw cargo with a one-input consumer.");
        }

        private static GeneratedWorld Generate(ContentCatalog catalog, Chain chain, ulong seed)
        {
            var rules = new[]
            {
                new IndustryPlacementRule(chain.Producer.Id, chain.Producer.Level, Array.Empty<string>(), Ids(chain.Producer.Outputs), 1, 0),
                new IndustryPlacementRule(chain.Consumer.Id, chain.Consumer.Level, Ids(chain.Consumer.Inputs), Ids(chain.Consumer.Outputs), 1, _maximumProducerDistance),
            };
            var settings = new WorldGenerationSettings(new TownPlacementSettings(MapSize.Small, 1, _townSpacing), _industrySpacing, rules);
            return WorldGenerator.Generate(settings, seed, catalog.TownNeeds, catalog);
        }

        private static string[] Ids(IReadOnlyList<CargoAmountDefinition> amounts)
        {
            return amounts.Select(amount => amount.CargoId).ToArray();
        }

        private static Dictionary<int, List<Candidate>> FindCandidates(ContentCatalog catalog, Chain chain, int firstDistance, int lastDistance)
        {
            var found = new ConcurrentDictionary<int, ConcurrentBag<Candidate>>();
            for (int start = 1; start <= _maximumSeed; start += _seedBatch)
            {
                Parallel.For(start, Math.Min(start + _seedBatch, _maximumSeed + 1), seed =>
                {
                    GeneratedWorld world = Generate(catalog, chain, (ulong)seed);
                    TileCoordinate[] from = Neighbors(Find(world, chain.Producer.Id).Coordinate);
                    TileCoordinate[] to = Neighbors(Find(world, chain.Consumer.Id).Coordinate);
                    for (int a = 0; a < from.Length; a++)
                    {
                        for (int b = 0; b < to.Length; b++)
                        {
                            int distance = Math.Abs(from[a].X - to[b].X) + Math.Abs(from[a].Y - to[b].Y);
                            if (distance >= firstDistance && distance <= lastDistance && IsFree(world, from[a]) && IsFree(world, to[b]))
                            {
                                found.GetOrAdd(distance, _ => new ConcurrentBag<Candidate>()).Add(new Candidate((ulong)seed, a, b));
                            }
                        }
                    }
                });

                bool enough = Enumerable.Range(firstDistance, lastDistance - firstDistance + 1)
                    .All(distance => found.TryGetValue(distance, out ConcurrentBag<Candidate>? bag) && bag.Count >= _candidatesWanted);
                if (enough)
                {
                    break;
                }
            }

            return found.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.OrderBy(candidate => candidate.Seed).ThenBy(candidate => candidate.From).ThenBy(candidate => candidate.To).ToList());
        }

        private static VehicleBenchRow? Measure(ContentCatalog catalog, Chain chain, VehicleDefinition model, int distance, Candidate candidate, int years)
        {
            GeneratedWorld world = Generate(catalog, chain, candidate.Seed);
            PlacedIndustry producer = Find(world, chain.Producer.Id);
            PlacedIndustry consumer = Find(world, chain.Consumer.Id);
            TileCoordinate from = Neighbors(producer.Coordinate)[candidate.From];
            TileCoordinate to = Neighbors(consumer.Coordinate)[candidate.To];
            int year = catalog.Vehicles.Max(vehicle => vehicle.AvailableYear);
            bool rail = model.Mode != VehicleMode.Road;
            var bench = new Bench(world);
            if (!bench.Do(new CheatAddCashCommand(_benchCash)) ||
                !bench.Do(new CheatUnlockAllVehicleTypesCommand(catalog)) ||
                !BuildRoute(bench, from, to, distance, rail))
            {
                return null;
            }

            int routeId = world.NextRouteId;
            RouteMode mode = rail ? RouteMode.Rail : RouteMode.Road;
            if (!StationIds(world, from, to, rail, out int fromId, out int toId) ||
                !bench.Do(new CreateRouteCommand("Bench")) ||
                !bench.Do(new AddRouteStopCommand(routeId, mode, fromId)) ||
                !bench.Do(new AddRouteStopCommand(routeId, mode, toId)) ||
                !bench.Do(new SetRouteStopWaitForFullCommand(routeId, 0, true)) ||
                !bench.Do(rail
                    ? new BuyVehicleForRouteCommand(routeId, catalog, model.Id, year, model.MaxWagons)
                    : new BuyVehicleForRouteCommand(routeId, catalog, model.Id, year)))
            {
                return null;
            }

            return Drive(world, catalog, producer, consumer, chain.CargoId, model, distance, rail, year, years);
        }

        private static bool BuildRoute(Bench bench, TileCoordinate from, TileCoordinate to, int distance, bool rail)
        {
            if (rail
                ? !bench.Do(new BuildRailStationCommand(from)) || !bench.Do(new BuildRailStationCommand(to))
                : !bench.Do(new BuildRoadStationCommand(from)) || !bench.Do(new BuildRoadStationCommand(to)))
            {
                return false;
            }

            List<TileCoordinate>? path = FindPath(bench.World, from, to);
            if (path == null || path.Count - 1 != distance)
            {
                return false;
            }

            foreach (TileCoordinate step in path.Where(step => !step.Equals(from) && !step.Equals(to)))
            {
                bool built = rail ? bench.Do(new BuildRailCommand(step)) : bench.Do(new BuildRoadCommand(step));
                if (!built)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StationIds(GeneratedWorld world, TileCoordinate from, TileCoordinate to, bool rail, out int fromId, out int toId)
        {
            fromId = 0;
            toId = 0;
            if (rail)
            {
                if (!world.TryGetRailStationAt(from, out RailStation? a) || a == null || !world.TryGetRailStationAt(to, out RailStation? b) || b == null)
                {
                    return false;
                }

                fromId = a.Id;
                toId = b.Id;
                return true;
            }

            if (!world.TryGetRoadStationAt(from, out RoadStation? c) || c == null || !world.TryGetRoadStationAt(to, out RoadStation? d) || d == null)
            {
                return false;
            }

            fromId = c.Id;
            toId = d.Id;
            return true;
        }

        private static VehicleBenchRow? Drive(
            GeneratedWorld world,
            ContentCatalog catalog,
            PlacedIndustry producer,
            PlacedIndustry consumer,
            string cargoId,
            VehicleDefinition model,
            int distance,
            bool rail,
            int year,
            int years)
        {
            int capacity = Capacity(model);
            var date = new GameDate(year, 1, 1);
            var deliveryTicks = new List<int>();
            var incomes = new List<long>();
            long counted = 0L;
            for (int now = 0; now < years * _ticksPerYear; now++)
            {
                producer.TryAddCargo(cargoId, _cargoAddedPerTick);
                consumer.Cargo.Clear();
                if (rail)
                {
                    RailVehicleMovement.AdvanceAll(world, catalog, date);
                }
                else
                {
                    RoadVehicleMovement.AdvanceAll(world, catalog, date);
                }

                long delivered = world.GetCargoDelivered(cargoId, DeliveryDestinationKind.Any);
                if (delivered / capacity > counted / capacity)
                {
                    deliveryTicks.Add(now);
                    incomes.Add(rail ? world.RailVehicles[0].LifetimeDeliveryIncome : world.RoadVehicles[0].LifetimeDeliveryIncome);
                }

                counted = delivered;
            }

            if (deliveryTicks.Count < _minimumDeliveries)
            {
                return null;
            }

            int span = deliveryTicks[deliveryTicks.Count - 1] - deliveryTicks[0];
            double ticksPerTrip = (double)span / (deliveryTicks.Count - 1);
            double incomePerYear = (double)(incomes[incomes.Count - 1] - incomes[0]) * _ticksPerYear / span;
            return new VehicleBenchRow(model.Id, distance, ticksPerTrip, deliveryTicks.Count, incomePerYear);
        }

        private static List<TileCoordinate>? FindPath(GeneratedWorld world, TileCoordinate from, TileCoordinate to)
        {
            var cameFrom = new Dictionary<int, int> { [world.Map.GetTileId(from)] = -1 };
            var queue = new Queue<TileCoordinate>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                TileCoordinate current = queue.Dequeue();
                if (current.Equals(to))
                {
                    var path = new List<TileCoordinate>();
                    for (int cursor = world.Map.GetTileId(to); cursor >= 0; cursor = cameFrom[cursor])
                    {
                        path.Add(world.Map.GetCoordinate(cursor));
                    }

                    path.Reverse();
                    return path;
                }

                for (int direction = 0; direction < _offsetX.Length; direction++)
                {
                    var next = new TileCoordinate(current.X + _offsetX[direction], current.Y + _offsetY[direction]);
                    if (world.Map.Contains(next) && !cameFrom.ContainsKey(world.Map.GetTileId(next)) && (next.Equals(to) || IsFree(world, next)))
                    {
                        cameFrom[world.Map.GetTileId(next)] = world.Map.GetTileId(current);
                        queue.Enqueue(next);
                    }
                }
            }

            return null;
        }

        private static TileCoordinate[] Neighbors(TileCoordinate anchor)
        {
            return Enumerable.Range(0, _offsetX.Length)
                .Select(direction => new TileCoordinate(anchor.X + _offsetX[direction], anchor.Y + _offsetY[direction]))
                .ToArray();
        }

        private static bool IsFree(GeneratedWorld world, TileCoordinate tile)
        {
            return world.Map.Contains(tile) &&
                   world.Map.GetObjectId(tile) == TileMap.NoObjectId &&
                   world.Map.GetStationId(tile) == TileMap.NoStationId;
        }

        private static PlacedIndustry Find(GeneratedWorld world, string industryId)
        {
            return world.Industries.First(industry => string.Equals(industry.IndustryId, industryId, StringComparison.Ordinal));
        }

        private sealed class Bench
        {
            private readonly SimulationCommandProcessor<GeneratedWorld> _processor;
            private int _tick = 1;

            public Bench(GeneratedWorld world)
            {
                World = world;
                _processor = new SimulationCommandProcessor<GeneratedWorld>(world);
            }

            public GeneratedWorld World { get; }

            public bool Do(ISimulationCommand<GeneratedWorld> command)
            {
                return _processor.Process(new Tick(_tick++), command).IsAccepted;
            }
        }

        private sealed class Chain
        {
            public Chain(IndustryDefinition producer, IndustryDefinition consumer, string cargoId)
            {
                Producer = producer;
                Consumer = consumer;
                CargoId = cargoId;
            }

            public IndustryDefinition Producer { get; }

            public IndustryDefinition Consumer { get; }

            public string CargoId { get; }
        }

        private sealed class Candidate
        {
            public Candidate(ulong seed, int from, int to)
            {
                Seed = seed;
                From = from;
                To = to;
            }

            public ulong Seed { get; }

            public int From { get; }

            public int To { get; }
        }
    }
}
