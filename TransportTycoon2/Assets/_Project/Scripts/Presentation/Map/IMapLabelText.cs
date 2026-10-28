#nullable enable

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Localized text for world-space map labels (Issue #193): industry names come from the UI string tables.
    /// </summary>
    public interface IMapLabelText
    {
        /// <summary>
        /// Gets a counter that changes whenever any loaded text changes, so labels know to rebuild.
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Gets the localized text of a key, or the key itself while it is not loaded.
        /// </summary>
        string Get(string key);
    }
}
