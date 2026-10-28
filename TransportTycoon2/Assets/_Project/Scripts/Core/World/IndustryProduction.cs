#nullable enable
using System;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic industry production onto industry stock (Issue #132, Issue #193). Primary industries produce on
    /// a fixed pulse; processing industries produce when their inputs are delivered. Outputs stay on the industry until
    /// a vehicle loads; no station cargo transfer.
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

            // Processing industries produce when their inputs are delivered (TryDeliverInput), not on the pulse.
            if (definition.Inputs.Count > 0)
            {
                return;
            }

            industry.EnsureMonthStats(calendarDate);
            ProduceRecipes(industry, definition, 1);
        }

        /// <summary>
        /// Takes delivered units of one recipe input (Issue #193). Every complete recipe is turned into output at once;
        /// output above the stock cap is lost. Units that do not complete a recipe stay in the hidden
        /// <see cref="PlacedIndustry.InputLeftover"/>, capped at one recipe's worth per input (the rest is lost).
        /// </summary>
        /// <returns><see langword="false"/> (nothing taken) when the cargo is not an input of the industry.</returns>
        public static bool TryDeliverInput(
            PlacedIndustry industry,
            IndustryDefinition definition,
            string cargoId,
            int amount,
            GameDate? calendarDate)
        {
#pragma warning disable CA1510
            if (industry == null)
            {
                throw new ArgumentNullException(nameof(industry));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
#pragma warning restore CA1510

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Delivered amount must be positive.");
            }

            if (!IsInput(definition, cargoId))
            {
                return false;
            }

            if (calendarDate.HasValue)
            {
                industry.EnsureMonthStats(calendarDate.Value);
            }

            industry.InputLeftover.Add(cargoId, amount);
            int recipes = int.MaxValue;
            for (int inputIndex = 0; inputIndex < definition.Inputs.Count; inputIndex++)
            {
                CargoAmountDefinition input = definition.Inputs[inputIndex];
                int available = industry.InputLeftover.GetAmount(input.CargoId) / input.Amount;
                recipes = available < recipes ? available : recipes;
            }

            for (int inputIndex = 0; inputIndex < definition.Inputs.Count; inputIndex++)
            {
                CargoAmountDefinition input = definition.Inputs[inputIndex];
                if (recipes > 0)
                {
                    industry.InputLeftover.TryRemove(input.CargoId, checked(recipes * input.Amount));
                }

                int excess = industry.InputLeftover.GetAmount(input.CargoId) - input.Amount;
                if (excess > 0)
                {
                    industry.InputLeftover.TryRemove(input.CargoId, excess);
                }
            }

            ProduceRecipes(industry, definition, recipes);
            return true;
        }

        /// <summary>
        /// Returns whether <paramref name="cargoId"/> is one of the definition's recipe inputs.
        /// </summary>
        public static bool IsInput(IndustryDefinition definition, string cargoId)
        {
#pragma warning disable CA1510
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
#pragma warning restore CA1510

            for (int inputIndex = 0; inputIndex < definition.Inputs.Count; inputIndex++)
            {
                if (string.Equals(definition.Inputs[inputIndex].CargoId, cargoId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Credits Produced for the full output of <paramref name="recipes"/> recipes and stocks what fits; excess
        /// output is lost.
        /// </summary>
        private static void ProduceRecipes(PlacedIndustry industry, IndustryDefinition definition, int recipes)
        {
            if (recipes <= 0)
            {
                return;
            }

            long producedUnits = 0L;
            for (int outputIndex = 0; outputIndex < definition.Outputs.Count; outputIndex++)
            {
                CargoAmountDefinition output = definition.Outputs[outputIndex];
                int units = checked(recipes * output.Amount);
                industry.TryAddCargo(output.CargoId, units);
                producedUnits = checked(producedUnits + units);
            }

            industry.CreditMonthProduced(producedUnits);
        }
    }
}
