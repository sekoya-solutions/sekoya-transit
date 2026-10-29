#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 23 saves to 24 (Issue #73). Format 24 adds the station tile where each vehicle load started and
    /// the unpaid delivery pay fraction (distance-based pay), plus each industry production level and well-served month
    /// streak (primary industry growth). Older saves keep the defaults: an unknown load origin pays the minimum
    /// distance once, no pay fraction, and every industry at the base production level with no streak.
    /// </summary>
    public sealed class SaveFormatV23ToV24DeliveryPayIndustryGrowthMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 23;

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
