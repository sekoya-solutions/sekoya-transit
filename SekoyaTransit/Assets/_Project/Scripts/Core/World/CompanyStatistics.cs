#nullable enable
using System;

namespace SekoyaTransit.Core.World
{
    /// <summary>
    /// Lifetime company counters measured by the achievement tracks (Issue #223). They only ever grow and are saved
    /// from format version 25; older saves start them at zero.
    /// </summary>
    public sealed class CompanyStatistics
    {
        /// <summary>
        /// Gets the lifetime gross income: delivery income plus contract bonuses.
        /// </summary>
        public long LifetimeIncome { get; private set; }

        /// <summary>
        /// Gets the number of contracts fulfilled.
        /// </summary>
        public long ContractsCompleted { get; private set; }

        /// <summary>
        /// Gets the tiles travelled by road vehicles (counted when a vehicle finishes a road edge).
        /// </summary>
        public long RoadTilesTravelled { get; private set; }

        /// <summary>
        /// Gets the tiles travelled by trains (counted when a train finishes a rail edge).
        /// </summary>
        public long RailTilesTravelled { get; private set; }

        /// <summary>
        /// Adds income to <see cref="LifetimeIncome"/>.
        /// </summary>
        public void AddIncome(long amount)
        {
            LifetimeIncome = checked(LifetimeIncome + RequireNonNegative(amount, nameof(amount)));
        }

        /// <summary>
        /// Counts one fulfilled contract.
        /// </summary>
        public void AddContractCompleted()
        {
            ContractsCompleted = checked(ContractsCompleted + 1L);
        }

        /// <summary>
        /// Adds tiles to <see cref="RoadTilesTravelled"/>.
        /// </summary>
        public void AddRoadTiles(long tiles)
        {
            RoadTilesTravelled = checked(RoadTilesTravelled + RequireNonNegative(tiles, nameof(tiles)));
        }

        /// <summary>
        /// Adds tiles to <see cref="RailTilesTravelled"/>.
        /// </summary>
        public void AddRailTiles(long tiles)
        {
            RailTilesTravelled = checked(RailTilesTravelled + RequireNonNegative(tiles, nameof(tiles)));
        }

        /// <summary>
        /// Replaces every counter (save restore).
        /// </summary>
        public void Restore(long lifetimeIncome, long contractsCompleted, long roadTilesTravelled, long railTilesTravelled)
        {
            LifetimeIncome = RequireNonNegative(lifetimeIncome, nameof(lifetimeIncome));
            ContractsCompleted = RequireNonNegative(contractsCompleted, nameof(contractsCompleted));
            RoadTilesTravelled = RequireNonNegative(roadTilesTravelled, nameof(roadTilesTravelled));
            RailTilesTravelled = RequireNonNegative(railTilesTravelled, nameof(railTilesTravelled));
        }

        /// <summary>
        /// Returns an independent copy.
        /// </summary>
        public CompanyStatistics Copy()
        {
            var copy = new CompanyStatistics();
            copy.Restore(LifetimeIncome, ContractsCompleted, RoadTilesTravelled, RailTilesTravelled);
            return copy;
        }

        private static long RequireNonNegative(long value, string name)
        {
            if (value < 0L)
            {
                throw new ArgumentOutOfRangeException(name, value, "A company counter cannot be negative.");
            }

            return value;
        }
    }
}
