#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// The town or industry row selected in the towns and industries lists (Issue #178). At most one of the two is
    /// selected at a time, so selecting one clears the other.
    /// </summary>
    public sealed class ListSelection
    {
        /// <summary>
        /// Gets the selected town id, or null.
        /// </summary>
        public int? TownId { get; private set; }

        /// <summary>
        /// Gets the selected industry content id, or null.
        /// </summary>
        public string? IndustryId { get; private set; }

        /// <summary>
        /// Gets the selected industry instance index (0 when no industry is selected).
        /// </summary>
        public int IndustryInstance { get; private set; }

        /// <summary>
        /// Gets whether an industry row is selected.
        /// </summary>
        public bool HasIndustry => IndustryId != null;

        /// <summary>
        /// Selects a town and clears the industry selection.
        /// </summary>
        public void SelectTown(int townId)
        {
            TownId = townId;
            IndustryId = null;
            IndustryInstance = 0;
        }

        /// <summary>
        /// Selects an industry instance and clears the town selection.
        /// </summary>
        public void SelectIndustry(string industryId, int instanceIndex)
        {
            IndustryId = industryId ?? throw new ArgumentNullException(nameof(industryId));
            IndustryInstance = instanceIndex;
            TownId = null;
        }

        /// <summary>
        /// Clears both selections.
        /// </summary>
        public void Clear()
        {
            TownId = null;
            IndustryId = null;
            IndustryInstance = 0;
        }

        /// <summary>
        /// Returns whether the given town is the selected one.
        /// </summary>
        public bool IsTownSelected(int townId)
        {
            return TownId.HasValue && TownId.Value == townId;
        }

        /// <summary>
        /// Returns whether the given industry instance is the selected one.
        /// </summary>
        public bool IsIndustrySelected(string industryId, int instanceIndex)
        {
            return IndustryId != null
                && string.Equals(IndustryId, industryId, StringComparison.Ordinal)
                && IndustryInstance == instanceIndex;
        }
    }
}
