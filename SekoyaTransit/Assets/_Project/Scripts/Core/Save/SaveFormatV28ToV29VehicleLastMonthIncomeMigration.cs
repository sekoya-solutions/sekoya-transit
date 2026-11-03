#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 28 saves to 29 (Issue #280). Format 29 stores each vehicle's delivery income of the current and the
    /// previous month for the vehicles panel. Format 28 saves load them as zero with an unknown last month, so the panel
    /// shows "not available yet" until the next month ends.
    /// </summary>
    public sealed class SaveFormatV28ToV29VehicleLastMonthIncomeMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 28;

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
