#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Turns achievement items into display rows (Issue #66): the localized name, progress as current / goal
    /// (current is capped at the goal), and the completion date once done. Completed rows are flagged.
    /// </summary>
    public static class AchievementsViewModel
    {
        /// <summary>
        /// Builds one row per item, in input order.
        /// </summary>
        public static IReadOnlyList<AchievementRow> BuildRows(
            IReadOnlyList<AchievementListItem> items,
            ShellTextCache text,
            CultureInfo culture)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            var rows = new AchievementRow[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                AchievementListItem item = items[index];
                TextLine name = item.CargoId != null
                    ? TextLine.Concat(TextLine.Icon(item.CargoId), TextLine.Plain(text.Get(item.NameKey)))
                    : TextLine.Plain(text.Get(item.NameKey));
                TextLine current = TextLine.Plain(CashFormat.FormatAmount(Math.Min(item.Current, item.Goal), culture));
                TextLine goal = TextLine.Plain(CashFormat.FormatAmount(item.Goal, culture));
                TextLine line = item.IsCompleted
                    ? TextLine.Format(
                        text.Get(GoalsUiKeys.AchievementsRowCompleted),
                        name,
                        current,
                        goal,
                        TextLine.Plain(item.CompletionDate ?? string.Empty))
                    : TextLine.Format(text.Get(GoalsUiKeys.AchievementsRow), name, current, goal);
                rows[index] = new AchievementRow(line, item.IsCompleted);
            }

            return rows;
        }
    }

    /// <summary>
    /// One display row of the achievements panel.
    /// </summary>
    public readonly struct AchievementRow : IEquatable<AchievementRow>
    {
        /// <summary>
        /// Initializes a row.
        /// </summary>
        public AchievementRow(TextLine line, bool isCompleted)
        {
            Line = line ?? throw new ArgumentNullException(nameof(line));
            IsCompleted = isCompleted;
        }

        /// <summary>
        /// Gets the formatted row (text and, for a cargo delivery achievement, the cargo icon).
        /// </summary>
        public TextLine Line { get; }

        /// <summary>
        /// Gets whether the achievement is completed.
        /// </summary>
        public bool IsCompleted { get; }

        /// <inheritdoc />
        public bool Equals(AchievementRow other)
        {
            return Line.Equals(other.Line) && IsCompleted == other.IsCompleted;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is AchievementRow other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Line?.GetHashCode() ?? 0) * 397) ^ IsCompleted.GetHashCode();
            }
        }
    }
}
