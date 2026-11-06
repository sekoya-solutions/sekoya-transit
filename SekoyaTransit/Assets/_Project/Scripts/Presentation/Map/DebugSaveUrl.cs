#nullable enable
using System;
using System.Text.RegularExpressions;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Reads the <c>?save=&lt;file&gt;</c> parameter that Development WebGL builds load at start (Issue #320), so an
    /// agent can open a save file served next to the build in a browser that cannot pick files. Only a plain file name
    /// ending in <c>.sav</c> is accepted, so the file always comes from the page's own folder.
    /// </summary>
    public static class DebugSaveUrl
    {
        /// <summary>
        /// Name of the query parameter.
        /// </summary>
        public const string Parameter = "save";

        private static readonly Regex FileNamePattern = new Regex(
            "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}\\.sav$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Gets the save file named by the page URL's first <see cref="Parameter"/> parameter; false when the URL has
        /// none or names anything but a plain <c>.sav</c> file name (a path, another extension, a bad escape).
        /// </summary>
        public static bool TryGetFileName(string? pageUrl, out string fileName)
        {
            fileName = string.Empty;
            if (string.IsNullOrEmpty(pageUrl) || !Uri.TryCreate(pageUrl, UriKind.Absolute, out Uri? uri))
            {
                return false;
            }

            string query = uri.Query;
            if (query.Length <= 1)
            {
                return false;
            }

            string[] pairs = query.Substring(1).Split('&');
            for (int index = 0; index < pairs.Length; index++)
            {
                string pair = pairs[index];
                int equals = pair.IndexOf('=');
                string key = equals < 0 ? pair : pair.Substring(0, equals);
                if (!string.Equals(key, Parameter, StringComparison.Ordinal))
                {
                    continue;
                }

                string value;
                try
                {
                    value = equals < 0 ? string.Empty : Uri.UnescapeDataString(pair.Substring(equals + 1));
                }
                catch (UriFormatException)
                {
                    return false;
                }

                if (!FileNamePattern.IsMatch(value) || value.Contains(".."))
                {
                    return false;
                }

                fileName = value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the address of <paramref name="fileName"/> in the folder of the page at <paramref name="pageUrl"/>.
        /// </summary>
        public static string ResolveFileUrl(string pageUrl, string fileName)
        {
            if (pageUrl is null)
            {
                throw new ArgumentNullException(nameof(pageUrl));
            }

            if (fileName is null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            return new Uri(new Uri(pageUrl, UriKind.Absolute), fileName).AbsoluteUri;
        }
    }
}
