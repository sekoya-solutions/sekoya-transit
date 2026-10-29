#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Upgrades format version 4 saves to version 5 (cargo inventories and wait-for-full orders).
    /// Version-4 entities deserialize with empty cargo and wait-for-full false; this step bumps
    /// the format version so subsequent loads persist those fields explicitly.
    /// </summary>
    public sealed class SaveFormatV4ToV5CargoMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 4;
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

            if (!snapshot.HasRoadVehicles)
            {
                throw new SaveFormatException("Format version 4 snapshots must include a road-vehicle list.");
            }

            return snapshot.WithFormatVersion(FromVersion + 1);
        }
    }
}
