#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 30 saves to 31 (Issue #294). Format 31 stores whether each route and vehicle existed since the start
    /// of the current month, so a month closed after a mid-month creation does not publish partial last-month figures.
    /// Format 30 saves load the flag as "the last month is known": an entity that already closed a month existed for it,
    /// and one that has not is treated as created mid-month, so it waits for its first full month. Format 34 drops the
    /// flag again (Issue #324).
    /// </summary>
    public sealed class SaveFormatV30ToV31FullMonthFiguresMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 30;

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
