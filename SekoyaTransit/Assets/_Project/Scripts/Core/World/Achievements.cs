#nullable enable
using System;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Evaluates achievement tracks against the world (Issue #65, tiered in Issue #223). Each milestone completes on
    /// its own; completion is permanent, pays nothing, and never ends the game.
    /// </summary>
    public static class Achievements
    {
        /// <summary>
        /// Completes every not-yet-completed milestone whose goal is reached, track by track in catalog order and
        /// bronze to gold, recording <paramref name="date"/> and publishing one <see cref="AchievementCompletedEvent"/>
        /// each when <paramref name="events"/> is supplied. The global track is evaluated last so it counts the
        /// milestones completed in the same call. Completed ids absent from the catalog are ignored. Returns the
        /// number of milestones newly completed.
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
            AchievementDefinition? globalTrack = null;
            for (int index = 0; index < catalog.Achievements.Count; index++)
            {
                AchievementDefinition definition = catalog.Achievements[index];
                if (definition.Kind == AchievementKind.AchievementsUnlocked)
                {
                    globalTrack = definition;
                    continue;
                }

                completed += AdvanceTrack(world, catalog, definition, date, events);
            }

            if (globalTrack != null)
            {
                completed += AdvanceTrack(world, catalog, globalTrack, date, events);
            }

            return completed;
        }

        /// <summary>
        /// Returns the current value of a track: the number of other milestones unlocked, lifetime income, contracts
        /// fulfilled, vehicle models unlocked, road vehicles or trains owned, road or rail tiles travelled, the largest
        /// town population, or the units of one cargo sold. This is a read-only, on-demand query.
        /// </summary>
        public static long GetProgress(GeneratedWorld world, ContentCatalog catalog, AchievementDefinition definition)
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

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
#pragma warning restore CA1510

            switch (definition.Kind)
            {
                case AchievementKind.AchievementsUnlocked:
                    return CountCompletedMilestones(world, catalog);
                case AchievementKind.LifetimeIncome:
                    return world.Statistics.LifetimeIncome;
                case AchievementKind.ContractsCompleted:
                    return world.Statistics.ContractsCompleted;
                case AchievementKind.VehicleTypesUnlocked:
                    return world.UnlockedVehicleTypeIds.Count;
                case AchievementKind.RoadVehicleCount:
                    return world.RoadVehicles.Count;
                case AchievementKind.TrainCount:
                    return world.RailVehicles.Count;
                case AchievementKind.RoadTilesTravelled:
                    return world.Statistics.RoadTilesTravelled;
                case AchievementKind.RailTilesTravelled:
                    return world.Statistics.RailTilesTravelled;
                case AchievementKind.LargestTown:
                    long largest = 0L;
                    for (int index = 0; index < world.Towns.Count; index++)
                    {
                        largest = Math.Max(largest, world.Towns[index].Population);
                    }

                    return largest;
                case AchievementKind.CargoSold:
                    return world.GetCargoDelivered(definition.CargoId!, DeliveryDestinationKind.Any);
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition), definition.Kind, "Unknown achievement kind.");
            }
        }

        /// <summary>
        /// Returns the goal of one milestone for this world, in the unit of <see cref="GetProgress"/>: the content goal
        /// multiplied by the map-size scale for a scaling track, or, for the global track, the number of other
        /// milestones matching its percentage (rounded up, at least 1).
        /// </summary>
        public static long GetGoal(
            GeneratedWorld world,
            ContentCatalog catalog,
            AchievementDefinition definition,
            AchievementTier tier)
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

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }
#pragma warning restore CA1510

            long goal = definition.Milestones[(int)tier].Goal;
            if (definition.Kind == AchievementKind.AchievementsUnlocked)
            {
                long total = CountMilestones(catalog);
                long count = (checked(goal * total) + AchievementDefinition.MaxPercentGoal - 1L) /
                    AchievementDefinition.MaxPercentGoal;
                return count < 1L ? 1L : count;
            }

            return definition.ScalesWithMapSize ? catalog.AchievementGoalScale.Apply(goal, world.Map.Size) : goal;
        }

        /// <summary>
        /// Returns the number of milestones the global track counts: every milestone of every other track.
        /// </summary>
        public static int CountMilestones(ContentCatalog catalog)
        {
#pragma warning disable CA1510
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
#pragma warning restore CA1510

            int count = 0;
            for (int index = 0; index < catalog.Achievements.Count; index++)
            {
                if (catalog.Achievements[index].Kind != AchievementKind.AchievementsUnlocked)
                {
                    count += AchievementDefinition.MilestoneCount;
                }
            }

            return count;
        }

        private static long CountCompletedMilestones(GeneratedWorld world, ContentCatalog catalog)
        {
            long count = 0L;
            for (int index = 0; index < catalog.Achievements.Count; index++)
            {
                AchievementDefinition definition = catalog.Achievements[index];
                if (definition.Kind == AchievementKind.AchievementsUnlocked)
                {
                    continue;
                }

                for (int tier = 0; tier < AchievementDefinition.MilestoneCount; tier++)
                {
                    if (world.IsAchievementCompleted(definition.GetMilestoneId((AchievementTier)tier)))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int AdvanceTrack(
            GeneratedWorld world,
            ContentCatalog catalog,
            AchievementDefinition definition,
            GameDate date,
            EventBus? events)
        {
            int completed = 0;
            long progress = -1L;
            for (int index = 0; index < AchievementDefinition.MilestoneCount; index++)
            {
                var tier = (AchievementTier)index;
                string milestoneId = definition.GetMilestoneId(tier);
                if (world.IsAchievementCompleted(milestoneId))
                {
                    continue;
                }

                if (progress < 0L)
                {
                    progress = GetProgress(world, catalog, definition);
                }

                if (progress < GetGoal(world, catalog, definition, tier))
                {
                    // Goals strictly increase, so no later milestone of this track can be met either.
                    break;
                }

                world.CompleteAchievement(milestoneId, date);
                completed++;
                if (events != null)
                {
                    events.Publish(
                        new AchievementCompletedEvent(
                            milestoneId,
                            definition.Id,
                            tier,
                            definition.Milestones[index].NameKey,
                            date));
                }
            }

            return completed;
        }
    }
}
