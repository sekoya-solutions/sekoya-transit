#nullable enable
using System;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// Applies persisted map channels from a <see cref="SaveSnapshot"/> onto a live world.
    /// </summary>
    public static class WorldSave
    {
        /// <summary>
        /// Copies road and rail tile channels from the snapshot when present.
        /// </summary>
        public static void ApplyTransportTileChannels(TileMap map, SaveSnapshot snapshot)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
#pragma warning restore CA1510

            if (map.Size != snapshot.MapSize)
            {
                throw new ArgumentException("Snapshot map size does not match the target map.", nameof(snapshot));
            }

            if (snapshot.HasRoadTypeIds)
            {
                map.CopyRoadTypeIdsFrom(snapshot.RoadTypeIds);
            }

            if (snapshot.HasRailTypeIds)
            {
                map.CopyRailTypeIdsFrom(snapshot.RailTypeIds);
            }
        }

        /// <summary>
        /// Rebuilds road and rail graphs from the map after transport channels were applied.
        /// </summary>
        public static void RebuildTransportGraphs(GeneratedWorld world)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            world.RoadGraph.RebuildFromMap(world.Map);
            world.RailGraph.RebuildFromMap(world.Map);
        }

        /// <summary>
        /// Applies transport tile channels and rebuilds derived graphs.
        /// </summary>
        public static void ApplyTransportChannels(GeneratedWorld world, SaveSnapshot snapshot)
        {
            ApplyTransportTileChannels(world.Map, snapshot);
            RebuildTransportGraphs(world);
        }
    }
}
