#nullable enable
using System;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Adds town population and needs when upgrading format version 12 saves to version 13.
    /// Migrated towns receive <see cref="World.GeneratedWorld.MigratedTownPopulation"/> and empty needs.
    /// </summary>
    public sealed class SaveFormatV12ToV13TownPopulationMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 12;
            }
        }

        /// <inheritdoc />
        public SaveSnapshot Migrate(SaveSnapshot snapshot)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
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

            // Constructor supplies migrated placeholder population and empty needs when towns are omitted.
            return snapshot.WithFormatVersion(FromVersion + 1);
        }
    }
}
