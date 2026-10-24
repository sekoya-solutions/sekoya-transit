#nullable enable
using System;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Adds town need monthly supply and stats month bookkeeping when upgrading format version 13 to 14.
    /// Migrated towns keep population and need cargo ids; supply counters and stats month stay unset/zero.
    /// </summary>
    public sealed class SaveFormatV13ToV14TownSupplyGrowthMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 13;
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

            // Serializer/runtime already treat missing monthSupply and stats as zero/unset.
            return snapshot.WithFormatVersion(FromVersion + 1);
        }
    }
}
