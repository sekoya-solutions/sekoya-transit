#nullable enable

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Factory for the production save migration pipeline at <see cref="SaveSnapshot.CurrentFormatVersion"/>.
    /// </summary>
    public static class CurrentSaveMigrations
    {
        /// <summary>
        /// Creates the migration pipeline that loads archived formats up to the current version.
        /// </summary>
        public static SaveMigrationPipeline CreatePipeline()
        {
            return new SaveMigrationPipeline(
                SaveSnapshot.CurrentFormatVersion,
                new ISaveMigration[]
                {
                    new SaveFormatV1ToV2RoadChannelMigration(),
                    new SaveFormatV2ToV3RoadStationsMigration(),
                    new SaveFormatV3ToV4RoadVehiclesMigration(),
                    new SaveFormatV4ToV5CargoMigration(),
                    new SaveFormatV5ToV6EconomyProductionMigration(),
                    new SaveFormatV6ToV7VehicleLifetimeIncomeMigration(),
                    new SaveFormatV7ToV8IndustryMonthStatsMigration(),
                    new SaveFormatV8ToV9StationsStopsOnlyMigration(),
                    new SaveFormatV9ToV10RailChannelMigration(),
                    new SaveFormatV10ToV11RailStationsVehiclesMigration(),
                    new SaveFormatV11ToV12RailWagonCountMigration(),
                    new SaveFormatV12ToV13TownPopulationMigration(),
                    new SaveFormatV13ToV14TownSupplyGrowthMigration(),
                    new SaveFormatV14ToV15PassengerMailMigration()
                });
        }
    }
}
