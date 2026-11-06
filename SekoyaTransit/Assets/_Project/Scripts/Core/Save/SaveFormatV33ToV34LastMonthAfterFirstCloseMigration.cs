#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 33 saves to 34 (Issue #324). Format 34 drops the full-month flag of routes, road vehicles and rail
    /// vehicles (added in format 31): an entity now publishes its last month at every month change, including a partial
    /// first month. The flag of an older save is ignored; its current month totals already count from the entity's
    /// creation, so the next month change publishes them.
    /// </summary>
    public sealed class SaveFormatV33ToV34LastMonthAfterFirstCloseMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 33;

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
