#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TransportTycoon.Core.Simulation;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Save
{
    /// <summary>
    /// An immutable, versioned snapshot of the currently implemented simulation state.
    /// </summary>
    public sealed class SaveSnapshot
    {
        public const int CurrentFormatVersion = 1;

        private readonly int[] _terrainTypeIds;
        private readonly int[] _objectIds;
        private readonly int[] _ownerIds;
        private readonly ReadOnlyCollection<TileCoordinate> _townAnchors;
        private readonly ReadOnlyCollection<PlacedIndustry> _industries;

        /// <summary>
        /// Initializes a versioned snapshot and copies all supplied collections.
        /// </summary>
        public SaveSnapshot(
            int formatVersion,
            ulong mapSeed,
            Tick currentTick,
            MapSize mapSize,
            IReadOnlyList<int> terrainTypeIds,
            IReadOnlyList<int> objectIds,
            IReadOnlyList<int> ownerIds,
            IReadOnlyList<TileCoordinate> townAnchors,
            IReadOnlyList<PlacedIndustry> industries)
        {
            if (formatVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(formatVersion), formatVersion, "Format version must be positive.");
            }

#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (terrainTypeIds == null)
            {
                throw new ArgumentNullException(nameof(terrainTypeIds));
            }

            if (objectIds == null)
            {
                throw new ArgumentNullException(nameof(objectIds));
            }

            if (ownerIds == null)
            {
                throw new ArgumentNullException(nameof(ownerIds));
            }

            if (townAnchors == null)
            {
                throw new ArgumentNullException(nameof(townAnchors));
            }

            if (industries == null)
            {
                throw new ArgumentNullException(nameof(industries));
            }
#pragma warning restore CA1510

            int sideLength = (int)mapSize;
            if (sideLength != 128 && sideLength != 256 && sideLength != 512)
            {
                throw new ArgumentOutOfRangeException(nameof(mapSize), mapSize, "Map size must be 128, 256, or 512 tiles per side.");
            }

            int tileCount = checked(sideLength * sideLength);
            if (terrainTypeIds.Count != tileCount || objectIds.Count != tileCount || ownerIds.Count != tileCount)
            {
                throw new ArgumentException("Every tile channel must contain exactly one value per map tile.");
            }

            _terrainTypeIds = CopyTileChannel(terrainTypeIds, nameof(terrainTypeIds), false);
            _objectIds = CopyTileChannel(objectIds, nameof(objectIds), true);
            _ownerIds = CopyTileChannel(ownerIds, nameof(ownerIds), true);

            var townCopy = new TileCoordinate[townAnchors.Count];
            for (int index = 0; index < townAnchors.Count; index++)
            {
                TileCoordinate coordinate = townAnchors[index];
                ValidateCoordinate(coordinate, sideLength, nameof(townAnchors));
                if (_objectIds[(coordinate.Y * sideLength) + coordinate.X] != index)
                {
                    throw new ArgumentException(
                        $"Town anchor {index} does not match its map object ID.",
                        nameof(townAnchors));
                }

                townCopy[index] = coordinate;
            }

            var industryCopy = new PlacedIndustry[industries.Count];
            for (int index = 0; index < industries.Count; index++)
            {
                PlacedIndustry industry = industries[index];
                if (string.IsNullOrWhiteSpace(industry.IndustryId))
                {
                    throw new ArgumentException($"Industry placement {index} has no definition ID.", nameof(industries));
                }

                if (industry.InstanceIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(industries), "Industry instance indices cannot be negative.");
                }

                ValidateCoordinate(industry.Coordinate, sideLength, nameof(industries));
                int expectedObjectId = checked(townCopy.Length + index);
                int tileId = (industry.Coordinate.Y * sideLength) + industry.Coordinate.X;
                if (_objectIds[tileId] != expectedObjectId)
                {
                    throw new ArgumentException(
                        $"Industry placement {index} does not match its map object ID.",
                        nameof(industries));
                }

                industryCopy[index] = industry;
            }

            FormatVersion = formatVersion;
            MapSeed = mapSeed;
            CurrentTick = currentTick;
            MapSize = mapSize;
            TileCount = tileCount;
            _townAnchors = Array.AsReadOnly(townCopy);
            _industries = Array.AsReadOnly(industryCopy);
        }

        /// <summary>
        /// Gets the serialized format version.
        /// </summary>
        public int FormatVersion { get; }

        /// <summary>
        /// Gets the seed used to generate the world.
        /// </summary>
        public ulong MapSeed { get; }

        /// <summary>
        /// Gets the most recently completed simulation tick.
        /// </summary>
        public Tick CurrentTick { get; }

        /// <summary>
        /// Gets the square map size.
        /// </summary>
        public MapSize MapSize { get; }

        /// <summary>
        /// Gets the number of tiles in the map.
        /// </summary>
        public int TileCount { get; }

        /// <summary>
        /// Gets the row-major terrain type IDs.
        /// </summary>
        public ReadOnlySpan<int> TerrainTypeIds
        {
            get
            {
                return _terrainTypeIds;
            }
        }

        /// <summary>
        /// Gets the row-major object IDs.
        /// </summary>
        public ReadOnlySpan<int> ObjectIds
        {
            get
            {
                return _objectIds;
            }
        }

        /// <summary>
        /// Gets the row-major owner IDs.
        /// </summary>
        public ReadOnlySpan<int> OwnerIds
        {
            get
            {
                return _ownerIds;
            }
        }

        /// <summary>
        /// Gets town anchors in placement order.
        /// </summary>
        public IReadOnlyList<TileCoordinate> TownAnchors
        {
            get
            {
                return _townAnchors;
            }
        }

        /// <summary>
        /// Gets industry placements in placement order.
        /// </summary>
        public IReadOnlyList<PlacedIndustry> Industries
        {
            get
            {
                return _industries;
            }
        }

        /// <summary>
        /// Creates a snapshot of a generated world.
        /// </summary>
        public static SaveSnapshot Capture(ulong mapSeed, Tick currentTick, GeneratedWorld world)
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            int[] terrainTypeIds = new int[world.Map.TileCount];
            int[] objectIds = new int[world.Map.TileCount];
            int[] ownerIds = new int[world.Map.TileCount];
            world.Map.TerrainTypeIds.CopyTo(terrainTypeIds);
            world.Map.ObjectIds.CopyTo(objectIds);
            world.Map.OwnerIds.CopyTo(ownerIds);

            return new SaveSnapshot(
                CurrentFormatVersion,
                mapSeed,
                currentTick,
                world.Map.Size,
                terrainTypeIds,
                objectIds,
                ownerIds,
                world.TownAnchors,
                world.Industries);
        }

        /// <summary>
        /// Copies this snapshot with a new format version for a migration step.
        /// </summary>
        public SaveSnapshot WithFormatVersion(int formatVersion)
        {
            return new SaveSnapshot(
                formatVersion,
                MapSeed,
                CurrentTick,
                MapSize,
                _terrainTypeIds,
                _objectIds,
                _ownerIds,
                _townAnchors,
                _industries);
        }

        private static int[] CopyTileChannel(IReadOnlyList<int> values, string parameterName, bool allowEmpty)
        {
            var copy = new int[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                int value = values[index];
                if ((allowEmpty && value < TileMap.NoObjectId) ||
                    (!allowEmpty && value < TileMap.DefaultTerrainTypeId))
                {
                    throw new ArgumentOutOfRangeException(parameterName, value, "Tile channel value is outside its valid range.");
                }

                copy[index] = value;
            }

            return copy;
        }

        private static void ValidateCoordinate(TileCoordinate coordinate, int sideLength, string parameterName)
        {
            if (coordinate.X < 0 || coordinate.X >= sideLength || coordinate.Y < 0 || coordinate.Y >= sideLength)
            {
                throw new ArgumentOutOfRangeException(parameterName, coordinate, "Snapshot coordinate is outside the map.");
            }
        }
    }
}
