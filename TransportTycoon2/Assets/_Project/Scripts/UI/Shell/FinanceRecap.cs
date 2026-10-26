#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// The finance panel data: this year and last year (Issue #66).
    /// </summary>
    public sealed class FinanceRecap
    {
        /// <summary>
        /// Initializes the recap.
        /// </summary>
        public FinanceRecap(FinanceYearRecap current, FinanceYearRecap previous)
        {
            Current = current ?? throw new ArgumentNullException(nameof(current));
            Previous = previous ?? throw new ArgumentNullException(nameof(previous));
        }

        /// <summary>
        /// Gets the current calendar year.
        /// </summary>
        public FinanceYearRecap Current { get; }

        /// <summary>
        /// Gets the previous calendar year.
        /// </summary>
        public FinanceYearRecap Previous { get; }
    }
}
