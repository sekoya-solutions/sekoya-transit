#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic periodic industry production and adjacent-station cargo transfer.
    /// </summary>
    public static class IndustryProduction
    {
        /// <summary>
        /// Simulation ticks between production pulses (placeholder; ~1.5s at MapPlay's 50ms tick).
        /// </summary>
        public const int ProductionIntervalTicks = 30;

        private static readonly TileCoordinate[] _neighborOffsets =
        {
            new TileCoordinate(0, 1),
            new TileCoordinate(1, 0),
            new TileCoordinate(0, -1),
            new TileCoordinate(-1, 0)
        };

        /// <summary>
        /// Runs production for every placed industry when <paramref name="tick"/> hits the interval.
        /// </summary>
        public static void AdvanceAll(GeneratedWorld world, ContentCatalog catalog, Tick tick)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            if (tick.Value <= 0 || tick.Value % ProductionIntervalTicks != 0)
            {
                return;
            }

            for (int index = 0; index < world.Industries.Count; index++)
            {
                AdvanceIndustry(world, catalog, world.Industries[index]);
            }
        }

        /// <summary>
        /// Runs one production pulse for every industry orthogonally adjacent to
        /// <paramref name="stationCoordinate"/> (e.g. right after a station is built).
        /// </summary>
        public static void AdvanceIndustriesAdjacentTo(
            GeneratedWorld world,
            ContentCatalog catalog,
            TileCoordinate stationCoordinate)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            int townCount = world.TownAnchors.Count;
            for (int index = 0; index < _neighborOffsets.Length; index++)
            {
                TileCoordinate offset = _neighborOffsets[index];
                var neighbor = new TileCoordinate(
                    stationCoordinate.X + offset.X,
                    stationCoordinate.Y + offset.Y);
                if (!world.Map.Contains(neighbor))
                {
                    continue;
                }

                int objectId = world.Map.GetObjectId(neighbor);
                if (objectId < townCount)
                {
                    continue;
                }

                int industryIndex = objectId - townCount;
                if (industryIndex < 0 || industryIndex >= world.Industries.Count)
                {
                    continue;
                }

                AdvanceIndustry(world, catalog, world.Industries[industryIndex]);
            }
        }

        private static void AdvanceIndustry(
            GeneratedWorld world,
            ContentCatalog catalog,
            PlacedIndustry industry)
        {
            if (!catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) ||
                definition == null)
            {
                return;
            }

            IReadOnlyList<RoadStation> adjacentStations = CollectAdjacentStations(world, industry.Coordinate);

            if (definition.Level >= 2)
            {
                PullInputsFromStations(industry, definition, adjacentStations);
            }

            TryProduce(industry, definition);
            PushOutputsToStations(industry, definition, adjacentStations);
        }

        private static void PullInputsFromStations(
            PlacedIndustry industry,
            IndustryDefinition definition,
            IReadOnlyList<RoadStation> adjacentStations)
        {
            for (int inputIndex = 0; inputIndex < definition.Inputs.Count; inputIndex++)
            {
                CargoAmountDefinition input = definition.Inputs[inputIndex];
                int needed = input.Amount - industry.Cargo.GetAmount(input.CargoId);
                if (needed <= 0)
                {
                    continue;
                }

                for (int stationIndex = 0; stationIndex < adjacentStations.Count && needed > 0; stationIndex++)
                {
                    RoadStation station = adjacentStations[stationIndex];
                    int available = station.Cargo.GetAmount(input.CargoId);
                    if (available <= 0)
                    {
                        continue;
                    }

                    int free = industry.FreeCargoCapacity;
                    if (free <= 0)
                    {
                        return;
                    }

                    int move = needed;
                    if (move > available)
                    {
                        move = available;
                    }

                    if (move > free)
                    {
                        move = free;
                    }

                    int removed = station.Cargo.TryRemove(input.CargoId, move);
                    if (removed > 0)
                    {
                        industry.Cargo.Add(input.CargoId, removed);
                        needed -= removed;
                    }
                }
            }
        }

        private static void TryProduce(PlacedIndustry industry, IndustryDefinition definition)
        {
            for (int inputIndex = 0; inputIndex < definition.Inputs.Count; inputIndex++)
            {
                CargoAmountDefinition input = definition.Inputs[inputIndex];
                if (industry.Cargo.GetAmount(input.CargoId) < input.Amount)
                {
                    return;
                }
            }

            int outputUnits = 0;
            for (int outputIndex = 0; outputIndex < definition.Outputs.Count; outputIndex++)
            {
                outputUnits = checked(outputUnits + definition.Outputs[outputIndex].Amount);
            }

            int inputUnits = 0;
            for (int inputIndex = 0; inputIndex < definition.Inputs.Count; inputIndex++)
            {
                inputUnits = checked(inputUnits + definition.Inputs[inputIndex].Amount);
            }

            int freeAfterConsume = industry.FreeCargoCapacity + inputUnits;
            if (freeAfterConsume < outputUnits)
            {
                return;
            }

            for (int inputIndex = 0; inputIndex < definition.Inputs.Count; inputIndex++)
            {
                CargoAmountDefinition input = definition.Inputs[inputIndex];
                industry.Cargo.TryRemove(input.CargoId, input.Amount);
            }

            for (int outputIndex = 0; outputIndex < definition.Outputs.Count; outputIndex++)
            {
                CargoAmountDefinition output = definition.Outputs[outputIndex];
                industry.TryAddCargo(output.CargoId, output.Amount);
            }
        }

        private static void PushOutputsToStations(
            PlacedIndustry industry,
            IndustryDefinition definition,
            IReadOnlyList<RoadStation> adjacentStations)
        {
            for (int outputIndex = 0; outputIndex < definition.Outputs.Count; outputIndex++)
            {
                string cargoId = definition.Outputs[outputIndex].CargoId;
                int available = industry.Cargo.GetAmount(cargoId);
                if (available <= 0)
                {
                    continue;
                }

                for (int stationIndex = 0; stationIndex < adjacentStations.Count && available > 0; stationIndex++)
                {
                    RoadStation station = adjacentStations[stationIndex];
                    int free = station.FreeCargoCapacity;
                    if (free <= 0)
                    {
                        continue;
                    }

                    int move = available < free ? available : free;
                    int removed = industry.Cargo.TryRemove(cargoId, move);
                    if (removed <= 0)
                    {
                        break;
                    }

                    station.TryAddCargo(cargoId, removed);
                    available -= removed;
                }
            }
        }

        private static List<RoadStation> CollectAdjacentStations(
            GeneratedWorld world,
            TileCoordinate coordinate)
        {
            var stations = new List<RoadStation>(4);
            for (int index = 0; index < _neighborOffsets.Length; index++)
            {
                TileCoordinate offset = _neighborOffsets[index];
                var neighbor = new TileCoordinate(coordinate.X + offset.X, coordinate.Y + offset.Y);
                if (!world.TryGetRoadStationAt(neighbor, out RoadStation? station) || station == null)
                {
                    continue;
                }

                stations.Add(station);
            }

            stations.Sort(CompareStationsById);
            return stations;
        }

        private static int CompareStationsById(RoadStation left, RoadStation right)
        {
            return left.Id.CompareTo(right.Id);
        }
    }
}
