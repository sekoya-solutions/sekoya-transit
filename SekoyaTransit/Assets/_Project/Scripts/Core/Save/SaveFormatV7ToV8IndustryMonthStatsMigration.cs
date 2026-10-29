#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Upgrades format version 7 saves to version 8 (per-industry calendar-month produced/transported).
    /// Existing industries keep counters at zero and unset stats month until production runs.
    /// </summary>
    public sealed class SaveFormatV7ToV8IndustryMonthStatsMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 7;
            }
        }

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
