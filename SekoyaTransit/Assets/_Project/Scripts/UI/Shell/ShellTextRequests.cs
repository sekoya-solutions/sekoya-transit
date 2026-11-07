#nullable enable
using System;
using UnityEngine.Localization;
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

        /// <summary>
        /// Resolves <paramref name="key"/> in the locale selected when the lookup finishes (Issue #338). A lookup
        /// started in one locale can finish after the player picked another one (a table that is still loading finishes
        /// after a table already loaded), so a lookup whose locale is no longer selected is started again in the current
        /// locale instead of applying an old-language text over a newer one. <paramref name="completed"/> runs exactly
        /// once: with the text, or with null when the lookup fails or a restarted lookup cannot start.
        /// </summary>
        /// <param name="key">The key to resolve.</param>
        /// <param name="currentLocale">Gets the selected locale, or null while it is not known yet.</param>
        /// <param name="lookup">Starts resolving a key in a locale and calls its callback once with the text or null.</param>
        /// <param name="completed">Receives the result.</param>
        public static void RequestInCurrentLocale<TLocale>(
            string key,
            Func<TLocale?> currentLocale,
            Action<string, TLocale?, Action<string?>> lookup,
            Action<string?> completed)
            where TLocale : class
        {
            if (currentLocale is null)
            {
                throw new ArgumentNullException(nameof(currentLocale));
            }

            if (lookup is null)
            {
                throw new ArgumentNullException(nameof(lookup));
            }

            if (completed is null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            TLocale? locale = currentLocale();
            lookup(key, locale, text =>
            {
                if (ReferenceEquals(currentLocale(), locale))
                {
                    completed(text);
                    return;
                }

                try
                {
                    RequestInCurrentLocale(key, currentLocale, lookup, completed);
                }
                catch
                {
                    // The restarted lookup could not start, so its callback will not run: finish the request here so
                    // it never stays pending.
                    completed(null);
                }
            });
        }

        private static void RequestFromLocalization(string key, Action<string?> completed)
        {
            RequestInCurrentLocale<Locale>(key, SelectedLocaleIfKnown, LookUp, completed);
        }

        // SelectedLocale would wait synchronously while the locales load, which WebGL does not support.
        private static Locale? SelectedLocaleIfKnown()
        {
            AsyncOperationHandle<Locale> selected = LocalizationSettings.SelectedLocaleAsync;
            return selected.IsValid() && selected.IsDone ? selected.Result : null;
        }

        private static void LookUp(string key, Locale? locale, Action<string?> completed)
        {
            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(GameUiKeys.Table, key, locale);
            operation.Completed += handle =>
                completed(handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null);
        }
    }
}
