#nullable enable
using System;
using System.Globalization;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Formats the current calendar date with the localized MapPlay label.
    /// </summary>
    public static class CalendarDateFormat
    {
        /// <summary>
        /// Applies a localized date label template to an already formatted calendar date.
        /// </summary>
        public static string FormatLabeled(string template, string date, CultureInfo culture)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (date == null)
            {
                throw new ArgumentNullException(nameof(date));
            }

            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            return string.Format(culture, template, date);
        }
    }
}
