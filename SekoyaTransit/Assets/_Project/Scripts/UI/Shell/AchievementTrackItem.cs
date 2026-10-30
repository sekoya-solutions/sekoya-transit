#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// UI-facing snapshot of one achievement track for the achievements window (Issue #223): its section, label, current
    /// value, and its three milestones. Presentation builds these from Core; progress is queried on demand.
    /// </summary>
    public sealed class AchievementTrackItem : IEquatable<AchievementTrackItem>
    {
        /// <summary>
        /// The number of milestones of every track.
        /// </summary>
        public const int MilestoneCount = 3;

        private readonly AchievementMilestoneItem[] _milestones;

        /// <summary>
        /// Initializes a track. Exactly one of <paramref name="labelKey"/> and <paramref name="cargoId"/> is set: a cargo
        /// track is labelled with <paramref name="cargoName"/> and the cargo icon.
        /// </summary>
        public AchievementTrackItem(
            AchievementSection section,
            string? labelKey,
            string? cargoId,
            string? cargoName,
            long current,
            IReadOnlyList<AchievementMilestoneItem> milestones)
        {
            if ((labelKey == null) == (cargoId == null))
            {
                throw new ArgumentException("A track needs exactly one of a label key and a cargo id.");
            }

            if ((cargoId == null) != (cargoName == null))
            {
                throw new ArgumentException("A cargo track needs a cargo name, and only a cargo track has one.");
            }

            if (current < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "Current progress cannot be negative.");
            }

            if (milestones is null)
            {
                throw new ArgumentNullException(nameof(milestones));
            }

            if (milestones.Count != MilestoneCount)
            {
                throw new ArgumentException($"A track needs exactly {MilestoneCount} milestones.", nameof(milestones));
            }

            _milestones = new AchievementMilestoneItem[MilestoneCount];
            for (int index = 0; index < MilestoneCount; index++)
            {
                _milestones[index] = milestones[index];
            }

            Section = section;
            LabelKey = labelKey;
            CargoId = cargoId;
            CargoName = cargoName;
            Current = current;
        }

        /// <summary>
        /// Gets the window section.
        /// </summary>
        public AchievementSection Section { get; }

        /// <summary>
        /// Gets the localization key of the label, or <see langword="null"/> for a cargo track.
        /// </summary>
        public string? LabelKey { get; }

        /// <summary>
        /// Gets the cargo id of a cargo track, otherwise <see langword="null"/>.
        /// </summary>
        public string? CargoId { get; }

        /// <summary>
        /// Gets the display name of a cargo track's cargo, otherwise <see langword="null"/>.
        /// </summary>
        public string? CargoName { get; }

        /// <summary>
        /// Gets the current value measured against the milestone goals.
        /// </summary>
        public long Current { get; }

        /// <summary>
        /// Gets the bronze, silver, and gold milestones.
        /// </summary>
        public IReadOnlyList<AchievementMilestoneItem> Milestones => _milestones;

        /// <inheritdoc />
        public bool Equals(AchievementTrackItem? other)
        {
            if (other is null)
            {
                return false;
            }

            if (Section != other.Section ||
                !string.Equals(LabelKey, other.LabelKey, StringComparison.Ordinal) ||
                !string.Equals(CargoId, other.CargoId, StringComparison.Ordinal) ||
                !string.Equals(CargoName, other.CargoName, StringComparison.Ordinal) ||
                Current != other.Current)
            {
                return false;
            }

            for (int index = 0; index < MilestoneCount; index++)
            {
                if (!_milestones[index].Equals(other._milestones[index]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is AchievementTrackItem other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Section;
                hash = (hash * 397) ^ (LabelKey?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (CargoId?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ Current.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// One milestone of an <see cref="AchievementTrackItem"/>: its goal for this map, title key, and completion.
    /// </summary>
    public readonly struct AchievementMilestoneItem : IEquatable<AchievementMilestoneItem>
    {
        /// <summary>
        /// Initializes a milestone. <paramref name="completionDate"/> is kept only when completed.
        /// </summary>
        public AchievementMilestoneItem(long goal, string nameKey, bool isCompleted, string? completionDate)
        {
            if (goal <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(goal), goal, "Goal must be positive.");
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Name key cannot be null or whitespace.", nameof(nameKey));
            }

            Goal = goal;
            NameKey = nameKey;
            IsCompleted = isCompleted;
            CompletionDate = isCompleted ? completionDate : null;
        }

        /// <summary>
        /// Gets the goal for this map.
        /// </summary>
        public long Goal { get; }

        /// <summary>
        /// Gets the localization key of the milestone title.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets whether the milestone is unlocked.
        /// </summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// Gets the completion date text, or <see langword="null"/> while locked.
        /// </summary>
        public string? CompletionDate { get; }

        /// <inheritdoc />
        public bool Equals(AchievementMilestoneItem other)
        {
            return Goal == other.Goal &&
                string.Equals(NameKey, other.NameKey, StringComparison.Ordinal) &&
                IsCompleted == other.IsCompleted &&
                string.Equals(CompletionDate, other.CompletionDate, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is AchievementMilestoneItem other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Goal.GetHashCode();
                hash = (hash * 397) ^ (NameKey?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ IsCompleted.GetHashCode();
                return hash;
            }
        }
    }
}
