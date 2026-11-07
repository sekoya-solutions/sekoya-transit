#nullable enable
using System;
using System.Globalization;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Formats a calendar date only when the day or the language changes, so the per-frame date label refresh
    /// allocates no string while the date stands still (Issue #70; language, Issue #338).
    /// </summary>
    public sealed class GameDateTextCache
    {
        private GameDate _date;
        private CultureInfo? _culture;
        private string? _text;

        /// <summary>
        /// Returns the text of <paramref name="date"/> in <paramref name="culture"/>, reusing the previous string when
        /// neither changed.
        /// </summary>
        public string Get(GameDate date, CultureInfo culture)
        {
            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            if (_text == null || date != _date || !ReferenceEquals(culture, _culture))
            {
                _date = date;
                _culture = culture;
                _text = GameDateText.Format(date, culture);
            }

            return _text;
        }
    }
}
