#nullable enable
using System;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Deterministic periodic passenger and mail generation onto town waiting stock (Issue #59).
    /// </summary>
    public static class TownPassengerGeneration
    {
        /// <summary>
        /// Cargo id for passengers.
        /// </summary>
        public const string PassengersCargoId = "passengers";

        /// <summary>
        /// Cargo id for mail.
        /// </summary>
        public const string MailCargoId = "mail";

        /// <summary>
        /// Simulation ticks between generation pulses (same cadence as industry production).
        /// </summary>
        public const int GenerationIntervalTicks = IndustryProduction.ProductionIntervalTicks;

        /// <summary>
        /// Runs generation for every town when <paramref name="tick"/> hits the interval.
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

            if (tick.Value <= 0 || tick.Value % GenerationIntervalTicks != 0)
            {
                return;
            }

            TownNeedsSettings settings = catalog.TownNeeds;
            for (int index = 0; index < world.Towns.Count; index++)
            {
                AdvanceTown(world.Towns[index], settings);
            }
        }

        /// <summary>
        /// Computes passengers generated for one pulse from <paramref name="population"/>.
        /// </summary>
        public static int ComputePassengersGenerated(int population, TownNeedsSettings settings)
        {
#pragma warning disable CA1510
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
#pragma warning restore CA1510

            if (population <= 0)
            {
                return 0;
            }

            int amount = population / settings.PassengersPerPopulationUnit;
            return amount < 1 ? 1 : amount;
        }

        /// <summary>
        /// Computes mail generated for one pulse from <paramref name="population"/>.
        /// </summary>
        public static int ComputeMailGenerated(int population, TownNeedsSettings settings)
        {
#pragma warning disable CA1510
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
#pragma warning restore CA1510

            if (population <= 0)
            {
                return 0;
            }

            return population / settings.MailPerPopulationUnit;
        }

        private static void AdvanceTown(Town town, TownNeedsSettings settings)
        {
            int capacity = settings.PassengerWaitingCapacity;
            int passengers = ComputePassengersGenerated(town.Population, settings);
            if (passengers > 0)
            {
                town.CreditMonthPassengersGenerated(passengers);
                _ = town.TryAddWaitingCargo(PassengersCargoId, passengers, capacity);
            }

            int mail = ComputeMailGenerated(town.Population, settings);
            if (mail > 0)
            {
                town.CreditMonthMailGenerated(mail);
                _ = town.TryAddWaitingCargo(MailCargoId, mail, capacity);
            }
        }
    }
}
