#nullable enable
using System;
using System.Collections.Generic;
using System.Text;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The kind of one <see cref="TextLineSegment"/>.
    /// </summary>
    public enum TextLineSegmentKind
    {
        /// <summary>
        /// Plain text.
        /// </summary>
        Text = 0,

        /// <summary>
        /// The icon of a cargo; the segment value is the cargo id.
        /// </summary>
        CargoIcon = 1,
    }

    /// <summary>
    /// One piece of a <see cref="TextLine"/>: plain text or a cargo icon.
    /// </summary>
    public readonly struct TextLineSegment : IEquatable<TextLineSegment>
    {
        private TextLineSegment(TextLineSegmentKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }

        /// <summary>
        /// Gets the segment kind.
        /// </summary>
        public TextLineSegmentKind Kind { get; }

        /// <summary>
        /// Gets the text, or the cargo id for <see cref="TextLineSegmentKind.CargoIcon"/>.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Creates a text segment.
        /// </summary>
        public static TextLineSegment Text(string text)
        {
            return new TextLineSegment(TextLineSegmentKind.Text, text ?? throw new ArgumentNullException(nameof(text)));
        }

        /// <summary>
        /// Creates a cargo icon segment.
        /// </summary>
        public static TextLineSegment CargoIcon(string cargoId)
        {
            if (string.IsNullOrWhiteSpace(cargoId))
            {
                throw new ArgumentException("Cargo id cannot be null or whitespace.", nameof(cargoId));
            }

            return new TextLineSegment(TextLineSegmentKind.CargoIcon, cargoId);
        }

        /// <inheritdoc />
        public bool Equals(TextLineSegment other)
        {
            return Kind == other.Kind && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is TextLineSegment other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// A display line made of text and cargo icons (Issue #182). Localized format strings are split at their
    /// <c>{n}</c> placeholders by <see cref="Format"/>, so the translated word order is kept and a cargo argument
    /// shows its icon in front of its name.
    /// </summary>
    public sealed class TextLine : IEquatable<TextLine>
    {
        private readonly TextLineSegment[] _segments;

        private TextLine(TextLineSegment[] segments)
        {
            _segments = segments;
        }

        /// <summary>
        /// Gets the empty line.
        /// </summary>
        public static TextLine Empty { get; } = new TextLine(Array.Empty<TextLineSegment>());

        /// <summary>
        /// Gets the segments; adjacent text is merged and empty text is dropped.
        /// </summary>
        public IReadOnlyList<TextLineSegment> Segments => _segments;

        /// <summary>
        /// Gets whether the line has no segments.
        /// </summary>
        public bool IsEmpty => _segments.Length == 0;

        /// <summary>
        /// Creates a line of plain text.
        /// </summary>
        public static TextLine Plain(string text)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return text.Length == 0 ? Empty : new TextLine(new[] { TextLineSegment.Text(text) });
        }

        /// <summary>
        /// Creates a line holding one cargo icon.
        /// </summary>
        public static TextLine Icon(string cargoId)
        {
            return new TextLine(new[] { TextLineSegment.CargoIcon(cargoId) });
        }

        /// <summary>
        /// Creates a cargo reference: its icon followed by its display name.
        /// </summary>
        public static TextLine Cargo(string cargoId, string displayName)
        {
            return Concat(Icon(cargoId), Plain(displayName));
        }

        /// <summary>
        /// Joins lines end to end.
        /// </summary>
        public static TextLine Concat(params TextLine[] parts)
        {
            if (parts is null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            var builder = new Builder();
            for (int index = 0; index < parts.Length; index++)
            {
                builder.Append(parts[index] ?? throw new ArgumentNullException(nameof(parts)));
            }

            return builder.Build();
        }

        /// <summary>
        /// Joins lines with a plain text separator.
        /// </summary>
        public static TextLine Join(string separator, IReadOnlyList<TextLine> parts)
        {
            if (separator is null)
            {
                throw new ArgumentNullException(nameof(separator));
            }

            if (parts is null)
            {
                throw new ArgumentNullException(nameof(parts));
            }

            var builder = new Builder();
            for (int index = 0; index < parts.Count; index++)
            {
                if (index > 0)
                {
                    builder.AppendText(separator);
                }

                builder.Append(parts[index] ?? throw new ArgumentNullException(nameof(parts)));
            }

            return builder.Build();
        }

        /// <summary>
        /// Replaces each <c>{n}</c> placeholder of a composite format string with argument n, keeping the format's
        /// order. <c>{{</c> and <c>}}</c> stand for literal braces. Arguments are already formatted, so alignment and
        /// format components (<c>{0,5}</c>, <c>{0:N0}</c>) are rejected.
        /// </summary>
        /// <exception cref="FormatException">The format is malformed or references a missing argument.</exception>
        public static TextLine Format(string format, params TextLine[] args)
        {
            if (format is null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            var builder = new Builder();
            var literal = new StringBuilder();
            int position = 0;
            while (position < format.Length)
            {
                char current = format[position];
                if (current == '{')
                {
                    if (position + 1 < format.Length && format[position + 1] == '{')
                    {
                        literal.Append('{');
                        position += 2;
                        continue;
                    }

                    int close = format.IndexOf('}', position + 1);
                    if (close < 0)
                    {
                        throw new FormatException("Unclosed placeholder in format \"" + format + "\".");
                    }

                    int argumentIndex = ParseArgumentIndex(format, position + 1, close);
                    if (argumentIndex >= args.Length)
                    {
                        throw new FormatException(
                            "Format \"" + format + "\" references argument " + argumentIndex + " but only "
                            + args.Length + " were given.");
                    }

                    builder.AppendText(literal.ToString());
                    literal.Clear();
                    builder.Append(args[argumentIndex] ?? throw new ArgumentNullException(nameof(args)));
                    position = close + 1;
                    continue;
                }

                if (current == '}')
                {
                    if (position + 1 < format.Length && format[position + 1] == '}')
                    {
                        literal.Append('}');
                        position += 2;
                        continue;
                    }

                    throw new FormatException("Unmatched '}' in format \"" + format + "\".");
                }

                literal.Append(current);
                position++;
            }

            builder.AppendText(literal.ToString());
            return builder.Build();
        }

        /// <summary>
        /// Returns the text with icons left out (for tests, logs, and accessibility text).
        /// </summary>
        public string ToPlainText()
        {
            var text = new StringBuilder();
            for (int index = 0; index < _segments.Length; index++)
            {
                if (_segments[index].Kind == TextLineSegmentKind.Text)
                {
                    text.Append(_segments[index].Value);
                }
            }

            return text.ToString();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToPlainText();
        }

        /// <inheritdoc />
        public bool Equals(TextLine? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_segments.Length != other._segments.Length)
            {
                return false;
            }

            for (int index = 0; index < _segments.Length; index++)
            {
                if (!_segments[index].Equals(other._segments[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is TextLine other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _segments.Length;
                for (int index = 0; index < _segments.Length; index++)
                {
                    hash = (hash * 397) ^ _segments[index].GetHashCode();
                }

                return hash;
            }
        }

        private static int ParseArgumentIndex(string format, int start, int end)
        {
            if (start == end)
            {
                throw new FormatException("Empty placeholder in format \"" + format + "\".");
            }

            int value = 0;
            for (int index = start; index < end; index++)
            {
                char digit = format[index];
                if (digit < '0' || digit > '9')
                {
                    throw new FormatException(
                        "Placeholder in format \"" + format + "\" must be a plain argument index like {0}.");
                }

                value = checked((value * 10) + (digit - '0'));
            }

            return value;
        }

        private sealed class Builder
        {
            private readonly List<TextLineSegment> _segments = new List<TextLineSegment>();
            private readonly StringBuilder _pendingText = new StringBuilder();

            public void AppendText(string text)
            {
                _pendingText.Append(text);
            }

            public void Append(TextLine line)
            {
                for (int index = 0; index < line._segments.Length; index++)
                {
                    TextLineSegment segment = line._segments[index];
                    if (segment.Kind == TextLineSegmentKind.Text)
                    {
                        _pendingText.Append(segment.Value);
                    }
                    else
                    {
                        FlushText();
                        _segments.Add(segment);
                    }
                }
            }

            public TextLine Build()
            {
                FlushText();
                return _segments.Count == 0 ? Empty : new TextLine(_segments.ToArray());
            }

            private void FlushText()
            {
                if (_pendingText.Length > 0)
                {
                    _segments.Add(TextLineSegment.Text(_pendingText.ToString()));
                    _pendingText.Clear();
                }
            }
        }
    }
}
