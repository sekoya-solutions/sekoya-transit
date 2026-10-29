#nullable enable
using System;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Upgrades format version 5 saves to version 6 (company cash and industry cargo).
    /// </summary>
    public sealed class SaveFormatV5ToV6EconomyProductionMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 5;
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

            return snapshot.WithCompanyCash(FromVersion + 1, GeneratedWorld.PlaceholderStartingCash);
        }
    }
}
