#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Resolves localization keys of the GameUi table asynchronously into a <see cref="ShellTextCache"/>.
    /// </summary>
    public sealed class ShellTextLoader
    {
        private readonly ShellTextCache _text;
        private readonly ShellTextRequests _requests;
        private readonly HashSet<string> _requested = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Initializes a loader that fills <paramref name="text"/> through <paramref name="requests"/>.
        /// </summary>
        public ShellTextLoader(ShellTextCache text, ShellTextRequests requests)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _requests = requests ?? throw new ArgumentNullException(nameof(requests));
        }

        /// <summary>
        /// Forgets which keys were requested so the next <see cref="Request"/> resolves them again (locale change).
        /// </summary>
        public void Reset()
        {
            _requested.Clear();
        }

        /// <summary>
        /// Starts resolving every key that was not requested since the last reset. Empty results are ignored.
        /// </summary>
        public void Request(IEnumerable<string> keys)
        {
            foreach (string key in keys)
            {
                if (!_requested.Add(key))
                {
                    continue;
                }

                string captured = key;
                _requests.Request(captured, value => _text.Set(captured, value));
            }
        }
    }
}
