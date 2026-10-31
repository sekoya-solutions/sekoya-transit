#nullable enable

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Reports a save written in a format older than <see cref="SaveSnapshot.MinimumLoadableFormatVersion"/>, which
    /// the game no longer loads (Issue #229).
    /// </summary>
    public sealed class SaveFormatTooOldException : SaveFormatException
    {
        /// <summary>
        /// Initializes the exception for a save at <paramref name="formatVersion"/>.
        /// </summary>
        public SaveFormatTooOldException(int formatVersion, int minimumVersion)
            : base($"Save format version {formatVersion} is older than the oldest supported version {minimumVersion}.")
        {
            FormatVersion = formatVersion;
        }

        /// <summary>
        /// Gets the format version of the refused save.
        /// </summary>
        public int FormatVersion { get; }
    }
}
