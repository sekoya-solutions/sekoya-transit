#nullable enable

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Serializes snapshots to and from their compressed persistent representation.
    /// </summary>
    public interface ISaveSerializer
    {
        /// <summary>
        /// Serializes a current-version snapshot into compressed bytes.
        /// </summary>
        byte[] Serialize(SaveSnapshot snapshot);

        /// <summary>
        /// Deserializes and migrates compressed bytes to the current snapshot version.
        /// </summary>
        SaveSnapshot Deserialize(byte[] compressedData);
    }
}
