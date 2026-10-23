#nullable enable
using System;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Adds an empty rail type channel when upgrading format version 9 saves to version 10.
    /// </summary>
    public sealed class SaveFormatV9ToV10RailChannelMigration : ISaveMigration
    {
        /// <inheritdoc />
        public int FromVersion
        {
            get
            {
                return 9;
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

            if (snapshot.HasRailTypeIds)
            {
                throw new SaveFormatException("Format version 9 snapshots must not already include a rail channel.");
            }

            var railTypeIds = new int[snapshot.TileCount];
            Array.Fill(railTypeIds, TileMap.NoRailTypeId);
            return snapshot.WithRailTypeIds(FromVersion + 1, railTypeIds);
        }
    }
}
