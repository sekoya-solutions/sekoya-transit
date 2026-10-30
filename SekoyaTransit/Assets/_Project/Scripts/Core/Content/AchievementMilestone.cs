#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// One tier of an achievement track: its goal before map-size scaling and the localization key of its title
    /// (Issue #223).
    /// </summary>
    public sealed class AchievementMilestone
    {
        /// <summary>
        /// Initializes a milestone.
        /// </summary>
        public AchievementMilestone(long goal, string nameKey)
        {
            if (goal <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(goal), goal, "Milestone goal must be positive.");
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Milestone name key cannot be null or whitespace.", nameof(nameKey));
            }

            Goal = goal;
            NameKey = nameKey;
        }

        /// <summary>
        /// Gets the positive goal as written in content (before map-size scaling).
        /// </summary>
        public long Goal { get; }

        /// <summary>
        /// Gets the localization key of the milestone title.
        /// </summary>
        public string NameKey { get; }
    }
}
