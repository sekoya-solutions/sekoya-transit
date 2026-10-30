#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Per-map-size multiplier, in percent, applied to the goals of tracks that scale with the map size (Issue #223):
    /// a larger map has more industries and towns, so the same milestone would be reached much sooner.
    /// </summary>
    public sealed class AchievementGoalScale
    {
        /// <summary>
        /// The unscaled multiplier (100 percent).
        /// </summary>
        public const int NeutralPercent = 100;

        /// <summary>
        /// Initializes the scale. Every percentage must be positive.
        /// </summary>
        public AchievementGoalScale(int smallPercent, int mediumPercent, int largePercent)
        {
            SmallPercent = RequirePositive(smallPercent, nameof(smallPercent));
            MediumPercent = RequirePositive(mediumPercent, nameof(mediumPercent));
            LargePercent = RequirePositive(largePercent, nameof(largePercent));
        }

        /// <summary>
        /// Gets the scale that leaves every goal unchanged.
        /// </summary>
        public static AchievementGoalScale Neutral { get; } =
            new AchievementGoalScale(NeutralPercent, NeutralPercent, NeutralPercent);

        /// <summary>
        /// Gets the Small map multiplier in percent.
        /// </summary>
        public int SmallPercent { get; }

        /// <summary>
        /// Gets the Medium map multiplier in percent.
        /// </summary>
        public int MediumPercent { get; }

        /// <summary>
        /// Gets the Large map multiplier in percent.
        /// </summary>
        public int LargePercent { get; }

        /// <summary>
        /// Gets the multiplier in percent for <paramref name="mapSize"/>.
        /// </summary>
        public int GetPercent(MapSize mapSize)
        {
            switch (mapSize)
            {
                case MapSize.Small:
                    return SmallPercent;
                case MapSize.Medium:
                    return MediumPercent;
                case MapSize.Large:
                    return LargePercent;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mapSize), mapSize, "Map size is not supported.");
            }
        }

        /// <summary>
        /// Scales <paramref name="goal"/> for <paramref name="mapSize"/> with integer math (rounded down, at least 1).
        /// </summary>
        public long Apply(long goal, MapSize mapSize)
        {
            long scaled = checked(goal * GetPercent(mapSize)) / NeutralPercent;
            return scaled < 1L ? 1L : scaled;
        }

        private static int RequirePositive(int percent, string name)
        {
            if (percent <= 0)
            {
                throw new ArgumentOutOfRangeException(name, percent, "A goal scale percentage must be positive.");
            }

            return percent;
        }
    }
}
