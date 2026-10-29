#nullable enable
using System;
using System.Collections.Generic;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// Migrates a save snapshot from one format version to the next.
    /// </summary>
    public interface ISaveMigration
    {
        /// <summary>
        /// Gets the format version accepted by this migration.
        /// </summary>
        int FromVersion { get; }

        /// <summary>
        /// Creates a snapshot at exactly <see cref="FromVersion"/> plus one.
        /// </summary>
        SaveSnapshot Migrate(SaveSnapshot snapshot);
    }

    /// <summary>
    /// Applies a registered sequence of one-version-at-a-time save migrations.
    /// </summary>
    public sealed class SaveMigrationPipeline
    {
        private readonly Dictionary<int, ISaveMigration> _migrationsByVersion;

        /// <summary>
        /// Initializes a migration pipeline for the current save format.
        /// </summary>
        public SaveMigrationPipeline(int currentVersion, IReadOnlyList<ISaveMigration> migrations)
        {
            if (currentVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(currentVersion), currentVersion, "Current version must be positive.");
            }

#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (migrations == null)
            {
                throw new ArgumentNullException(nameof(migrations));
            }
#pragma warning restore CA1510

            _migrationsByVersion = new Dictionary<int, ISaveMigration>();
            for (int index = 0; index < migrations.Count; index++)
            {
                ISaveMigration migration = migrations[index] ??
                    throw new ArgumentException("Migration entries cannot be null.", nameof(migrations));
                if (migration.FromVersion < 1 || migration.FromVersion >= currentVersion)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(migrations),
                        migration.FromVersion,
                        "Migration source version must be positive and older than the current version.");
                }

                if (!_migrationsByVersion.TryAdd(migration.FromVersion, migration))
                {
                    throw new ArgumentException(
                        $"More than one migration starts at version {migration.FromVersion}.",
                        nameof(migrations));
                }
            }

            CurrentVersion = currentVersion;
        }

        /// <summary>
        /// Gets the version all successful migrations produce.
        /// </summary>
        public int CurrentVersion { get; }

        /// <summary>
        /// Migrates a snapshot to the configured current version.
        /// </summary>
        public SaveSnapshot Migrate(SaveSnapshot snapshot)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
#pragma warning restore CA1510

            if (snapshot.FormatVersion > CurrentVersion)
            {
                throw new SaveFormatException(
                    $"Save format version {snapshot.FormatVersion} is newer than supported version {CurrentVersion}.");
            }

            SaveSnapshot current = snapshot;
            while (current.FormatVersion < CurrentVersion)
            {
                if (!_migrationsByVersion.TryGetValue(current.FormatVersion, out ISaveMigration? migration))
                {
                    throw new SaveFormatException(
                        $"No save migration is registered from format version {current.FormatVersion}.");
                }

                SaveSnapshot migrated = migration.Migrate(current);
                if (migrated == null || migrated.FormatVersion != current.FormatVersion + 1)
                {
                    throw new SaveFormatException(
                        $"Migration from format version {current.FormatVersion} must produce version {current.FormatVersion + 1}.");
                }

                current = migrated;
            }

            return current;
        }
    }
}
