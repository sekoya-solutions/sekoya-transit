#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

// Minimal stand-ins for the two UnityEngine types the content loader and the save writer use, so the shipped code runs
// unchanged.
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

        /// <summary>
        /// Writes a document the way Unity's <c>JsonUtility.ToJson</c> does (Issue #352), so a save written by this
        /// tool is the file the game would write: serialized fields in declaration order, no whitespace, a null string
        /// as <c>""</c>, a null array as <c>[]</c> and a null nested object as its default field values. It supports
        /// the field types the save document uses (bool, int, long, string, arrays and serializable classes) and
        /// throws on any other, so a new field type cannot be written differently without notice. The save check in
        /// the balance tool's README compares the result with Unity's own output byte for byte.
        /// </summary>
        public static string ToJson(object document)
        {
            ArgumentNullException.ThrowIfNull(document);

            var json = new StringBuilder();
            WriteObject(json, document, document.GetType());
            return json.ToString();
        }

        private static void WriteObject(StringBuilder json, object? value, Type type)
        {
            // Unity has no null for a serializable class field: it writes the class with its default values.
            value ??= Activator.CreateInstance(type, nonPublic: true)
                ?? throw new ArgumentException($"{type.Name} has no parameterless constructor.");
            json.Append('{');
            bool first = true;
            foreach (FieldInfo field in SerializedFields(type))
            {
                if (!first)
                {
                    json.Append(',');
                }

                first = false;
                WriteString(json, field.Name);
                json.Append(':');
                WriteValue(json, field.GetValue(value), field.FieldType);
            }

            json.Append('}');
        }

        private static void WriteValue(StringBuilder json, object? value, Type type)
        {
            if (type == typeof(bool))
            {
                json.Append((bool)value! ? "true" : "false");
            }
            else if (type == typeof(int))
            {
                json.Append(((int)value!).ToString(CultureInfo.InvariantCulture));
            }
            else if (type == typeof(long))
            {
                json.Append(((long)value!).ToString(CultureInfo.InvariantCulture));
            }
            else if (type == typeof(string))
            {
                WriteString(json, (string?)value ?? string.Empty);
            }
            else if (type.IsArray)
            {
                Type element = type.GetElementType()!;
                json.Append('[');
                if (value is IList items)
                {
                    for (int index = 0; index < items.Count; index++)
                    {
                        if (index > 0)
                        {
                            json.Append(',');
                        }

                        WriteValue(json, items[index], element);
                    }
                }

                json.Append(']');
            }
            else if (type.IsClass && type.IsDefined(typeof(SerializableAttribute), false))
            {
                WriteObject(json, value, type);
            }
            else
            {
                throw new ArgumentException($"The save writer stand-in does not support fields of type {type.Name}.");
            }
        }

        // Unity serializes public instance fields that are neither readonly, constant nor marked NonSerialized, in
        // declaration order (metadata order); the save document has no private [SerializeField] fields.
        private static FieldInfo[] SerializedFields(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsInitOnly && !field.IsLiteral && !field.IsDefined(typeof(NonSerializedAttribute), false))
                .OrderBy(field => field.MetadataToken)
                .ToArray();
        }

        private static void WriteString(StringBuilder json, string text)
        {
            json.Append('"');
            foreach (char character in text)
            {
                switch (character)
                {
                    case '"':
                        json.Append("\\\"");
                        break;
                    case '\\':
                        json.Append("\\\\");
                        break;
                    case '\n':
                        json.Append("\\n");
                        break;
                    case '\r':
                        json.Append("\\r");
                        break;
                    case '\t':
                        json.Append("\\t");
                        break;
                    case '\b':
                        json.Append("\\b");
                        break;
                    case '\f':
                        json.Append("\\f");
                        break;
                    default:
                        if (character < ' ')
                        {
                            json.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            json.Append(character);
                        }

                        break;
                }
            }

            json.Append('"');
        }
    }
}
