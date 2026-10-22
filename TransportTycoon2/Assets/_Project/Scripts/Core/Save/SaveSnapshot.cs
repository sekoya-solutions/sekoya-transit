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
        public const int CurrentFormatVersion = 5;

        private readonly int[] _terrainTypeIds;
        private readonly int[] _objectIds;
        private readonly int[] _ownerIds;
        private readonly int[] _roadTypeIds;
        private readonly ReadOnlyCollection<TileCoordinate> _townAnchors;
        private readonly ReadOnlyCollection<PlacedIndustry> _industries;
        private readonly ReadOnlyCollection<RoadStation> _roadStations;
        private readonly ReadOnlyCollection<RoadVehicle> _roadVehicles;

        /// <summary>
        /// Initializes a versioned snapshot and copies all supplied collections.
        /// Format version 1 has no road channel (<paramref name="roadTypeIds"/> must be <see langword="null"/>).
        /// Format version 2+ requires a road channel with one value per tile.
        /// Format version 3+ requires a road-station list (<paramref name="roadStations"/>; may be empty).
        /// Format versions below 3 must pass <see langword="null"/> for <paramref name="roadStations"/>.
        /// Format version 4+ requires a road-vehicle list (<paramref name="roadVehicles"/>; may be empty).
        /// Format versions below 4 must pass <see langword="null"/> for <paramref name="roadVehicles"/>.
        /// Format version 5+ persists station/vehicle cargo and per-order wait-for-full flags on those entities.
        /// </summary>
        public SaveSnapshot(
            int formatVersion,
            ulong mapSeed,
            Tick currentTick,
            MapSize mapSize,
            IReadOnlyList<int> terrainTypeIds,
            IReadOnlyList<int> objectIds,
            IReadOnlyList<int> ownerIds,
            IReadOnlyList<int>? roadTypeIds,
            IReadOnlyList<TileCoordinate> townAnchors,
            IReadOnlyList<PlacedIndustry> industries,
            IReadOnlyList<RoadStation>? roadStations = null,
            IReadOnlyList<RoadVehicle>? roadVehicles = null)
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

            if (formatVersion >= 2)
            {
                if (roadTypeIds == null)
                {
                    throw new ArgumentNullException(nameof(roadTypeIds), "Format version 2+ requires a road type channel.");
                }

                if (roadTypeIds.Count != tileCount)
                {
                    throw new ArgumentException("Every tile channel must contain exactly one value per map tile.", nameof(roadTypeIds));
                }

                _roadTypeIds = CopyTileChannel(roadTypeIds, nameof(roadTypeIds), false);
            }
            else
            {
                if (roadTypeIds != null)
                {
                    throw new ArgumentException("Format version 1 snapshots must not include a road type channel.", nameof(roadTypeIds));
                }

                _roadTypeIds = Array.Empty<int>();
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

            if (formatVersion >= 3)
            {
                if (roadStations == null)
                {
                    throw new ArgumentNullException(nameof(roadStations), "Format version 3+ requires a road station list.");
                }

                _roadStations = Array.AsReadOnly(CopyRoadStations(roadStations, sideLength, _objectIds, _roadTypeIds));
            }
            else
            {
                if (roadStations != null)
                {
                    throw new ArgumentException("Format versions below 3 must not include road stations.", nameof(roadStations));
                }

                _roadStations = Array.AsReadOnly(Array.Empty<RoadStation>());
            }

            if (formatVersion >= 4)
            {
                if (roadVehicles == null)
                {
                    throw new ArgumentNullException(nameof(roadVehicles), "Format version 4+ requires a road vehicle list.");
                }

                _roadVehicles = Array.AsReadOnly(CopyRoadVehicles(roadVehicles));
            }
            else
            {
                if (roadVehicles != null)
                {
                    throw new ArgumentException("Format versions below 4 must not include road vehicles.", nameof(roadVehicles));
                }

                _roadVehicles = Array.AsReadOnly(Array.Empty<RoadVehicle>());
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
        /// Gets whether this snapshot includes a road type channel (format version 2+).
        /// </summary>
        public bool HasRoadTypeIds
        {
            get
            {
                return _roadTypeIds.Length > 0;
            }
        }

        /// <summary>
        /// Gets whether this snapshot includes the format version 3+ road station list.
        /// </summary>
        public bool HasRoadStations
        {
            get
            {
                return FormatVersion >= 3;
            }
        }

        /// <summary>
        /// Gets whether this snapshot includes the format version 4+ road vehicle list.
        /// </summary>
        public bool HasRoadVehicles
        {
            get
            {
                return FormatVersion >= 4;
            }
        }

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
        /// Gets the row-major road type IDs. Empty when <see cref="HasRoadTypeIds"/> is false.
        /// </summary>
        public ReadOnlySpan<int> RoadTypeIds
        {
            get
            {
                return _roadTypeIds;
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
        /// Gets road stations in ascending id order. Empty when <see cref="HasRoadStations"/> is false.
        /// </summary>
        public IReadOnlyList<RoadStation> RoadStations
        {
            get
            {
                return _roadStations;
            }
        }

        /// <summary>
        /// Gets road vehicles in ascending id order. Empty when <see cref="HasRoadVehicles"/> is false.
        /// </summary>
        public IReadOnlyList<RoadVehicle> RoadVehicles
        {
            get
            {
                return _roadVehicles;
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
            int[] roadTypeIds = new int[world.Map.TileCount];
            world.Map.TerrainTypeIds.CopyTo(terrainTypeIds);
            world.Map.ObjectIds.CopyTo(objectIds);
            world.Map.OwnerIds.CopyTo(ownerIds);
            world.Map.RoadTypeIds.CopyTo(roadTypeIds);

            return new SaveSnapshot(
                CurrentFormatVersion,
                mapSeed,
                currentTick,
                world.Map.Size,
                terrainTypeIds,
                objectIds,
                ownerIds,
                roadTypeIds,
                world.TownAnchors,
                world.Industries,
                world.RoadStations,
                world.RoadVehicles);
        }

        /// <summary>
        /// Copies this snapshot with a new format version for a same-channel migration step.
        /// When upgrading to format version 3+, an empty road-station list is supplied if absent.
        /// When upgrading to format version 4+, an empty road-vehicle list is supplied if absent.
        /// </summary>
        public SaveSnapshot WithFormatVersion(int formatVersion)
        {
            IReadOnlyList<int>? roadTypeIds = HasRoadTypeIds ? (IReadOnlyList<int>)_roadTypeIds : null;
            IReadOnlyList<RoadStation>? roadStations = null;
            if (formatVersion >= 3)
            {
                roadStations = _roadStations;
            }

            IReadOnlyList<RoadVehicle>? roadVehicles = null;
            if (formatVersion >= 4)
            {
                roadVehicles = _roadVehicles;
            }

            return new SaveSnapshot(
                formatVersion,
                MapSeed,
                CurrentTick,
                MapSize,
                _terrainTypeIds,
                _objectIds,
                _ownerIds,
                roadTypeIds,
                _townAnchors,
                _industries,
                roadStations,
                roadVehicles);
        }

        /// <summary>
        /// Copies this snapshot and adds or replaces the road type channel for a format upgrade.
        /// </summary>
        public SaveSnapshot WithRoadTypeIds(int formatVersion, IReadOnlyList<int> roadTypeIds)
        {
            IReadOnlyList<RoadStation>? roadStations = null;
            if (formatVersion >= 3)
            {
                roadStations = _roadStations;
            }

            IReadOnlyList<RoadVehicle>? roadVehicles = null;
            if (formatVersion >= 4)
            {
                roadVehicles = _roadVehicles;
            }

            return new SaveSnapshot(
                formatVersion,
                MapSeed,
                CurrentTick,
                MapSize,
                _terrainTypeIds,
                _objectIds,
                _ownerIds,
                roadTypeIds,
                _townAnchors,
                _industries,
                roadStations,
                roadVehicles);
        }

        /// <summary>
        /// Copies this snapshot and adds or replaces the road station list for a format upgrade.
        /// </summary>
        public SaveSnapshot WithRoadStations(int formatVersion, IReadOnlyList<RoadStation> roadStations)
        {
            IReadOnlyList<int>? roadTypeIds = HasRoadTypeIds ? (IReadOnlyList<int>)_roadTypeIds : null;
            IReadOnlyList<RoadVehicle>? roadVehicles = null;
            if (formatVersion >= 4)
            {
                roadVehicles = _roadVehicles;
            }

            return new SaveSnapshot(
                formatVersion,
                MapSeed,
                CurrentTick,
                MapSize,
                _terrainTypeIds,
                _objectIds,
                _ownerIds,
                roadTypeIds,
                _townAnchors,
                _industries,
                roadStations,
                roadVehicles);
        }

        /// <summary>
        /// Copies this snapshot and adds or replaces the road vehicle list for a format upgrade.
        /// </summary>
        public SaveSnapshot WithRoadVehicles(int formatVersion, IReadOnlyList<RoadVehicle> roadVehicles)
        {
            IReadOnlyList<int>? roadTypeIds = HasRoadTypeIds ? (IReadOnlyList<int>)_roadTypeIds : null;
            return new SaveSnapshot(
                formatVersion,
                MapSeed,
                CurrentTick,
                MapSize,
                _terrainTypeIds,
                _objectIds,
                _ownerIds,
                roadTypeIds,
                _townAnchors,
                _industries,
                _roadStations,
                roadVehicles);
        }

        private static RoadStation[] CopyRoadStations(
            IReadOnlyList<RoadStation> roadStations,
            int sideLength,
            int[] objectIds,
            int[] roadTypeIds)
        {
            var copy = new RoadStation[roadStations.Count];
            var seenIds = new HashSet<int>();
            var seenTiles = new HashSet<int>();
            for (int index = 0; index < roadStations.Count; index++)
            {
                RoadStation station = roadStations[index];
#pragma warning disable CA1510
                if (station == null)
                {
                    throw new ArgumentNullException(nameof(roadStations), $"Road station at index {index} is null.");
                }
#pragma warning restore CA1510

                if (station.Id < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(roadStations), "Station ids cannot be negative.");
                }

                if (!seenIds.Add(station.Id))
                {
                    throw new ArgumentException($"Duplicate road station id {station.Id}.", nameof(roadStations));
                }

                ValidateCoordinate(station.Coordinate, sideLength, nameof(roadStations));
                int tileId = (station.Coordinate.Y * sideLength) + station.Coordinate.X;
                if (!seenTiles.Add(tileId))
                {
                    throw new ArgumentException(
                        $"Duplicate road station tile ({station.Coordinate.X}, {station.Coordinate.Y}).",
                        nameof(roadStations));
                }

                if (objectIds[tileId] != TileMap.NoObjectId)
                {
                    throw new ArgumentException(
                        $"Road station {station.Id} overlaps a town or industry tile.",
                        nameof(roadStations));
                }

                if (roadTypeIds.Length > 0 && roadTypeIds[tileId] != TileMap.NoRoadTypeId)
                {
                    throw new ArgumentException(
                        $"Road station {station.Id} overlaps a road tile.",
                        nameof(roadStations));
                }

                copy[index] = new RoadStation(station.Id, station.Coordinate, station.Cargo.Stacks);
            }

            Array.Sort(copy, CompareStationsById);
            return copy;
        }

        private static int CompareStationsById(RoadStation left, RoadStation right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private static RoadVehicle[] CopyRoadVehicles(IReadOnlyList<RoadVehicle> roadVehicles)
        {
            var copy = new RoadVehicle[roadVehicles.Count];
            var seenIds = new HashSet<int>();
            for (int index = 0; index < roadVehicles.Count; index++)
            {
                RoadVehicle vehicle = roadVehicles[index];
#pragma warning disable CA1510
                if (vehicle == null)
                {
                    throw new ArgumentNullException(nameof(roadVehicles), $"Road vehicle at index {index} is null.");
                }
#pragma warning restore CA1510

                if (!seenIds.Add(vehicle.Id))
                {
                    throw new ArgumentException($"Duplicate road vehicle id {vehicle.Id}.", nameof(roadVehicles));
                }

                var orderCopy = new RoadVehicleOrder[vehicle.Orders.Count];
                for (int orderIndex = 0; orderIndex < vehicle.Orders.Count; orderIndex++)
                {
                    orderCopy[orderIndex] = vehicle.Orders[orderIndex];
                }

                copy[index] = new RoadVehicle(
                    vehicle.Id,
                    vehicle.TypeId,
                    orderCopy,
                    vehicle.OrderIndex,
                    vehicle.IsStuck,
                    vehicle.IsOnEdge,
                    vehicle.EdgeFromNodeId,
                    vehicle.EdgeToNodeId,
                    vehicle.ProgressAlongEdge,
                    vehicle.Cargo.Stacks,
                    vehicle.CargoCapacity,
                    vehicle.IsServicingStop,
                    vehicle.IsUnloadComplete);
            }

            Array.Sort(copy, CompareVehiclesById);
            return copy;
        }

        private static int CompareVehiclesById(RoadVehicle left, RoadVehicle right)
        {
            return left.Id.CompareTo(right.Id);
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
