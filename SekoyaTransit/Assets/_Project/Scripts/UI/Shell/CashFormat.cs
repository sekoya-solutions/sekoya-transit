#nullable enable
using System;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Culture-aware formatting for company cash display (ADR 0011).
    /// </summary>
    public static class CashFormat
    {
        /// <summary>
        /// Formats <paramref name="amount"/> with grouping separators for <paramref name="culture"/>.
        /// </summary>
        public static string FormatAmount(long amount, CultureInfo culture)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            return amount.ToString("N0", culture);
        }

        /// <summary>
        /// Applies a localized template such as <c>Cash: {0}</c> with a culture-aware amount.
        /// </summary>
        public static string FormatLabeled(string template, long amount, CultureInfo culture)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            return string.Format(culture, template, FormatAmount(amount, culture));
        }
    }
}
