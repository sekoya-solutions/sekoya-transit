#nullable enable
using System;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Bumps format 18 saves to 19 with an empty permanent vehicle-unlock list.
    /// </summary>
    public sealed class SaveFormatV18ToV19VehicleUnlocksMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 18;

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
