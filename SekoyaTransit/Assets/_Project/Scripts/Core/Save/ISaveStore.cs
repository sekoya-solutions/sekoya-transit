#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Stores compressed save payloads without exposing a platform-specific backend.
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>
        /// Writes or replaces a save slot.
        /// </summary>
        Task SaveAsync(SaveSlot slot, byte[] compressedData);

        /// <summary>
        /// Reads a save slot or fails if it does not exist.
        /// </summary>
        Task<byte[]> LoadAsync(SaveSlot slot);

        /// <summary>
        /// Lists the available autosave and manual slots.
        /// </summary>
        Task<IReadOnlyList<SaveSlot>> ListAsync();

        /// <summary>
        /// Deletes a save slot or fails if it does not exist.
        /// </summary>
        Task DeleteAsync(SaveSlot slot);
    }
}
