#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Adds town passenger/mail month Generated/Transported counters when upgrading
    /// format version 15 to 16. Migrated towns keep zero counters.
    /// </summary>
    public sealed class SaveFormatV15ToV16TownPassengerMonthStatsMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 15;
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

            // Serializer/runtime already treat missing month passenger/mail counters as zero.
            return snapshot.WithFormatVersion(FromVersion + 1);
        }
    }
}
