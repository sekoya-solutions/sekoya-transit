#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace TransportTycoon.UI.Shell
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
                string name = text.Get(item.NameKey);
                string current = CashFormat.FormatAmount(Math.Min(item.Current, item.Goal), culture);
                string goal = CashFormat.FormatAmount(item.Goal, culture);
                string line = item.IsCompleted
                    ? string.Format(
                        culture,
                        text.Get(GoalsUiKeys.AchievementsRowCompleted),
                        name,
                        current,
                        goal,
                        item.CompletionDate ?? string.Empty)
                    : string.Format(culture, text.Get(GoalsUiKeys.AchievementsRow), name, current, goal);
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
        public AchievementRow(string text, bool isCompleted)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            IsCompleted = isCompleted;
        }

        /// <summary>
        /// Gets the formatted row text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets whether the achievement is completed.
        /// </summary>
        public bool IsCompleted { get; }

        /// <inheritdoc />
        public bool Equals(AchievementRow other)
        {
            return string.Equals(Text, other.Text, StringComparison.Ordinal) && IsCompleted == other.IsCompleted;
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
                return (StringComparer.Ordinal.GetHashCode(Text ?? string.Empty) * 397) ^ IsCompleted.GetHashCode();
            }
        }
    }
}
