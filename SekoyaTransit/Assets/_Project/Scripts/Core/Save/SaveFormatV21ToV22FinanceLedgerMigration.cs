#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 21 saves to 22 with an empty finance ledger. The ledger starts on the first tick after load,
    /// when it records the calendar year.
    /// </summary>
    public sealed class SaveFormatV21ToV22FinanceLedgerMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 21;

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
