#nullable enable
using System;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The single path through which the shell resolves localized texts. It counts the requests still running so
    /// the shell can tell when its load-time texts are ready instead of guessing from elapsed time.
    /// </summary>
    public sealed class ShellTextRequests
    {
        private readonly Action<string, Action<string?>> _source;

        /// <summary>
        /// Initializes the requests over a text source.
        /// </summary>
        /// <param name="source">
        /// Starts resolving a key and calls the given callback exactly once with the text, or with null on failure.
        /// </param>
        public ShellTextRequests(Action<string, Action<string?>> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// Gets the number of requests that have not finished yet.
        /// </summary>
        public int Pending { get; private set; }

        /// <summary>
        /// Creates requests resolved from the GameUi string table.
        /// </summary>
        public static ShellTextRequests ForLocalization()
        {
            return new ShellTextRequests(RequestFromLocalization);
        }

        /// <summary>
        /// Starts resolving a key. <paramref name="apply"/> runs on success with a non-empty text. The request counts
        /// as finished whatever the outcome, even if <paramref name="apply"/> throws, so it can never block the shell.
        /// </summary>
        public void Request(string key, Action<string> apply)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));
            }

            if (apply is null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            Pending++;
            bool callbackRan = false;
            try
            {
                _source(key, text =>
                {
                    callbackRan = true;
                    try
                    {
                        if (!string.IsNullOrEmpty(text))
                        {
                            apply(text!);
                        }
                    }
                    finally
                    {
                        Pending--;
                    }
                });
            }
            catch
            {
                // The source failed to start the request, so its callback will not run.
                if (!callbackRan)
                {
                    Pending--;
                }

                throw;
            }
        }

        private static void RequestFromLocalization(string key, Action<string?> completed)
        {
            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(GameUiKeys.Table, key);
            operation.Completed += handle =>
                completed(handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null);
        }
    }
}
