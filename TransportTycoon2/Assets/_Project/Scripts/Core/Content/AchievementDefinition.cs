#nullable enable
using System;

namespace TransportTycoon.Core.Content
{
    /// <summary>
    /// A data-driven achievement: a permanent milestone with no reward (Issue #65).
    /// </summary>
    public sealed class AchievementDefinition
    {
        /// <summary>
        /// Initializes an achievement. <paramref name="cargoId"/> and <paramref name="destinationKind"/>
        /// are required for <see cref="AchievementKind.CargoDelivered"/> and forbidden for other kinds.
        /// </summary>
        public AchievementDefinition(
            string id,
            string nameKey,
            AchievementKind kind,
            long goal,
            string? cargoId = null,
            DeliveryDestinationKind? destinationKind = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Achievement id cannot be null or whitespace.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(nameKey))
            {
                throw new ArgumentException("Achievement name key cannot be null or whitespace.", nameof(nameKey));
            }

            if (kind < AchievementKind.TownPopulation || kind > AchievementKind.CargoDelivered)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown achievement kind.");
            }

            if (goal <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(goal), goal, "Achievement goal must be positive.");
            }

            if (kind == AchievementKind.CargoDelivered)
            {
                if (string.IsNullOrWhiteSpace(cargoId))
                {
                    throw new ArgumentException("A cargoDelivered achievement requires a cargo id.", nameof(cargoId));
                }

                if (destinationKind == null ||
                    destinationKind.Value < DeliveryDestinationKind.Town ||
                    destinationKind.Value > DeliveryDestinationKind.Any)
                {
                    throw new ArgumentException(
                        "A cargoDelivered achievement requires a destination kind.",
                        nameof(destinationKind));
                }
            }
            else if (cargoId != null || destinationKind != null)
            {
                throw new ArgumentException("Only cargoDelivered achievements may set a cargo id or destination kind.");
            }

            Id = id;
            NameKey = nameKey;
            Kind = kind;
            Goal = goal;
            CargoId = cargoId;
            DestinationKind = destinationKind;
        }

        /// <summary>
        /// Gets the stable achievement id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the localization key for the display name.
        /// </summary>
        public string NameKey { get; }

        /// <summary>
        /// Gets the condition family.
        /// </summary>
        public AchievementKind Kind { get; }

        /// <summary>
        /// Gets the positive goal: inhabitants, cash, vehicles, or cargo units depending on <see cref="Kind"/>.
        /// </summary>
        public long Goal { get; }

        /// <summary>
        /// Gets the cargo id for <see cref="AchievementKind.CargoDelivered"/>, otherwise <see langword="null"/>.
        /// </summary>
        public string? CargoId { get; }

        /// <summary>
        /// Gets the destination filter for <see cref="AchievementKind.CargoDelivered"/>, otherwise <see langword="null"/>.
        /// </summary>
        public DeliveryDestinationKind? DestinationKind { get; }
    }
}
