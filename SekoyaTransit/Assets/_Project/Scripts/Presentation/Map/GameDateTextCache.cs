#nullable enable
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Formats a calendar date only when the day changes, so the per-frame date label refresh allocates no string
    /// while the date stands still (Issue #70).
    /// </summary>
    public sealed class GameDateTextCache
    {
        private GameDate _date;
        private string? _text;

        /// <summary>
        /// Returns the text of <paramref name="date"/>, reusing the previous string when the date did not change.
        /// </summary>
        public string Get(GameDate date)
        {
            if (_text == null || date != _date)
            {
                _date = date;
                _text = date.ToString();
            }

            return _text;
        }
    }
}
