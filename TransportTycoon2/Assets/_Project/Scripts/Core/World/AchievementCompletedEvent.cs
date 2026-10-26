#nullable enable
using TransportTycoon.Core.Simulation;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Published once when an achievement is completed (ADR 0024 queued delivery).
    /// </summary>
    public readonly struct AchievementCompletedEvent : ISimulationEvent
    {
        /// <summary>
        /// Initializes the event.
        /// </summary>
        public AchievementCompletedEvent(string achievementId, string nameKey, GameDate date)
        {
            AchievementId = achievementId;
            NameKey = nameKey;
            Date = date;
        }

        /// <summary>
        /// Gets the completed achievement id.
        /// </summary>
        public string AchievementId { get; }

        /// <summary>
        /// Gets the localization key of the achievement name.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets the completion date.
        /// </summary>
        public GameDate Date { get; }
    }
}
