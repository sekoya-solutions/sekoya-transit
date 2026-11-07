#nullable enable
using System;
using System.Globalization;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Formats a game date for the player in the selected language (Issue #338): <c>DD/MM/YYYY</c> in French,
    /// <c>YYYY-MM-DD</c> in English and any other language.
    /// </summary>
    /// <remarks>
    /// The format is chosen by language code, not from the culture's short date pattern, so English keeps its
    /// year-month-day dates instead of the United States <c>M/d/yyyy</c> pattern, and every French culture gets the
    /// same format whatever its regional pattern.
    /// </remarks>
    public static class GameDateText
    {
        /// <summary>
        /// Returns the text of <paramref name="date"/> for <paramref name="culture"/>.
        /// </summary>
        public static string Format(GameDate date, CultureInfo culture)
        {
            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            if (!IsFrench(culture))
            {
                return date.ToString();
            }

            return date.Day.ToString("D2", CultureInfo.InvariantCulture)
                + "/"
                + date.Month.ToString("D2", CultureInfo.InvariantCulture)
                + "/"
                + date.Year.ToString("D4", CultureInfo.InvariantCulture);
        }

        private static bool IsFrench(CultureInfo culture)
        {
            string name = culture.Name;
            return string.Equals(name, ShellLanguages.French, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(ShellLanguages.French + "-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
