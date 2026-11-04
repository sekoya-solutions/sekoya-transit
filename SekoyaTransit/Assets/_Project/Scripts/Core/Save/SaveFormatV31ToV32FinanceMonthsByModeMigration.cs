#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 31 saves to 32 (Issue #296). Format 32 stores the finance ledger per vehicle mode and adds the
    /// current and previous month. Format 31 saves load their yearly amounts as general (no vehicle mode) and start with
    /// no monthly data: the first month is partial and the last month is "not available yet" until a full month closes.
    /// </summary>
    public sealed class SaveFormatV31ToV32FinanceMonthsByModeMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 31;

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
