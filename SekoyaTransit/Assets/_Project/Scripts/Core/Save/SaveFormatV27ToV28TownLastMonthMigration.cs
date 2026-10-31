#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 27 saves to 28 (Issue #242). Format 28 stores each town need's supply of the previous month and
    /// each town's growth of the previous month for the towns panel. Format 27 saves load them as zero, so the panel
    /// shows nothing supplied last month until the next month ends.
    /// </summary>
    public sealed class SaveFormatV27ToV28TownLastMonthMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 27;

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
