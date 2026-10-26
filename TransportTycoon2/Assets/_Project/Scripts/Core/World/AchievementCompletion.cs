#nullable enable
using System;
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// A completed achievement and the calendar date it was first met.
    /// </summary>
    public readonly struct AchievementCompletion : IEquatable<AchievementCompletion>
    {
        /// <summary>
        /// Initializes a completion record.
        /// </summary>
        public AchievementCompletion(string achievementId, GameDate date)
        {
            if (string.IsNullOrWhiteSpace(achievementId))
            {
                throw new ArgumentException("Achievement id cannot be null or whitespace.", nameof(achievementId));
            }

            AchievementId = achievementId;
            Date = date;
        }

        /// <summary>
        /// Gets the achievement id.
        /// </summary>
        public string AchievementId { get; }

        /// <summary>
        /// Gets the date the achievement was completed.
        /// </summary>
        public GameDate Date { get; }

        /// <inheritdoc />
        public bool Equals(AchievementCompletion other)
        {
            return string.Equals(AchievementId, other.AchievementId, StringComparison.Ordinal) &&
                Date.Equals(other.Date);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is AchievementCompletion other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(AchievementId), Date);
        }

        /// <summary>
        /// Returns whether two completions are equal.
        /// </summary>
        public static bool operator ==(AchievementCompletion left, AchievementCompletion right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns whether two completions differ.
        /// </summary>
        public static bool operator !=(AchievementCompletion left, AchievementCompletion right)
        {
            return !left.Equals(right);
        }
    }
}
