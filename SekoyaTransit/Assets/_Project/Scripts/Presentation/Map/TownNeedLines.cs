#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Builds the towns panel's need lines from the simulation, so a line's color follows the same rule as the growth
    /// (Issue #310).
    /// </summary>
    public static class TownNeedLines
    {
        /// <summary>
        /// Returns the panel line of a town need: last month's supply, the demand of its level, and its status.
        /// </summary>
        public static TownNeedListLine Create(TownNeed need, ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (need == null)
            {
                throw new ArgumentNullException(nameof(need));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            string cargoNameKey = need.CargoId;
            int level = TownNeedListLine.MinLevel;
            if (catalog.TryGetCargo(need.CargoId, out CargoDefinition? cargo) && cargo != null)
            {
                cargoNameKey = cargo.NameKey;
                level = Math.Clamp(cargo.ManufacturingLevel, TownNeedListLine.MinLevel, TownNeedListLine.MaxLevel);
            }

            TownNeedsSettings settings = catalog.TownNeeds;
            int demand = settings.DemandPerNeedPerMonth.Get(level);
            TownNeedStatus status = TownGrowth.GetStatus(need.LastMonthSupply, demand, settings.PartlyMetMinDemandPercent);
            return new TownNeedListLine(need.CargoId, cargoNameKey, level, need.LastMonthSupply, demand, ToLineStatus(status));
        }

        private static TownNeedLineStatus ToLineStatus(TownNeedStatus status)
        {
            switch (status)
            {
                case TownNeedStatus.Unmet:
                    return TownNeedLineStatus.Unmet;
                case TownNeedStatus.PartlyMet:
                    return TownNeedLineStatus.PartlyMet;
                case TownNeedStatus.FullyMet:
                    return TownNeedLineStatus.FullyMet;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown town need status.");
            }
        }
    }
}
