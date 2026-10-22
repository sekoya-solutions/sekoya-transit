#nullable enable
using System;

namespace TransportTycoon.Core.World
{
    /// <summary>
    /// Stores per-tile world data in flat arrays indexed in row-major order.
    /// </summary>
    public sealed class TileMap
    {
        public const int DefaultTerrainTypeId = 0;
        public const int NoObjectId = -1;
        public const int UnownedId = -1;
        public const int NoRoadTypeId = 0;
        public const int BasicRoadTypeId = 1;

        private readonly int[] _terrainTypeIds;
        private readonly int[] _objectIds;
        private readonly int[] _ownerIds;
        private readonly int[] _roadTypeIds;

        /// <summary>
        /// Initializes a square tile map of a supported size.
        /// </summary>
        /// <param name="size">The number of tiles along each side.</param>
        public TileMap(MapSize size)
        {
            int sideLength = (int)size;
            if (sideLength != 128 && sideLength != 256 && sideLength != 512)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, "Map size must be 128, 256, or 512 tiles per side.");
            }

            Size = size;
            Width = sideLength;
            Height = sideLength;
            TileCount = checked(Width * Height);

            _terrainTypeIds = new int[TileCount];
            _objectIds = new int[TileCount];
            _ownerIds = new int[TileCount];
            _roadTypeIds = new int[TileCount];
            Array.Fill(_objectIds, NoObjectId);
            Array.Fill(_ownerIds, UnownedId);
            Array.Fill(_roadTypeIds, NoRoadTypeId);
        }

        /// <summary>
        /// Gets the configured square map size.
        /// </summary>
        public MapSize Size { get; }

        /// <summary>
        /// Gets the map width in tiles.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the map height in tiles.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the number of tiles in the map.
        /// </summary>
        public int TileCount { get; }

        /// <summary>
        /// Gets the read-only row-major terrain type IDs.
        /// </summary>
        public ReadOnlySpan<int> TerrainTypeIds
        {
            get
            {
                return _terrainTypeIds;
            }
        }

        /// <summary>
        /// Gets the read-only row-major object or entity IDs.
        /// </summary>
        public ReadOnlySpan<int> ObjectIds
        {
            get
            {
                return _objectIds;
            }
        }

        /// <summary>
        /// Gets the read-only row-major owner IDs.
        /// </summary>
        public ReadOnlySpan<int> OwnerIds
        {
            get
            {
                return _ownerIds;
            }
        }

        /// <summary>
        /// Gets the read-only row-major road type IDs.
        /// </summary>
        public ReadOnlySpan<int> RoadTypeIds
        {
            get
            {
                return _roadTypeIds;
            }
        }

        /// <summary>
        /// Returns whether the coordinate lies inside the map.
        /// </summary>
        public bool Contains(TileCoordinate coordinate)
        {
            return coordinate.X >= 0 && coordinate.X < Width && coordinate.Y >= 0 && coordinate.Y < Height;
        }

        /// <summary>
        /// Converts a valid coordinate to its row-major tile ID.
        /// </summary>
        /// <param name="coordinate">The tile coordinate.</param>
        /// <returns>The tile ID, calculated as <c>y * width + x</c>.</returns>
        public int GetTileId(TileCoordinate coordinate)
        {
            ValidateCoordinate(coordinate);
            return checked((coordinate.Y * Width) + coordinate.X);
        }

        /// <summary>
        /// Converts a valid row-major tile ID to its coordinate.
        /// </summary>
        /// <param name="tileId">The tile ID.</param>
        /// <returns>The corresponding zero-based coordinate.</returns>
        public TileCoordinate GetCoordinate(int tileId)
        {
            if (tileId < 0 || tileId >= TileCount)
            {
                throw new ArgumentOutOfRangeException(nameof(tileId), tileId, "Tile ID is outside the map.");
            }

            return new TileCoordinate(tileId % Width, tileId / Width);
        }

        /// <summary>
        /// Gets the terrain type ID at a coordinate. ID zero is the default flat terrain.
        /// </summary>
        /// <param name="coordinate">The tile coordinate.</param>
        public int GetTerrainTypeId(TileCoordinate coordinate)
        {
            return _terrainTypeIds[GetTileId(coordinate)];
        }

        /// <summary>
        /// Sets the terrain type ID at a coordinate.
        /// </summary>
        /// <param name="coordinate">The tile coordinate.</param>
        /// <param name="terrainTypeId">A non-negative terrain type ID.</param>
        public void SetTerrainTypeId(TileCoordinate coordinate, int terrainTypeId)
        {
            if (terrainTypeId < DefaultTerrainTypeId)
            {
                throw new ArgumentOutOfRangeException(nameof(terrainTypeId), terrainTypeId, "Terrain type ID cannot be negative.");
            }

            _terrainTypeIds[GetTileId(coordinate)] = terrainTypeId;
        }

        /// <summary>
        /// Gets the object or entity ID at a coordinate, or <see cref="NoObjectId"/> when empty.
        /// </summary>
        /// <param name="coordinate">The tile coordinate.</param>
        public int GetObjectId(TileCoordinate coordinate)
        {
            return _objectIds[GetTileId(coordinate)];
        }

        /// <summary>
        /// Sets the object or entity ID at a coordinate.
        /// </summary>
        /// <param name="coordinate">The tile coordinate.</param>
        /// <param name="objectId">A non-negative ID or <see cref="NoObjectId"/> for an empty tile.</param>
        public void SetObjectId(TileCoordinate coordinate, int objectId)
        {
            if (objectId < NoObjectId)
            {
                throw new ArgumentOutOfRangeException(nameof(objectId), objectId, "Object ID must be non-negative or the empty sentinel.");
            }

            _objectIds[GetTileId(coordinate)] = objectId;
        }

        /// <summary>
        /// Gets the owner ID at a coordinate, or <see cref="UnownedId"/> when unowned.
        /// </summary>
        /// <param name="coordinate">The tile coordinate.</param>
        public int GetOwnerId(TileCoordinate coordinate)
        {
            return _ownerIds[GetTileId(coordinate)];
        }

        /// <summary>
        /// Sets the owner ID at a coordinate.
        /// </summary>
        /// <param name="coordinate">The tile coordinate.</param>
        /// <param name="ownerId">A non-negative ID or <see cref="UnownedId"/> for an unowned tile.</param>
        public void SetOwnerId(TileCoordinate coordinate, int ownerId)
        {
            if (ownerId < UnownedId)
            {
                throw new ArgumentOutOfRangeException(nameof(ownerId), ownerId, "Owner ID must be non-negative or the unowned sentinel.");
            }

            _ownerIds[GetTileId(coordinate)] = ownerId;
        }

        /// <summary>
        /// Gets the road type ID at a coordinate. <see cref="NoRoadTypeId"/> means no road.
        /// </summary>
        /// <param name="coordinate">The tile coordinate.</param>
        public int GetRoadTypeId(TileCoordinate coordinate)
        {
            return _roadTypeIds[GetTileId(coordinate)];
        }

        /// <summary>
        /// Sets the road type ID at a coordinate.
        /// </summary>
        /// <param name="coordinate">The tile coordinate.</param>
        /// <param name="roadTypeId">A non-negative road type ID; use <see cref="NoRoadTypeId"/> to clear.</param>
        public void SetRoadTypeId(TileCoordinate coordinate, int roadTypeId)
        {
            if (roadTypeId < NoRoadTypeId)
            {
                throw new ArgumentOutOfRangeException(nameof(roadTypeId), roadTypeId, "Road type ID cannot be negative.");
            }

            _roadTypeIds[GetTileId(coordinate)] = roadTypeId;
        }

        private void ValidateCoordinate(TileCoordinate coordinate)
        {
            if (!Contains(coordinate))
            {
                throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate, "Coordinate is outside the map.");
            }
        }
    }
}
