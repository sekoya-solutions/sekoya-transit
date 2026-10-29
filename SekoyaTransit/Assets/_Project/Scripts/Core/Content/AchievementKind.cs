#nullable enable

namespace SekoyaTransit.Core.Content
{
    /// <summary>
    /// Condition family of an achievement (Issue #65).
    /// </summary>
    public enum AchievementKind
    {
        /// <summary>
        /// Any single town reaches the goal population.
        /// </summary>
        TownPopulation = 0,

        /// <summary>
        /// Company cash reaches the goal.
        /// </summary>
        CompanyCash = 1,

        /// <summary>
        /// Road vehicles plus trains reach the goal (wagons do not count).
        /// </summary>
        VehicleCount = 2,

        /// <summary>
        /// Lifetime units of one cargo delivered to a destination kind reach the goal.
        /// </summary>
        CargoDelivered = 3
    }
}
