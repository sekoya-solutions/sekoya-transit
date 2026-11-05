#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SekoyaTransit.Balance
{
    /// <summary>
    /// Adds an object graph to a hash by walking every instance field, public or private, so nothing a save records can
    /// be left out by accident. Collections hash their elements in order, not their internal fields, so only their
    /// content counts. Used to prove that a Core change leaves the game unchanged (Issue #308).
    /// </summary>
    internal static class StateHasher
    {
        private const BindingFlags _instanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly ConcurrentDictionary<Type, FieldInfo[]> _fieldsByType = new ConcurrentDictionary<Type, FieldInfo[]>();

        /// <summary>
        /// Appends <paramref name="root"/> and everything it references to <paramref name="hash"/>.
        /// </summary>
        public static void Append(IncrementalHash hash, object root)
        {
            using var buffer = new MemoryStream();
            using var writer = new BinaryWriter(buffer);
            Write(writer, root, new HashSet<object>(ReferenceEqualityComparer.Instance));
            writer.Flush();
            hash.AppendData(buffer.GetBuffer(), 0, (int)buffer.Length);
        }

        private static void Write(BinaryWriter writer, object? value, HashSet<object> visiting)
        {
            switch (value)
            {
                case null:
                    writer.Write((byte)0);
                    return;
                case string text:
                    writer.Write(text);
                    return;
                case bool flag:
                    writer.Write(flag);
                    return;
                case int number:
                    writer.Write(number);
                    return;
                case long number:
                    writer.Write(number);
                    return;
                case ulong number:
                    writer.Write(number);
                    return;
                case int[] numbers:
                    writer.Write(numbers.Length);
                    writer.Write(MemoryMarshal.AsBytes(numbers.AsSpan()));
                    return;
                case Enum choice:
                    writer.Write(Convert.ToInt64(choice, System.Globalization.CultureInfo.InvariantCulture));
                    return;
                case Delegate:
                case Type:
                    return;
            }

            Type type = value.GetType();
            if (type.IsPrimitive)
            {
                writer.Write(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            // A reference already being walked is a cycle: its content is covered by the outer visit.
            if (!type.IsValueType && !visiting.Add(value))
            {
                return;
            }

            if (value is IEnumerable items)
            {
                foreach (object? item in items)
                {
                    Write(writer, item, visiting);
                }
            }
            else
            {
                foreach (FieldInfo field in _fieldsByType.GetOrAdd(type, FieldsOf))
                {
                    Write(writer, field.GetValue(value), visiting);
                }
            }

            if (!type.IsValueType)
            {
                visiting.Remove(value);
            }
        }

        private static FieldInfo[] FieldsOf(Type type)
        {
            var fields = new List<FieldInfo>();
            for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                fields.AddRange(current.GetFields(_instanceFields | BindingFlags.DeclaredOnly)
                    .Where(field => !field.IsDefined(typeof(CompilerGeneratedAttribute), false) || field.Name.EndsWith("BackingField", StringComparison.Ordinal)));
            }

            return fields.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
        }
    }
}
