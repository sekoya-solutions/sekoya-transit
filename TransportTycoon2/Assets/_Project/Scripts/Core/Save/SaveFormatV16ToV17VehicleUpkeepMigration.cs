#nullable enable
using System;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Adds last vehicle-upkeep calendar year/month when upgrading format version 16 to 17.
    /// Migrated worlds keep unset 0/0 so the next month observation records without charging.
    /// </summary>
    public sealed class SaveFormatV16ToV17VehicleUpkeepMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 16;
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

            return snapshot.WithFormatVersion(FromVersion + 1);
        }
    }
}
