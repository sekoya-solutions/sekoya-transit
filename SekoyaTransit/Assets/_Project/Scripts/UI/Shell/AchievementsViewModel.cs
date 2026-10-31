#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Turns achievement tracks into display rows (Issue #223): the localized label (with the cargo icon for a cargo
    /// track), the current value, the bar fill, and each milestone's title, goal, and completion date. The three
    /// trophies are evenly spaced on the bar and the fill is linear between them.
    /// </summary>
    public static class AchievementsViewModel
    {
        /// <summary>
        /// Returns the position of a milestone trophy along the bar, from 0 to 1: the centers of three equal thirds.
        /// </summary>
        public static float MilestonePosition(int tier)
        {
            if (tier < 0 || tier >= AchievementTrackItem.MilestoneCount)
            {
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown milestone tier.");
            }

            return (2f * tier + 1f) / (2f * AchievementTrackItem.MilestoneCount);
        }

        /// <summary>
        /// Returns the filled part of the bar, from 0 to 1: linear from the bar start to the bronze trophy, between
        /// trophies, and full once the gold goal is reached.
        /// </summary>
        public static float Fill(long current, IReadOnlyList<AchievementMilestoneItem> milestones)
        {
            if (milestones is null)
            {
                throw new ArgumentNullException(nameof(milestones));
            }

            if (milestones.Count != AchievementTrackItem.MilestoneCount)
            {
                throw new ArgumentException("A track needs exactly three milestones.", nameof(milestones));
            }

            long previousGoal = 0L;
            float previousPosition = 0f;
            for (int tier = 0; tier < milestones.Count; tier++)
            {
                long goal = milestones[tier].Goal;
                float position = MilestonePosition(tier);
                if (current < goal)
                {
                    double ratio = goal > previousGoal
                        ? (double)(Math.Max(current, previousGoal) - previousGoal) / (goal - previousGoal)
                        : 0d;
                    return previousPosition + (float)(ratio * (position - previousPosition));
                }

                previousGoal = goal;
                previousPosition = position;
            }

            return 1f;
        }

        /// <summary>
        /// Builds one row per track, in input order.
        /// </summary>
        public static IReadOnlyList<AchievementTrackRow> BuildRows(
            IReadOnlyList<AchievementTrackItem> items,
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

            var rows = new AchievementTrackRow[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                AchievementTrackItem item = items[index];
                TextLine label = item.CargoId != null
                    ? TextLine.Icon(item.CargoId)
                    : TextLine.Plain(text.Get(item.LabelKey!));
                var milestones = new AchievementMilestoneRow[AchievementTrackItem.MilestoneCount];
                for (int tier = 0; tier < milestones.Length; tier++)
                {
                    AchievementMilestoneItem milestone = item.Milestones[tier];
                    milestones[tier] = new AchievementMilestoneRow(
                        text.Get(milestone.NameKey),
                        CashFormat.FormatAmount(milestone.Goal, culture),
                        milestone.CompletionDate,
                        milestone.IsCompleted);
                }

                rows[index] = new AchievementTrackRow(
                    item.Section,
                    label,
                    CashFormat.FormatAmount(item.Current, culture),
                    Fill(item.Current, item.Milestones),
                    milestones);
            }

            return rows;
        }

        /// <summary>
        /// Returns the localization key of a section heading.
        /// </summary>
        public static string SectionKey(AchievementSection section)
        {
            switch (section)
            {
                case AchievementSection.Global:
                    return GoalsUiKeys.AchievementsSectionGlobal;
                case AchievementSection.Company:
                    return GoalsUiKeys.AchievementsSectionCompany;
                case AchievementSection.Resources:
                    return GoalsUiKeys.AchievementsSectionResources;
                default:
                    throw new ArgumentOutOfRangeException(nameof(section), section, "Unknown achievement section.");
            }
        }
    }

    /// <summary>
    /// One display row of the achievements window.
    /// </summary>
    public sealed class AchievementTrackRow : IEquatable<AchievementTrackRow>
    {
        private readonly AchievementMilestoneRow[] _milestones;

        /// <summary>
        /// Initializes a row.
        /// </summary>
        public AchievementTrackRow(
            AchievementSection section,
            TextLine label,
            string valueText,
            float fill,
            AchievementMilestoneRow[] milestones)
        {
            Section = section;
            Label = label ?? throw new ArgumentNullException(nameof(label));
            ValueText = valueText ?? throw new ArgumentNullException(nameof(valueText));
            Fill = fill;
            _milestones = milestones ?? throw new ArgumentNullException(nameof(milestones));
        }

        /// <summary>
        /// Gets the window section.
        /// </summary>
        public AchievementSection Section { get; }

        /// <summary>
        /// Gets the track label (text and, for a cargo track, the cargo icon).
        /// </summary>
        public TextLine Label { get; }

        /// <summary>
        /// Gets the formatted current value.
        /// </summary>
        public string ValueText { get; }

        /// <summary>
        /// Gets the filled part of the bar, from 0 to 1.
        /// </summary>
        public float Fill { get; }

        /// <summary>
        /// Gets the bronze, silver, and gold milestone rows.
        /// </summary>
        public IReadOnlyList<AchievementMilestoneRow> Milestones => _milestones;

        /// <inheritdoc />
        public bool Equals(AchievementTrackRow? other)
        {
            if (other is null)
            {
                return false;
            }

            if (Section != other.Section ||
                !Label.Equals(other.Label) ||
                !string.Equals(ValueText, other.ValueText, StringComparison.Ordinal) ||
                !Fill.Equals(other.Fill) ||
                _milestones.Length != other._milestones.Length)
            {
                return false;
            }

            for (int index = 0; index < _milestones.Length; index++)
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
            return obj is AchievementTrackRow other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Label.GetHashCode() * 397) ^ ValueText.GetHashCode();
            }
        }
    }

    /// <summary>
    /// One milestone of an <see cref="AchievementTrackRow"/>.
    /// </summary>
    public readonly struct AchievementMilestoneRow : IEquatable<AchievementMilestoneRow>
    {
        /// <summary>
        /// Initializes a milestone row.
        /// </summary>
        public AchievementMilestoneRow(string title, string goalText, string? dateText, bool isUnlocked)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            GoalText = goalText ?? throw new ArgumentNullException(nameof(goalText));
            DateText = dateText;
            IsUnlocked = isUnlocked;
        }

        /// <summary>
        /// Gets the localized milestone title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the formatted goal.
        /// </summary>
        public string GoalText { get; }

        /// <summary>
        /// Gets the completion date, or <see langword="null"/> while locked.
        /// </summary>
        public string? DateText { get; }

        /// <summary>
        /// Gets whether the trophy is unlocked.
        /// </summary>
        public bool IsUnlocked { get; }

        /// <inheritdoc />
        public bool Equals(AchievementMilestoneRow other)
        {
            return string.Equals(Title, other.Title, StringComparison.Ordinal) &&
                string.Equals(GoalText, other.GoalText, StringComparison.Ordinal) &&
                string.Equals(DateText, other.DateText, StringComparison.Ordinal) &&
                IsUnlocked == other.IsUnlocked;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is AchievementMilestoneRow other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return ((Title?.GetHashCode() ?? 0) * 397) ^ IsUnlocked.GetHashCode();
            }
        }
    }
}
