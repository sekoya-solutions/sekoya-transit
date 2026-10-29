#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Adds town passenger waiting cargo and vehicle passenger-origin town ids when upgrading
    /// format version 14 to 15. Migrated towns keep empty waiting bags; vehicle origins stay unset.
    /// </summary>
    public sealed class SaveFormatV14ToV15PassengerMailMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 14;
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

            // Serializer/runtime already treat missing waiting cargo and passenger origin as empty/unset.
            return snapshot.WithFormatVersion(FromVersion + 1);
        }
    }
}
