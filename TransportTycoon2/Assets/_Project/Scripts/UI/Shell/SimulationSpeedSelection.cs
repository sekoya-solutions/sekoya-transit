#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Provides the supported MapPlay speed choices and forwards selections to the simulation owner.
    /// </summary>
    public sealed class SimulationSpeedSelection
    {
        private readonly Func<int> _speedProvider;
        private readonly Action<int> _onSpeedSelected;

        /// <summary>
        /// Initializes speed selection with callbacks owned by Presentation.
        /// </summary>
        public SimulationSpeedSelection(Func<int> speedProvider, Action<int> onSpeedSelected)
        {
            _speedProvider = speedProvider ?? throw new ArgumentNullException(nameof(speedProvider));
            _onSpeedSelected = onSpeedSelected ?? throw new ArgumentNullException(nameof(onSpeedSelected));
        }

        /// <summary>
        /// Gets the currently selected simulation speed.
        /// </summary>
        public int CurrentMultiplier
        {
            get
            {
                return _speedProvider();
            }
        }

        /// <summary>
        /// Returns whether the supplied speed is currently selected.
        /// </summary>
        public bool IsSelected(int multiplier)
        {
            return CurrentMultiplier == multiplier;
        }

        /// <summary>
        /// Applies one of the supported MapPlay speed multipliers.
        /// </summary>
        public void Select(int multiplier)
        {
            if (multiplier < 1 || multiplier > 3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(multiplier),
                    multiplier,
                    "MapPlay supports speed multipliers 1, 2, and 3.");
            }

            _onSpeedSelected(multiplier);
        }
    }
}
