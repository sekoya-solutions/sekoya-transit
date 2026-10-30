#nullable enable

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// What an achievement track measures (Issue #65, reworked into tiered tracks in Issue #223).
    /// </summary>
    public enum AchievementKind
    {
        /// <summary>
        /// Percentage of the catalog's other milestones unlocked (the global track; goals are percentages).
        /// </summary>
        AchievementsUnlocked = 0,

        /// <summary>
        /// Lifetime gross income: delivery income plus contract bonuses (start cash and vehicle sales excluded).
        /// </summary>
        LifetimeIncome = 1,

        /// <summary>
        /// Contracts fulfilled over the whole game.
        /// </summary>
        ContractsCompleted = 2,

        /// <summary>
        /// Vehicle models permanently unlocked with money.
        /// </summary>
        VehicleTypesUnlocked = 3,

        /// <summary>
        /// Road vehicles owned at the same time.
        /// </summary>
        RoadVehicleCount = 4,

        /// <summary>
        /// Trains (locomotives; wagons do not count) owned at the same time.
        /// </summary>
        TrainCount = 5,

        /// <summary>
        /// Lifetime tiles travelled by road vehicles.
        /// </summary>
        RoadTilesTravelled = 6,

        /// <summary>
        /// Lifetime tiles travelled by trains.
        /// </summary>
        RailTilesTravelled = 7,

        /// <summary>
        /// Population of the largest town.
        /// </summary>
        LargestTown = 8,

        /// <summary>
        /// Lifetime units of one cargo delivered and paid, to any destination.
        /// </summary>
        CargoSold = 9
    }
}
