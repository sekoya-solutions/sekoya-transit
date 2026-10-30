#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Bumps format 24 saves to 25 (Issue #223). Format 25 adds the lifetime company counters measured by the
    /// achievement tracks (lifetime income, contracts fulfilled, road and rail tiles travelled). Older saves start them
    /// at zero; completions of the pre-#223 achievement ids stay saved but no track counts them.
    /// </summary>
    public sealed class SaveFormatV24ToV25CompanyStatisticsMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion => 24;

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
