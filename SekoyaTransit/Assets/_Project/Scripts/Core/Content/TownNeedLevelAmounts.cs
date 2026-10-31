#nullable enable
using System;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// One non-negative amount per need level: level 1 (raw), level 2 (processed) and level 3 (finished) goods
    /// (Issue #242).
    /// </summary>
    public readonly struct TownNeedLevelAmounts : IEquatable<TownNeedLevelAmounts>
    {
        /// <summary>
        /// Initializes the per-level amounts.
        /// </summary>
        public TownNeedLevelAmounts(int level1, int level2, int level3)
        {
            if (level1 < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level1), level1, "Level amounts cannot be negative.");
            }

            if (level2 < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level2), level2, "Level amounts cannot be negative.");
            }

            if (level3 < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(level3), level3, "Level amounts cannot be negative.");
            }

            Level1 = level1;
            Level2 = level2;
            Level3 = level3;
        }

        /// <summary>
        /// Gets the level 1 (raw goods) amount.
        /// </summary>
        public int Level1 { get; }

        /// <summary>
        /// Gets the level 2 (processed goods) amount.
        /// </summary>
        public int Level2 { get; }

        /// <summary>
        /// Gets the level 3 (finished goods) amount.
        /// </summary>
        public int Level3 { get; }

        /// <summary>
        /// Returns the amount for a manufacturing level from 1 to 3.
        /// </summary>
        public int Get(int level)
        {
            switch (level)
            {
                case 1:
                    return Level1;
                case 2:
                    return Level2;
                case 3:
                    return Level3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, "Need level must be 1, 2 or 3.");
            }
        }

        /// <inheritdoc />
        public bool Equals(TownNeedLevelAmounts other)
        {
            return Level1 == other.Level1 && Level2 == other.Level2 && Level3 == other.Level3;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is TownNeedLevelAmounts other && Equals(other);
        }

        /// <summary>
        /// Returns whether both amounts are equal per level.
        /// </summary>
        public static bool operator ==(TownNeedLevelAmounts left, TownNeedLevelAmounts right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns whether the amounts differ at any level.
        /// </summary>
        public static bool operator !=(TownNeedLevelAmounts left, TownNeedLevelAmounts right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Level1;
                hash = (hash * 397) ^ Level2;
                hash = (hash * 397) ^ Level3;
                return hash;
            }
        }
    }
}
