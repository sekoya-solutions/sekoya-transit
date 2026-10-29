#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Evaluates achievements against the world. Completion is permanent, pays nothing, and never ends the game.
    /// </summary>
    public static class Achievements
    {
        /// <summary>
        /// Completes every not-yet-completed achievement in <paramref name="catalog"/> whose condition holds,
        /// in catalog order, recording <paramref name="date"/> and publishing one
        /// <see cref="AchievementCompletedEvent"/> each when <paramref name="events"/> is supplied.
        /// Completed ids absent from the catalog are ignored. Returns the number newly completed.
        /// </summary>
        public static int AdvanceAll(
            GeneratedWorld world,
            ContentCatalog catalog,
            GameDate date,
            EventBus? events = null)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            int completed = 0;
            for (int index = 0; index < catalog.Achievements.Count; index++)
            {
                AchievementDefinition definition = catalog.Achievements[index];
                if (world.IsAchievementCompleted(definition.Id) || !IsMet(world, definition))
                {
                    continue;
                }

                world.CompleteAchievement(definition.Id, date);
                completed++;
                if (events != null)
                {
                    events.Publish(new AchievementCompletedEvent(definition.Id, definition.NameKey, date));
                }
            }

            return completed;
        }

        /// <summary>
        /// Returns whether the current world state satisfies <paramref name="definition"/>.
        /// </summary>
        public static bool IsMet(GeneratedWorld world, AchievementDefinition definition)
        {
            return GetProgress(world, definition) >= definition.Goal;
        }

        /// <summary>
        /// Returns the current value measured against <see cref="AchievementDefinition.Goal"/>: the largest town
        /// population, company cash, road vehicles plus trains, or the lifetime units delivered. This is a read-only,
        /// on-demand query; no progress is saved (Issue #66).
        /// </summary>
        public static long GetProgress(GeneratedWorld world, AchievementDefinition definition)
        {
#pragma warning disable CA1510
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
#pragma warning restore CA1510

            switch (definition.Kind)
            {
                case AchievementKind.TownPopulation:
                    long largest = 0L;
                    for (int index = 0; index < world.Towns.Count; index++)
                    {
                        largest = Math.Max(largest, world.Towns[index].Population);
                    }

                    return largest;
                case AchievementKind.CompanyCash:
                    return world.CompanyCash;
                case AchievementKind.VehicleCount:
                    return (long)world.RoadVehicles.Count + world.RailVehicles.Count;
                case AchievementKind.CargoDelivered:
                    return world.GetCargoDelivered(definition.CargoId!, definition.DestinationKind!.Value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition), definition.Kind, "Unknown achievement kind.");
            }
        }
    }
}
