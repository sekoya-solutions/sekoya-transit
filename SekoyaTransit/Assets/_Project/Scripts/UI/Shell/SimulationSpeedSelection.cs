#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Provides the supported MapPlay speed choices and the pause toggle, and forwards them to the simulation owner.
    /// </summary>
    public sealed class SimulationSpeedSelection
    {
        private readonly Func<int> _speedProvider;
        private readonly Action<int> _onSpeedSelected;
        private readonly Func<bool> _pausedProvider;
        private readonly Action<bool> _onPausedChanged;

        /// <summary>
        /// Initializes speed selection with callbacks owned by Presentation.
        /// </summary>
        public SimulationSpeedSelection(
            Func<int> speedProvider,
            Action<int> onSpeedSelected,
            Func<bool> pausedProvider,
            Action<bool> onPausedChanged)
        {
            _speedProvider = speedProvider ?? throw new ArgumentNullException(nameof(speedProvider));
            _onSpeedSelected = onSpeedSelected ?? throw new ArgumentNullException(nameof(onSpeedSelected));
            _pausedProvider = pausedProvider ?? throw new ArgumentNullException(nameof(pausedProvider));
            _onPausedChanged = onPausedChanged ?? throw new ArgumentNullException(nameof(onPausedChanged));
        }

        /// <summary>
        /// Gets the currently selected simulation speed (the speed a pause resumes at).
        /// </summary>
        public int CurrentMultiplier
        {
            get
            {
                return _speedProvider();
            }
        }

        /// <summary>
        /// Gets whether the simulation is paused. Building, buying vehicles and route planning stay available.
        /// </summary>
        public bool IsPaused
        {
            get
            {
                return _pausedProvider();
            }
        }

        /// <summary>
        /// Returns whether the supplied speed is currently selected; no speed is selected while paused.
        /// </summary>
        public bool IsSelected(int multiplier)
        {
            return !IsPaused && CurrentMultiplier == multiplier;
        }

        /// <summary>
        /// Pauses a running simulation or resumes a paused one at the remembered speed.
        /// </summary>
        public void TogglePause()
        {
            _onPausedChanged(!IsPaused);
        }

        /// <summary>
        /// Applies one of the supported MapPlay speed multipliers and resumes a paused simulation.
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
            if (IsPaused)
            {
                _onPausedChanged(false);
            }
        }
    }
}
