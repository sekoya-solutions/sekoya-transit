#nullable enable
using System;
using System.Threading.Tasks;

namespace SekoyaTransit.Save
{
    /// <summary>
    /// The file transfer of platforms without it (Windows player and Editor, Issue #320): unavailable, every call fails.
    /// </summary>
    public sealed class UnavailableSaveFileTransfer : ISaveFileTransfer
    {
        /// <inheritdoc />
        public bool IsAvailable => false;

        /// <inheritdoc />
        public void Download(string fileName, byte[] data)
        {
            throw new NotSupportedException("Save file download exists only in WebGL players.");
        }

        /// <inheritdoc />
        public Task<byte[]?> PickFileAsync()
        {
            throw new NotSupportedException("Load from file exists only in WebGL players.");
        }
    }
}
