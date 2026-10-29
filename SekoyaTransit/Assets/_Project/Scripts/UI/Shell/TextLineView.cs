#nullable enable
using System;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Shows a <see cref="TextLine"/> as wrapping text with cargo icons (Issue #182). Text is laid out one word per
    /// label so lines wrap between words, and each icon is kept in one unbreakable piece with the word after it, so
    /// an icon never ends a line apart from its cargo name. Children are rebuilt only when the line changes.
    /// </summary>
    public sealed class TextLineView : VisualElement
    {
        /// <summary>
        /// USS class of the row.
        /// </summary>
        public const string UssClass = "text-line";

        /// <summary>
        /// USS class of a word label.
        /// </summary>
        public const string TextUssClass = "text-line__text";

        /// <summary>
        /// USS class of a cargo icon.
        /// </summary>
        public const string IconUssClass = "text-line__icon";

        /// <summary>
        /// USS class of the piece that holds an icon and the word after it.
        /// </summary>
        public const string ChunkUssClass = "text-line__chunk";

        /// <summary>
        /// USS class of a word, icon, or chunk followed by a space (a label does not measure its edge spaces, so the
        /// gap is a margin).
        /// </summary>
        public const string SpaceAfterUssClass = "text-line__piece--space-after";

        private readonly ICargoIconSource? _icons;

        /// <summary>
        /// Initializes an empty line; <paramref name="icons"/> may be null, in which case only the text shows.
        /// </summary>
        public TextLineView(ICargoIconSource? icons)
        {
            _icons = icons;
            AddToClassList(UssClass);
            pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// Gets the line on display.
        /// </summary>
        public TextLine Line { get; private set; } = TextLine.Empty;

        /// <summary>
        /// Shows <paramref name="line"/>; does nothing when it equals the line on display.
        /// </summary>
        public void SetLine(TextLine line)
        {
            if (line is null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            if (line.Equals(Line))
            {
                return;
            }

            Line = line;
            Clear();
            VisualElement? pendingIcon = null;
            VisualElement? last = null;
            for (int index = 0; index < line.Segments.Count; index++)
            {
                TextLineSegment segment = line.Segments[index];
                if (segment.Kind == TextLineSegmentKind.CargoIcon)
                {
                    last = FlushIcon(ref pendingIcon) ?? last;
                    pendingIcon = CreateIcon(segment.Value);
                    continue;
                }

                string text = segment.Value;
                int position = 0;
                while (position < text.Length)
                {
                    if (text[position] == ' ')
                    {
                        while (position < text.Length && text[position] == ' ')
                        {
                            position++;
                        }

                        last = FlushIcon(ref pendingIcon) ?? last;
                        last?.AddToClassList(SpaceAfterUssClass);
                        continue;
                    }

                    int start = position;
                    while (position < text.Length && text[position] != ' ')
                    {
                        position++;
                    }

                    Label word = CreateWord(text.Substring(start, position - start));
                    if (pendingIcon == null)
                    {
                        Add(word);
                        last = word;
                        continue;
                    }

                    var chunk = new VisualElement { pickingMode = PickingMode.Ignore };
                    chunk.AddToClassList(ChunkUssClass);
                    chunk.Add(pendingIcon);
                    chunk.Add(word);
                    Add(chunk);
                    last = chunk;
                    pendingIcon = null;
                }
            }

            FlushIcon(ref pendingIcon);
        }

        // Adds a waiting icon on its own and returns it, or returns null when none is waiting.
        private VisualElement? FlushIcon(ref VisualElement? pendingIcon)
        {
            VisualElement? icon = pendingIcon;
            if (icon != null)
            {
                Add(icon);
                pendingIcon = null;
            }

            return icon;
        }

        // Returns null when no icon is available, so the cargo name shows alone.
        private VisualElement? CreateIcon(string cargoId)
        {
            VectorImage? image = _icons?.GetIcon(cargoId);
            if (image == null)
            {
                return null;
            }

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(IconUssClass);
            icon.style.backgroundImage = new StyleBackground(image);
            return icon;
        }

        private static Label CreateWord(string word)
        {
            var label = new Label(word) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(TextUssClass);
            return label;
        }
    }
}
