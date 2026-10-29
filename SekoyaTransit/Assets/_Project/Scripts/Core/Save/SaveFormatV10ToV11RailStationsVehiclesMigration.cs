#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Adds empty rail-station and rail-vehicle lists when upgrading format version 10 saves to version 11.
    /// </summary>
    public sealed class SaveFormatV10ToV11RailStationsVehiclesMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 10;
            }
        }

        /// <inheritdoc />
        public SaveSnapshot Migrate(SaveSnapshot snapshot)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
#pragma warning restore CA1510

            if (snapshot.FormatVersion != FromVersion)
            {
                throw new SaveFormatException(
                    $"Migration from version {FromVersion} received format version {snapshot.FormatVersion}.");
            }

            if (snapshot.HasRailStations || snapshot.HasRailVehicles)
            {
                throw new SaveFormatException(
                    "Format version 10 snapshots must not already include rail stations or rail vehicles.");
            }

            return snapshot.WithRailStationsAndVehicles(
                FromVersion + 1,
                Array.Empty<RailStation>(),
                Array.Empty<RailVehicle>());
        }
    }
}
