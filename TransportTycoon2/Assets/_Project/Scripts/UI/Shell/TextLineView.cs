#nullable enable
using System;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Shows a <see cref="TextLine"/> as a wrapping row of labels and cargo icons (Issue #182). Children are rebuilt
    /// only when the line changes.
    /// </summary>
    public sealed class TextLineView : VisualElement
    {
        /// <summary>
        /// USS class of the row.
        /// </summary>
        public const string UssClass = "text-line";

        /// <summary>
        /// USS class of a text piece.
        /// </summary>
        public const string TextUssClass = "text-line__text";

        /// <summary>
        /// USS class of a cargo icon.
        /// </summary>
        public const string IconUssClass = "text-line__icon";

        private const char NoBreakSpace = ' ';
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
            for (int index = 0; index < line.Segments.Count; index++)
            {
                TextLineSegment segment = line.Segments[index];
                if (segment.Kind == TextLineSegmentKind.Text)
                {
                    var label = new Label(KeepEdgeSpaces(segment.Value)) { pickingMode = PickingMode.Ignore };
                    label.AddToClassList(TextUssClass);
                    Add(label);
                    continue;
                }

                VectorImage? image = _icons?.GetIcon(segment.Value);
                if (image == null)
                {
                    continue;
                }

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList(IconUssClass);
                icon.style.backgroundImage = new StyleBackground(image);
                Add(icon);
            }
        }

        // Each text piece is its own label, so a space at its edge would be trimmed by layout; a no-break space keeps
        // the gap between words that sit on either side of an icon.
        private static string KeepEdgeSpaces(string text)
        {
            if (text.Length == 0)
            {
                return text;
            }

            char[] characters = text.ToCharArray();
            if (characters[0] == ' ')
            {
                characters[0] = NoBreakSpace;
            }

            if (characters[characters.Length - 1] == ' ')
            {
                characters[characters.Length - 1] = NoBreakSpace;
            }

            return new string(characters);
        }
    }
}
