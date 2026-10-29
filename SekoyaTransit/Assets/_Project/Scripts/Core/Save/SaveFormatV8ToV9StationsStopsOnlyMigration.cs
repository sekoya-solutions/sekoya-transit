#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Upgrades format version 8 saves to version 9 (stations as stops only; no station cargo;
    /// industry load round-robin cursor defaults to 0).
    /// </summary>
    public sealed class SaveFormatV8ToV9StationsStopsOnlyMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 8;
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

            // Station cargo is dropped by reconstructing RoadStation without bags;
            // industry LoadRoundRobinIndex stays 0 via WithFormatVersion industry copy rules.
            return snapshot.WithFormatVersion(FromVersion + 1);
        }
    }
}
