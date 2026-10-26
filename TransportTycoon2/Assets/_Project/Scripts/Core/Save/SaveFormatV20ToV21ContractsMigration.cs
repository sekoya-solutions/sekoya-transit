#nullable enable
using System;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Bumps format 20 saves to 21 with no open contracts, the first contract id, and a contract generator
    /// freshly seeded from the world seed. The first tick after load tops up open contracts.
    /// </summary>
    public sealed class SaveFormatV20ToV21ContractsMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 20;

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
