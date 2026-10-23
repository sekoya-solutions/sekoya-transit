#nullable enable
using System;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic periodic industry production onto industry stock (Issue #132).
    /// Outputs stay on the industry until a vehicle loads; no station cargo transfer.
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
        /// Month produced counters follow <paramref name="calendarDate"/>.
        /// </summary>
        public static void AdvanceAll(
            GeneratedWorld world,
            ContentCatalog catalog,
            Tick tick,
            GameDate calendarDate)
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
                AdvanceIndustry(world, catalog, world.Industries[index], calendarDate);
            }
        }

        /// <summary>
        /// Runs one production pulse for every industry orthogonally adjacent to
        /// <paramref name="stationCoordinate"/> (e.g. right after a station is built).
        /// </summary>
        public static void AdvanceIndustriesAdjacentTo(
            GeneratedWorld world,
            ContentCatalog catalog,
            TileCoordinate stationCoordinate,
            GameDate calendarDate)
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

                AdvanceIndustry(world, catalog, world.Industries[industryIndex], calendarDate);
            }
        }

        private static void AdvanceIndustry(
            GeneratedWorld world,
            ContentCatalog catalog,
            PlacedIndustry industry,
            GameDate calendarDate)
        {
            if (!catalog.TryGetIndustry(industry.IndustryId, out IndustryDefinition? definition) ||
                definition == null)
            {
                return;
            }

            industry.EnsureMonthStats(calendarDate);
            TryProduce(industry, definition);
        }

        /// <summary>
        /// Consumes inputs when available, credits Produced for full recipe output, and stocks
        /// what fits. Excess output is wasted; level 2/3 still consume inputs when wasting.
        /// </summary>
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

            for (int inputIndex = 0; inputIndex < definition.Inputs.Count; inputIndex++)
            {
                CargoAmountDefinition input = definition.Inputs[inputIndex];
                industry.Cargo.TryRemove(input.CargoId, input.Amount);
            }

            long producedUnits = 0L;
            for (int outputIndex = 0; outputIndex < definition.Outputs.Count; outputIndex++)
            {
                CargoAmountDefinition output = definition.Outputs[outputIndex];
                industry.TryAddCargo(output.CargoId, output.Amount);
                producedUnits = checked(producedUnits + output.Amount);
            }

            industry.CreditMonthProduced(producedUnits);
        }
    }
}
