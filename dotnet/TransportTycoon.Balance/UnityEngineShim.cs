#nullable enable
using System.Text.Json;

// Minimal stand-ins for the two UnityEngine types the content loader uses, so the shipped loader runs unchanged.
namespace UnityEngine
{
    /// <summary>
    /// Text content of a JSON file, as Unity's TextAsset exposes it.
    /// </summary>
    public sealed class TextAsset
    {
        /// <summary>
        /// Initializes a text asset.
        /// </summary>
        public TextAsset(string name, string text)
        {
            this.name = name;
            this.text = text;
        }

#pragma warning disable CA1051, IDE1006 // Mirrors Unity's lower-case public members.
        /// <summary>
        /// Gets the asset name.
        /// </summary>
        public readonly string name;

        /// <summary>
        /// Gets the file text.
        /// </summary>
        public readonly string text;
#pragma warning restore CA1051, IDE1006
    }

    /// <summary>
    /// Field-based JSON parsing like Unity's JsonUtility.
    /// </summary>
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Parses a document.
        /// </summary>
        public static T? FromJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, _options);
        }
    }
}
