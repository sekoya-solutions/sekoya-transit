#nullable enable
using System;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The languages the game ships (ADR 0011, Issue #338): their locale codes, in the order of the language chooser.
    /// </summary>
    public static class ShellLanguages
    {
        /// <summary>
        /// English, also the language shown when no stored choice and no system language selects a shipped one.
        /// </summary>
        public const string English = "en";

        /// <summary>
        /// French.
        /// </summary>
        public const string French = "fr";

        /// <summary>
        /// Key of the English language name, written in English in every table.
        /// </summary>
        public const string EnglishNameKey = "language.en";

        /// <summary>
        /// Key of the French language name, written in French in every table.
        /// </summary>
        public const string FrenchNameKey = "language.fr";

        private static readonly string[] ShippedCodes = { English, French };

        /// <summary>
        /// Gets a copy of the shipped locale codes, in chooser order.
        /// </summary>
        public static string[] Shipped()
        {
            return (string[])ShippedCodes.Clone();
        }

        /// <summary>
        /// Returns whether <paramref name="code"/> is a shipped locale code (exact, case-sensitive match).
        /// </summary>
        public static bool IsShipped(string? code)
        {
            return Array.IndexOf(ShippedCodes, code) >= 0;
        }

        /// <summary>
        /// Gets the key of a shipped language's name, as shown on its chooser button.
        /// </summary>
        public static string NameKey(string code)
        {
            switch (code)
            {
                case English:
                    return EnglishNameKey;
                case French:
                    return FrenchNameKey;
                default:
                    throw new ArgumentOutOfRangeException(nameof(code), code, "Not a shipped language.");
            }
        }
    }
}
