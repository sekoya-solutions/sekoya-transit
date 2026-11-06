#nullable enable
using System.Threading.Tasks;

namespace SekoyaTransit.Save
{
    /// <summary>
    /// Moves save files between the game and the player's own files (Issue #320): the Debug download and Load from
    /// file. Only WebGL players have it; elsewhere <see cref="IsAvailable"/> is false.
    /// </summary>
    public interface ISaveFileTransfer
    {
        /// <summary>
        /// Gets whether this platform can download and pick files.
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Hands <paramref name="data"/> to the browser as a download named <paramref name="fileName"/>. Throws an
        /// <see cref="System.IO.IOException"/> when the browser refused it.
        /// </summary>
        void Download(string fileName, byte[] data);

        /// <summary>
        /// Opens the file picker and returns the chosen file's bytes, or null when the player cancelled. Fails with an
        /// <see cref="System.IO.IOException"/> when the file could not be opened or read. Starting another pick ends the
        /// previous one as cancelled. On a browser without the picker's cancel event, a cancelled pick only completes
        /// when the next pick starts; it holds nothing meanwhile.
        /// </summary>
        Task<byte[]?> PickFileAsync();
    }
}
