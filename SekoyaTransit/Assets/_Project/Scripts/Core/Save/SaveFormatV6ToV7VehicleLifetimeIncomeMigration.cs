#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Upgrades format version 6 saves to version 7 (per-vehicle lifetime delivery income).
    /// Existing vehicles keep income at zero when the field was absent.
    /// </summary>
    public sealed class SaveFormatV6ToV7VehicleLifetimeIncomeMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 6;
            }
        }

        /// <inheritdoc />
        public SaveSnapshot Migrate(SaveSnapshot snapshot)
        {
#pragma warning disable CA1510
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
