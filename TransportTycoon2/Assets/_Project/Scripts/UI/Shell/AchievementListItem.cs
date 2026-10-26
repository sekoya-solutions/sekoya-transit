#nullable enable
using System;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of one achievement for the achievements panel (Issue #66).
    /// Presentation builds these from Core; progress is queried on demand and never saved.
    /// </summary>
    public readonly struct AchievementListItem : IEquatable<AchievementListItem>
    {
        /// <summary>
        /// Initializes an achievement row. <paramref name="completionDate"/> is shown only when completed.
        /// </summary>
        public AchievementListItem(string nameKey, long current, long goal, bool isCompleted, string? completionDate)
        {
            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Name key cannot be null or whitespace.", nameof(nameKey));
            }

            if (current < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "Current progress cannot be negative.");
            }

            if (goal <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(goal), goal, "Goal must be positive.");
            }

            NameKey = nameKey;
            Current = current;
            Goal = goal;
            IsCompleted = isCompleted;
            CompletionDate = isCompleted ? completionDate : null;
        }

        /// <summary>
        /// Gets the localization key of the achievement name.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets the current value measured against <see cref="Goal"/> (may exceed it).
        /// </summary>
        public long Current { get; }

        /// <summary>
        /// Gets the goal.
        /// </summary>
        public long Goal { get; }

        /// <summary>
        /// Gets whether the achievement is completed.
        /// </summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// Gets the formatted completion date, or null while not completed.
        /// </summary>
        public string? CompletionDate { get; }

        /// <inheritdoc />
        public bool Equals(AchievementListItem other)
        {
            return string.Equals(NameKey, other.NameKey, StringComparison.Ordinal)
                && Current == other.Current
                && Goal == other.Goal
                && IsCompleted == other.IsCompleted
                && string.Equals(CompletionDate, other.CompletionDate, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is AchievementListItem other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(NameKey ?? string.Empty);
                hash = (hash * 397) ^ Current.GetHashCode();
                hash = (hash * 397) ^ Goal.GetHashCode();
                hash = (hash * 397) ^ IsCompleted.GetHashCode();
                return (hash * 397) ^ (CompletionDate is null ? 0 : StringComparer.Ordinal.GetHashCode(CompletionDate));
            }
        }
    }
}
