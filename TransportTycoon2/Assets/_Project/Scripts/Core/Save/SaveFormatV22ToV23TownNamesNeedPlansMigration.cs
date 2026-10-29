#nullable enable
using System;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Bumps format 22 saves to 23 (Issue #193). Format 23 adds town names, town need plans, and the hidden industry
    /// input leftover. Migration has no content catalog, so it leaves them empty: restore names the towns and rebuilds
    /// their need plans from content, and moves input cargo that older saves kept in industry stock into the leftover.
    /// </summary>
    public sealed class SaveFormatV22ToV23TownNamesNeedPlansMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 22;

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
