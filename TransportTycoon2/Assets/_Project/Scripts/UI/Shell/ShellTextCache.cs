#nullable enable
using System;
using System.Collections.Generic;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Localized strings already resolved by the shell, readable synchronously by view-models. A missing key
    /// resolves to the key itself. <see cref="Version"/> changes whenever a value changes so panels can rebuild.
    /// </summary>
    public sealed class ShellTextCache
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets a counter that increases whenever a stored value changes.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Stores the localized text of a key.
        /// </summary>
        public void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            }

            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (_values.TryGetValue(key, out string? existing) && string.Equals(existing, value, StringComparison.Ordinal))
            {
                return;
            }

            _values[key] = value;
            Version++;
        }

        /// <summary>
        /// Returns the localized text, or the key when it has not been resolved yet.
        /// </summary>
        public string Get(string key)
        {
            return _values.TryGetValue(key, out string? value) ? value : key;
        }
    }
}
