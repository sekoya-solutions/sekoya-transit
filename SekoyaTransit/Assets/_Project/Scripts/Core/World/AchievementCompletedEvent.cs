#nullable enable
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Published once when an achievement milestone is completed (ADR 0024 queued delivery).
    /// </summary>
    public readonly struct AchievementCompletedEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public AchievementCompletedEvent(
            string milestoneId,
            string trackId,
            AchievementTier tier,
            string nameKey,
            GameDate date)
        {
            MilestoneId = milestoneId;
            TrackId = trackId;
            Tier = tier;
            NameKey = nameKey;
            Date = date;
        }

        /// <summary>
        /// Gets the completed milestone id (<c>{track id}.{tier}</c>).
        /// </summary>
        public string MilestoneId { get; }

        /// <summary>
        /// Gets the id of the track the milestone belongs to.
        /// </summary>
        public string TrackId { get; }

        /// <summary>
        /// Gets the milestone tier.
        /// </summary>
        public AchievementTier Tier { get; }

        /// <summary>
        /// Gets the localization key of the milestone title.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets the completion date.
        /// </summary>
        public GameDate Date { get; }
    }
}
