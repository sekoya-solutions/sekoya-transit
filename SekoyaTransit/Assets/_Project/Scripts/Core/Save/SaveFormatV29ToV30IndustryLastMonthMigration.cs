#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 29 saves to 30 (Issue #282). Format 30 stores each industry's production and delivered units of the
    /// previous month for the industries panel. Format 29 saves load them as zero until the next month ends. The tier
    /// (production level) and the stock capacity follow the new growth settings on the first tick: a level above the
    /// new maximum is capped and the streak is kept.
    /// </summary>
    public sealed class SaveFormatV29ToV30IndustryLastMonthMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 29;

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
