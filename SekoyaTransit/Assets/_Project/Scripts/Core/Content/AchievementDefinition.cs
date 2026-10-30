#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// A data-driven achievement track (Issue #65, reworked in Issue #223): one measured value and three milestones
    /// (bronze, silver, gold) with strictly increasing goals. Completion is permanent and pays nothing.
    /// </summary>
    public sealed class AchievementDefinition
    {
        /// <summary>
        /// The number of milestones of every track.
        /// </summary>
        public const int MilestoneCount = 3;

        /// <summary>
        /// The largest goal of an <see cref="AchievementKind.AchievementsUnlocked"/> track (100 percent).
        /// </summary>
        public const long MaxPercentGoal = 100L;

        private readonly ReadOnlyCollection<AchievementMilestone> _milestones;
        private readonly string[] _milestoneIds;

        /// <summary>
        /// Initializes a track. <paramref name="cargoId"/> is required for <see cref="AchievementKind.CargoSold"/> and
        /// forbidden for other kinds; <paramref name="labelKey"/> is required for every other kind (a cargo track is
        /// labelled with the cargo name). An <see cref="AchievementKind.AchievementsUnlocked"/> track has percentage
        /// goals of at most 100 and cannot scale with the map size.
        /// </summary>
        public AchievementDefinition(
            string id,
            AchievementKind kind,
            IReadOnlyList<AchievementMilestone> milestones,
            string? labelKey = null,
            string? cargoId = null,
            bool scalesWithMapSize = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Achievement id cannot be null or whitespace.", nameof(id));
            }

            if (kind < AchievementKind.AchievementsUnlocked || kind > AchievementKind.CargoSold)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown achievement kind.");
            }

#pragma warning disable CA1510
            if (milestones == null)
            {
                throw new ArgumentNullException(nameof(milestones));
            }
#pragma warning restore CA1510

            if (milestones.Count != MilestoneCount)
            {
                throw new ArgumentException(
                    $"An achievement track needs exactly {MilestoneCount} milestones.",
                    nameof(milestones));
            }

            var copy = new AchievementMilestone[MilestoneCount];
            for (int index = 0; index < MilestoneCount; index++)
            {
                AchievementMilestone milestone = milestones[index] ??
                    throw new ArgumentException("Milestones cannot contain null.", nameof(milestones));
                if (index > 0 && milestone.Goal <= copy[index - 1].Goal)
                {
                    throw new ArgumentException("Milestone goals must strictly increase.", nameof(milestones));
                }

                copy[index] = milestone;
            }

            if (kind == AchievementKind.CargoSold)
            {
                if (string.IsNullOrWhiteSpace(cargoId))
                {
                    throw new ArgumentException("A cargoSold track requires a cargo id.", nameof(cargoId));
                }

                if (labelKey != null)
                {
                    throw new ArgumentException("A cargoSold track is labelled with its cargo name.", nameof(labelKey));
                }
            }
            else
            {
                if (cargoId != null)
                {
                    throw new ArgumentException("Only cargoSold tracks may set a cargo id.", nameof(cargoId));
                }

                if (string.IsNullOrWhiteSpace(labelKey))
                {
                    throw new ArgumentException("This track kind requires a label key.", nameof(labelKey));
                }
            }

            if (kind == AchievementKind.AchievementsUnlocked)
            {
                if (copy[MilestoneCount - 1].Goal > MaxPercentGoal)
                {
                    throw new ArgumentException(
                        "An achievementsUnlocked track has percentage goals of at most 100.",
                        nameof(milestones));
                }

                if (scalesWithMapSize)
                {
                    throw new ArgumentException(
                        "An achievementsUnlocked track cannot scale with the map size.",
                        nameof(scalesWithMapSize));
                }
            }

            Id = id;
            Kind = kind;
            LabelKey = labelKey;
            CargoId = cargoId;
            ScalesWithMapSize = scalesWithMapSize;
            _milestones = Array.AsReadOnly(copy);
            _milestoneIds = new[] { id + ".bronze", id + ".silver", id + ".gold" };
        }

        /// <summary>
        /// Gets the stable track id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets what the track measures.
        /// </summary>
        public AchievementKind Kind { get; }

        /// <summary>
        /// Gets the localization key of the track label, or <see langword="null"/> for a cargo track.
        /// </summary>
        public string? LabelKey { get; }

        /// <summary>
        /// Gets the cargo id for <see cref="AchievementKind.CargoSold"/>, otherwise <see langword="null"/>.
        /// </summary>
        public string? CargoId { get; }

        /// <summary>
        /// Gets whether the goals are multiplied by the map-size goal scale.
        /// </summary>
        public bool ScalesWithMapSize { get; }

        /// <summary>
        /// Gets the bronze, silver, and gold milestones in that order.
        /// </summary>
        public IReadOnlyList<AchievementMilestone> Milestones
        {
            get
            {
                return _milestones;
            }
        }

        /// <summary>
        /// Returns the stable id of one milestone, as stored in saves: <c>{track id}.{tier}</c> (for example
        /// <c>lifetime_income.gold</c>).
        /// </summary>
        public string GetMilestoneId(AchievementTier tier)
        {
            if (tier < AchievementTier.Bronze || tier > AchievementTier.Gold)
            {
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown achievement tier.");
            }

            return _milestoneIds[(int)tier];
        }
    }
}
