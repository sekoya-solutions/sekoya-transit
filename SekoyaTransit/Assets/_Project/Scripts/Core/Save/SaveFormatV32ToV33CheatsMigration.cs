#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 32 saves to 33 (Issue #316). Format 33 adds the cheats finance category and the lifted vehicle year
    /// gate. Format 32 saves load with no cheat income and the year gate in place.
    /// </summary>
    public sealed class SaveFormatV32ToV33CheatsMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 32;

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
