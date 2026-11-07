#nullable enable

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The language the player picked in the chooser, kept outside the save file so it applies at every launch
    /// (Issue #338).
    /// </summary>
    public interface ILanguagePreference
    {
        /// <summary>
        /// Gets the stored locale code, or null when the player never picked a language.
        /// </summary>
        string? Load();

        /// <summary>
        /// Stores <paramref name="code"/> and writes it to disk at once.
        /// </summary>
        void Store(string code);
    }
}
