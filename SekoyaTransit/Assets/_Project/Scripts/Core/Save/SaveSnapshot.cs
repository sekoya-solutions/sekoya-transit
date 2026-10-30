#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Save
{
    /// <summary>
    /// An immutable, versioned snapshot of the currently implemented simulation state.
    /// </summary>
    public sealed class SaveSnapshot
    {
        public const int CurrentFormatVersion = 25;

        private readonly int[] _terrainTypeIds;
        private readonly int[] _objectIds;
        private readonly int[] _ownerIds;
        private readonly int[] _roadTypeIds;
        private readonly int[] _railTypeIds;
        private readonly ReadOnlyCollection<TileCoordinate> _townAnchors;
        private readonly ReadOnlyCollection<Town> _towns;
        private readonly ReadOnlyCollection<PlacedIndustry> _industries;
        private readonly ReadOnlyCollection<RoadStation> _roadStations;
        private readonly ReadOnlyCollection<RoadVehicle> _roadVehicles;
        private readonly ReadOnlyCollection<RailStation> _railStations;
        private readonly ReadOnlyCollection<RailVehicle> _railVehicles;
        private readonly ReadOnlyCollection<string> _unlockedVehicleTypeIds;
        private readonly ReadOnlyCollection<AchievementCompletion> _achievementCompletions;
        private readonly ReadOnlyCollection<CargoDeliveryCounter> _cargoDeliveryCounters;
        private readonly ReadOnlyCollection<Contract> _openContracts;
        private readonly FinanceLedger _financeLedger;
        private readonly CompanyStatistics _companyStatistics;

        /// <summary>
        /// Initializes a versioned snapshot and copies all supplied collections.
        /// Format version 1 has no road channel (<paramref name="roadTypeIds"/> must be <see langword="null"/>).
        /// Format version 2+ requires a road channel with one value per tile.
        /// Format version 3+ requires a road-station list (<paramref name="roadStations"/>; may be empty).
        /// Format versions below 3 must pass <see langword="null"/> for <paramref name="roadStations"/>.
        /// Format version 4+ requires a road-vehicle list (<paramref name="roadVehicles"/>; may be empty).
        /// Format versions below 4 must pass <see langword="null"/> for <paramref name="roadVehicles"/>.
        /// Format version 5+ persists vehicle cargo and per-order wait-for-full flags (station cargo removed in v9).
        /// Format version 6+ persists company cash and industry cargo stacks.
        /// Format versions below 6 must pass <paramref name="companyCash"/> as zero (migration supplies the starting balance).
        /// Format version 8+ persists industry calendar-month produced/transported counters.
        /// Format version 9+ drops station cargo and persists industry load round-robin cursors.
        /// Format version 10+ requires a rail type channel with one value per tile.
        /// Format versions below 10 must pass <see langword="null"/> for <paramref name="railTypeIds"/>.
        /// Format version 11+ requires rail-station and rail-vehicle lists (<paramref name="railStations"/> /
        /// <paramref name="railVehicles"/>; may be empty).
        /// Format versions below 11 must pass <see langword="null"/> for those lists.
        /// Format version 13+ persists town population and needs (<paramref name="towns"/>).
        /// When omitted on version 13+, placeholder population and empty needs are supplied from anchors.
        /// Format versions below 13 must pass <see langword="null"/> for <paramref name="towns"/>.
        /// Format version 14+ persists per-need monthly supply and town stats year/month.
        /// Format version 15+ persists town waiting cargo and load round-robin, plus vehicle passenger origin.
        /// Format version 16+ persists town month passengers/mail generated and transported counters.
        /// Format version 17+ persists last vehicle-upkeep calendar year/month (0/0 unset).
        /// Format version 18+ persists string vehicle type definition ids (legacy integer typeId mapped at deserialize).
        /// Format version 19+ persists permanently unlocked vehicle type ids (<paramref name="unlockedVehicleTypeIds"/>; may be empty).
        /// Format versions below 19 must pass <see langword="null"/> or an empty list for <paramref name="unlockedVehicleTypeIds"/>.
        /// Format version 20+ persists achievement completions (completion order) and lifetime cargo delivery counters
        /// (<paramref name="achievementCompletions"/> / <paramref name="cargoDeliveryCounters"/>; may be empty).
        /// Format versions below 20 must pass <see langword="null"/> or empty lists for both.
        /// Format version 21+ persists open contracts (ascending id), the next contract id, and the contract
        /// generator state (<paramref name="openContracts"/> / <paramref name="nextContractId"/> /
        /// <paramref name="contractRandomState"/>). A zero next id means the first id and a null state means the
        /// generator freshly seeded from the world seed. Format versions below 21 must leave all three unset.
        /// Format version 22+ persists the finance ledger (<paramref name="financeLedger"/>; null means an empty
        /// ledger). Format versions below 22 must leave it unset.
        /// Format version 25+ persists the lifetime company counters (<paramref name="companyStatistics"/>; null means
        /// all zero). Format versions below 25 must leave it unset.
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
            IReadOnlyList<int>? railTypeIds,
            IReadOnlyList<TileCoordinate> townAnchors,
            IReadOnlyList<PlacedIndustry> industries,
            IReadOnlyList<RoadStation>? roadStations = null,
            IReadOnlyList<RoadVehicle>? roadVehicles = null,
            long companyCash = 0L,
            IReadOnlyList<RailStation>? railStations = null,
            IReadOnlyList<RailVehicle>? railVehicles = null,
            IReadOnlyList<Town>? towns = null,
            int lastUpkeepYear = 0,
            int lastUpkeepMonth = 0,
            IReadOnlyList<string>? unlockedVehicleTypeIds = null,
            IReadOnlyList<AchievementCompletion>? achievementCompletions = null,
            IReadOnlyList<CargoDeliveryCounter>? cargoDeliveryCounters = null,
            IReadOnlyList<Contract>? openContracts = null,
            int nextContractId = 0,
            Pcg32State? contractRandomState = null,
            FinanceLedger? financeLedger = null,
            CompanyStatistics? companyStatistics = null)
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

            if (formatVersion >= 10)
            {
                if (railTypeIds == null)
                {
                    throw new ArgumentNullException(nameof(railTypeIds), "Format version 10+ requires a rail type channel.");
                }

                if (railTypeIds.Count != tileCount)
                {
                    throw new ArgumentException("Every tile channel must contain exactly one value per map tile.", nameof(railTypeIds));
                }

                _railTypeIds = CopyTileChannel(railTypeIds, nameof(railTypeIds), false);
            }
            else
            {
                if (railTypeIds != null)
                {
                    throw new ArgumentException("Format versions below 10 must not include a rail type channel.", nameof(railTypeIds));
                }

                _railTypeIds = Array.Empty<int>();
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
#pragma warning disable CA1510
                if (industry == null)
                {
                    throw new ArgumentNullException(nameof(industries), $"Industry placement {index} is null.");
                }
#pragma warning restore CA1510

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

                IReadOnlyList<CargoAmount>? cargoStacks = formatVersion >= 6
                    ? industry.Cargo.Stacks
                    : Array.Empty<CargoAmount>();
                long monthProduced = formatVersion >= 8 ? industry.MonthProduced : 0L;
                long monthTransported = formatVersion >= 8 ? industry.MonthTransported : 0L;
                int statsYear = formatVersion >= 8 ? industry.StatsYear : 0;
                int statsMonth = formatVersion >= 8 ? industry.StatsMonth : 0;
                int loadRoundRobinIndex = formatVersion >= 9 ? industry.LoadRoundRobinIndex : 0;
                IReadOnlyList<CargoAmount>? inputLeftover = formatVersion >= 23
                    ? industry.InputLeftover.Stacks
                    : Array.Empty<CargoAmount>();
                int productionLevel = formatVersion >= 24 ? industry.ProductionLevel : 0;
                int servedMonths = formatVersion >= 24 ? industry.ServedMonths : 0;
                industryCopy[index] = new PlacedIndustry(
                    industry.IndustryId,
                    industry.InstanceIndex,
                    industry.Coordinate,
                    cargoStacks,
                    monthProduced,
                    monthTransported,
                    statsYear,
                    statsMonth,
                    loadRoundRobinIndex,
                    inputLeftover,
                    productionLevel,
                    servedMonths);
            }

            if (formatVersion >= 6)
            {
                if (companyCash < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(companyCash),
                        companyCash,
                        "Company cash cannot be negative.");
                }

                CompanyCash = companyCash;
            }
            else
            {
                if (companyCash != 0)
                {
                    throw new ArgumentException(
                        "Format versions below 6 must not include company cash.",
                        nameof(companyCash));
                }

                CompanyCash = 0;
            }

            if (formatVersion >= 17)
            {
                if (lastUpkeepYear < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(lastUpkeepYear),
                        lastUpkeepYear,
                        "Last upkeep year cannot be negative.");
                }

                if (lastUpkeepMonth < 0 || lastUpkeepMonth > 12)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(lastUpkeepMonth),
                        lastUpkeepMonth,
                        "Last upkeep month must be between 0 and 12.");
                }

                if ((lastUpkeepYear == 0) != (lastUpkeepMonth == 0))
                {
                    throw new ArgumentException("Last upkeep year and month must both be unset (0/0) or both set.");
                }

                LastUpkeepYear = lastUpkeepYear;
                LastUpkeepMonth = lastUpkeepMonth;
            }
            else
            {
                if (lastUpkeepYear != 0 || lastUpkeepMonth != 0)
                {
                    throw new ArgumentException(
                        "Format versions below 17 must not include last vehicle-upkeep month.");
                }

                LastUpkeepYear = 0;
                LastUpkeepMonth = 0;
            }

            if (formatVersion >= 19)
            {
                _unlockedVehicleTypeIds = Array.AsReadOnly(
                    CopySortedUniqueVehicleTypeIds(unlockedVehicleTypeIds, nameof(unlockedVehicleTypeIds)));
            }
            else
            {
                if (unlockedVehicleTypeIds != null && unlockedVehicleTypeIds.Count > 0)
                {
                    throw new ArgumentException(
                        "Format versions below 19 must not include unlocked vehicle type ids.",
                        nameof(unlockedVehicleTypeIds));
                }

                _unlockedVehicleTypeIds = Array.AsReadOnly(Array.Empty<string>());
            }

            if (formatVersion >= 20)
            {
                _achievementCompletions = Array.AsReadOnly(
                    CopyUniqueCompletions(achievementCompletions, nameof(achievementCompletions)));
                _cargoDeliveryCounters = Array.AsReadOnly(
                    CopySortedUniqueCounters(cargoDeliveryCounters, nameof(cargoDeliveryCounters)));
            }
            else
            {
                if ((achievementCompletions != null && achievementCompletions.Count > 0) ||
                    (cargoDeliveryCounters != null && cargoDeliveryCounters.Count > 0))
                {
                    throw new ArgumentException(
                        "Format versions below 20 must not include achievement completions or delivery counters.");
                }

                _achievementCompletions = Array.AsReadOnly(Array.Empty<AchievementCompletion>());
                _cargoDeliveryCounters = Array.AsReadOnly(Array.Empty<CargoDeliveryCounter>());
            }

            if (formatVersion >= 21)
            {
                if (nextContractId < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(nextContractId),
                        nextContractId,
                        "The next contract id cannot be negative.");
                }

                NextContractId = nextContractId == 0 ? GeneratedWorld.FirstContractId : nextContractId;
                ContractRandomState = contractRandomState ??
                    GeneratedWorld.CreateContractRandom(mapSeed).CapturedState;
                _openContracts = Array.AsReadOnly(
                    CopyOrderedUniqueContracts(openContracts, NextContractId, nameof(openContracts)));
            }
            else
            {
                if ((openContracts != null && openContracts.Count > 0) ||
                    nextContractId != 0 ||
                    contractRandomState != null)
                {
                    throw new ArgumentException(
                        "Format versions below 21 must not include contracts, a next contract id, or a contract generator state.");
                }

                NextContractId = GeneratedWorld.FirstContractId;
                ContractRandomState = GeneratedWorld.CreateContractRandom(mapSeed).CapturedState;
                _openContracts = Array.AsReadOnly(Array.Empty<Contract>());
            }

            if (formatVersion >= 22)
            {
                _financeLedger = financeLedger?.Copy() ?? new FinanceLedger();
            }
            else
            {
                if (financeLedger != null && (financeLedger.Year != 0 || !financeLedger.Current.IsEmpty ||
                    !financeLedger.Previous.IsEmpty))
                {
                    throw new ArgumentException(
                        "Format versions below 22 must not include a finance ledger.",
                        nameof(financeLedger));
                }

                _financeLedger = new FinanceLedger();
            }

            if (formatVersion >= 25)
            {
                _companyStatistics = companyStatistics?.Copy() ?? new CompanyStatistics();
            }
            else
            {
                if (companyStatistics != null)
                {
                    throw new ArgumentException(
                        "Format versions below 25 must not include company statistics.",
                        nameof(companyStatistics));
                }

                _companyStatistics = new CompanyStatistics();
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

            if (formatVersion >= 11)
            {
                if (railStations == null)
                {
                    throw new ArgumentNullException(nameof(railStations), "Format version 11+ requires a rail station list.");
                }

                if (railVehicles == null)
                {
                    throw new ArgumentNullException(nameof(railVehicles), "Format version 11+ requires a rail vehicle list.");
                }

                _railStations = Array.AsReadOnly(
                    CopyRailStations(railStations, sideLength, _objectIds, _roadTypeIds, _railTypeIds, _roadStations));
                _railVehicles = Array.AsReadOnly(CopyRailVehicles(railVehicles));
            }
            else
            {
                if (railStations != null)
                {
                    throw new ArgumentException("Format versions below 11 must not include rail stations.", nameof(railStations));
                }

                if (railVehicles != null)
                {
                    throw new ArgumentException("Format versions below 11 must not include rail vehicles.", nameof(railVehicles));
                }

                _railStations = Array.AsReadOnly(Array.Empty<RailStation>());
                _railVehicles = Array.AsReadOnly(Array.Empty<RailVehicle>());
            }

            if (formatVersion >= 13)
            {
                IReadOnlyList<Town> resolvedTowns = towns ??
                    TownNeedsAssignment.CreateTownsWithoutNeeds(
                        townCopy,
                        GeneratedWorld.MigratedTownPopulation);
                _towns = Array.AsReadOnly(CopyTowns(resolvedTowns, townCopy));
            }
            else
            {
                if (towns != null)
                {
                    throw new ArgumentException(
                        "Format versions below 13 must not include town population state.",
                        nameof(towns));
                }

                _towns = Array.AsReadOnly(Array.Empty<Town>());
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
        /// Gets whether this snapshot includes a rail type channel (format version 10+).
        /// </summary>
        public bool HasRailTypeIds
        {
            get
            {
                return _railTypeIds.Length > 0;
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
        /// Gets whether this snapshot includes the format version 11+ rail station list.
        /// </summary>
        public bool HasRailStations
        {
            get
            {
                return FormatVersion >= 11;
            }
        }

        /// <summary>
        /// Gets whether this snapshot includes the format version 11+ rail vehicle list.
        /// </summary>
        public bool HasRailVehicles
        {
            get
            {
                return FormatVersion >= 11;
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
        /// Gets the row-major rail type IDs. Empty when <see cref="HasRailTypeIds"/> is false.
        /// </summary>
        public ReadOnlySpan<int> RailTypeIds
        {
            get
            {
                return _railTypeIds;
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
        /// Gets towns with population and needs. Empty when <see cref="HasTowns"/> is false.
        /// </summary>
        public IReadOnlyList<Town> Towns
        {
            get
            {
                return _towns;
            }
        }

        /// <summary>
        /// Gets whether this snapshot includes format version 13+ town population and needs.
        /// </summary>
        public bool HasTowns
        {
            get
            {
                return FormatVersion >= 13;
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
        /// Gets rail stations in ascending id order. Empty when <see cref="HasRailStations"/> is false.
        /// </summary>
        public IReadOnlyList<RailStation> RailStations
        {
            get
            {
                return _railStations;
            }
        }

        /// <summary>
        /// Gets rail vehicles in ascending id order. Empty when <see cref="HasRailVehicles"/> is false.
        /// </summary>
        public IReadOnlyList<RailVehicle> RailVehicles
        {
            get
            {
                return _railVehicles;
            }
        }

        /// <summary>
        /// Gets company cash. Zero for format versions below 6.
        /// </summary>
        public long CompanyCash { get; }

        /// <summary>
        /// Gets whether this snapshot includes format version 6+ economy fields.
        /// </summary>
        public bool HasCompanyCash
        {
            get
            {
                return FormatVersion >= 6;
            }
        }

        /// <summary>
        /// Gets the calendar year of the last applied vehicle-upkeep month, or 0 when unset.
        /// </summary>
        public int LastUpkeepYear { get; }

        /// <summary>
        /// Gets the calendar month of the last applied vehicle-upkeep month, or 0 when unset.
        /// </summary>
        public int LastUpkeepMonth { get; }

        /// <summary>
        /// Gets permanently unlocked vehicle type ids in ascending ordinal order.
        /// Empty when <see cref="HasUnlockedVehicleTypes"/> is false.
        /// </summary>
        public IReadOnlyList<string> UnlockedVehicleTypeIds
        {
            get
            {
                return _unlockedVehicleTypeIds;
            }
        }

        /// <summary>
        /// Gets whether this snapshot includes format version 19+ permanent vehicle unlocks.
        /// </summary>
        public bool HasUnlockedVehicleTypes
        {
            get
            {
                return FormatVersion >= 19;
            }
        }

        /// <summary>
        /// Gets achievement completions in completion order. Empty when <see cref="HasAchievementProgress"/> is false.
        /// </summary>
        public IReadOnlyList<AchievementCompletion> AchievementCompletions
        {
            get
            {
                return _achievementCompletions;
            }
        }

        /// <summary>
        /// Gets lifetime cargo delivery counters ordered by cargo id, then town before industry.
        /// Empty when <see cref="HasAchievementProgress"/> is false.
        /// </summary>
        public IReadOnlyList<CargoDeliveryCounter> CargoDeliveryCounters
        {
            get
            {
                return _cargoDeliveryCounters;
            }
        }

        /// <summary>
        /// Gets open contracts in ascending id order. Empty when <see cref="HasContracts"/> is false.
        /// </summary>
        public IReadOnlyList<Contract> OpenContracts
        {
            get
            {
                return _openContracts;
            }
        }

        /// <summary>
        /// Gets the id the next offered contract receives (the first id before format version 21).
        /// </summary>
        public int NextContractId { get; }

        /// <summary>
        /// Gets the contract generator state (freshly seeded from the world seed before format version 21).
        /// </summary>
        public Pcg32State ContractRandomState { get; }

        /// <summary>
        /// Gets a copy of the finance ledger (empty before format version 22).
        /// </summary>
        public FinanceLedger FinanceLedger
        {
            get
            {
                return _financeLedger.Copy();
            }
        }

        /// <summary>
        /// Gets a copy of the lifetime company counters (all zero before format version 25).
        /// </summary>
        public CompanyStatistics CompanyStatistics
        {
            get
            {
                return _companyStatistics.Copy();
            }
        }

        /// <summary>
        /// Gets whether this snapshot includes format version 25+ company statistics.
        /// </summary>
        public bool HasCompanyStatistics
        {
            get
            {
                return FormatVersion >= 25;
            }
        }

        /// <summary>
        /// Gets whether this snapshot includes format version 22+ finance ledger state.
        /// </summary>
        public bool HasFinanceLedger
        {
            get
            {
                return FormatVersion >= 22;
            }
        }

        /// <summary>
        /// Gets whether this snapshot includes format version 21+ contract state.
        /// </summary>
        public bool HasContracts
        {
            get
            {
                return FormatVersion >= 21;
            }
        }

        /// <summary>
        /// Gets whether this snapshot includes format version 20+ achievement progress.
        /// </summary>
        public bool HasAchievementProgress
        {
            get
            {
                return FormatVersion >= 20;
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
            int[] railTypeIds = new int[world.Map.TileCount];
            world.Map.TerrainTypeIds.CopyTo(terrainTypeIds);
            world.Map.ObjectIds.CopyTo(objectIds);
            world.Map.OwnerIds.CopyTo(ownerIds);
            world.Map.RoadTypeIds.CopyTo(roadTypeIds);
            world.Map.RailTypeIds.CopyTo(railTypeIds);

            return new SaveSnapshot(
                CurrentFormatVersion,
                mapSeed,
                currentTick,
                world.Map.Size,
                terrainTypeIds,
                objectIds,
                ownerIds,
                roadTypeIds,
                railTypeIds,
                world.TownAnchors,
                world.Industries,
                world.RoadStations,
                world.RoadVehicles,
                world.CompanyCash,
                world.RailStations,
                world.RailVehicles,
                world.Towns,
                world.LastUpkeepYear,
                world.LastUpkeepMonth,
                world.UnlockedVehicleTypeIds,
                world.AchievementCompletions,
                world.CargoDeliveryCounters,
                world.OpenContracts,
                world.NextContractId,
                world.ContractRandomState,
                world.Finance,
                world.Statistics);
        }

        /// <summary>
        /// Copies this snapshot with a new format version for a same-channel migration step.
        /// When upgrading to format version 3+, an empty road-station list is supplied if absent.
        /// When upgrading to format version 4+, an empty road-vehicle list is supplied if absent.
        /// When upgrading to format version 6+, company cash defaults to zero unless set via
        /// <see cref="WithCompanyCash"/>.
        /// </summary>
        public SaveSnapshot WithFormatVersion(int formatVersion)
        {
            IReadOnlyList<int>? roadTypeIds = HasRoadTypeIds ? (IReadOnlyList<int>)_roadTypeIds : null;
            IReadOnlyList<int>? railTypeIds = HasRailTypeIds ? (IReadOnlyList<int>)_railTypeIds : null;
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

            IReadOnlyList<RailStation>? railStations = null;
            IReadOnlyList<RailVehicle>? railVehicles = null;
            if (formatVersion >= 11)
            {
                railStations = _railStations;
                railVehicles = _railVehicles;
            }

            long companyCash = 0L;
            if (formatVersion >= 6 && FormatVersion >= 6)
            {
                companyCash = CompanyCash;
            }

            return Reconstruct(
                formatVersion,
                roadTypeIds,
                railTypeIds,
                roadStations,
                roadVehicles,
                companyCash,
                railStations,
                railVehicles,
                ResolveTownsForFormat(formatVersion));
        }

        /// <summary>
        /// Copies this snapshot with format version 6+ company cash (industry cargo is taken from this snapshot).
        /// </summary>
        public SaveSnapshot WithCompanyCash(int formatVersion, long companyCash)
        {
            if (formatVersion < 6)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(formatVersion),
                    formatVersion,
                    "Company cash requires format version 6+.");
            }

            IReadOnlyList<int>? roadTypeIds = HasRoadTypeIds ? (IReadOnlyList<int>)_roadTypeIds : null;
            IReadOnlyList<int>? railTypeIds = HasRailTypeIds ? (IReadOnlyList<int>)_railTypeIds : null;
            IReadOnlyList<RailStation>? railStations = formatVersion >= 11 ? _railStations : null;
            IReadOnlyList<RailVehicle>? railVehicles = formatVersion >= 11 ? _railVehicles : null;
            return Reconstruct(
                formatVersion,
                roadTypeIds,
                railTypeIds,
                _roadStations,
                _roadVehicles,
                companyCash,
                railStations,
                railVehicles,
                ResolveTownsForFormat(formatVersion));
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

            long companyCash = formatVersion >= 6 && FormatVersion >= 6 ? CompanyCash : 0L;
            IReadOnlyList<int>? railTypeIds = HasRailTypeIds ? (IReadOnlyList<int>)_railTypeIds : null;
            IReadOnlyList<RailStation>? railStations = formatVersion >= 11 ? _railStations : null;
            IReadOnlyList<RailVehicle>? railVehicles = formatVersion >= 11 ? _railVehicles : null;
            return Reconstruct(
                formatVersion,
                roadTypeIds,
                railTypeIds,
                roadStations,
                roadVehicles,
                companyCash,
                railStations,
                railVehicles,
                ResolveTownsForFormat(formatVersion));
        }

        /// <summary>
        /// Copies this snapshot and adds or replaces the rail type channel for a format upgrade.
        /// </summary>
        public SaveSnapshot WithRailTypeIds(int formatVersion, IReadOnlyList<int> railTypeIds)
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

            long companyCash = formatVersion >= 6 && FormatVersion >= 6 ? CompanyCash : 0L;
            IReadOnlyList<RailStation>? railStations = formatVersion >= 11 ? _railStations : null;
            IReadOnlyList<RailVehicle>? railVehicles = formatVersion >= 11 ? _railVehicles : null;
            return Reconstruct(
                formatVersion,
                roadTypeIds,
                railTypeIds,
                roadStations,
                roadVehicles,
                companyCash,
                railStations,
                railVehicles,
                ResolveTownsForFormat(formatVersion));
        }

        /// <summary>
        /// Copies this snapshot and adds or replaces the road station list for a format upgrade.
        /// </summary>
        public SaveSnapshot WithRoadStations(int formatVersion, IReadOnlyList<RoadStation> roadStations)
        {
            IReadOnlyList<int>? roadTypeIds = HasRoadTypeIds ? (IReadOnlyList<int>)_roadTypeIds : null;
            IReadOnlyList<int>? railTypeIds = HasRailTypeIds ? (IReadOnlyList<int>)_railTypeIds : null;
            IReadOnlyList<RoadVehicle>? roadVehicles = null;
            if (formatVersion >= 4)
            {
                roadVehicles = _roadVehicles;
            }

            long companyCash = formatVersion >= 6 && FormatVersion >= 6 ? CompanyCash : 0L;
            IReadOnlyList<RailStation>? railStations = formatVersion >= 11 ? _railStations : null;
            IReadOnlyList<RailVehicle>? railVehicles = formatVersion >= 11 ? _railVehicles : null;
            return Reconstruct(
                formatVersion,
                roadTypeIds,
                railTypeIds,
                roadStations,
                roadVehicles,
                companyCash,
                railStations,
                railVehicles,
                ResolveTownsForFormat(formatVersion));
        }

        /// <summary>
        /// Copies this snapshot and adds or replaces the road vehicle list for a format upgrade.
        /// </summary>
        public SaveSnapshot WithRoadVehicles(int formatVersion, IReadOnlyList<RoadVehicle> roadVehicles)
        {
            IReadOnlyList<int>? roadTypeIds = HasRoadTypeIds ? (IReadOnlyList<int>)_roadTypeIds : null;
            IReadOnlyList<int>? railTypeIds = HasRailTypeIds ? (IReadOnlyList<int>)_railTypeIds : null;
            long companyCash = formatVersion >= 6 && FormatVersion >= 6 ? CompanyCash : 0L;
            IReadOnlyList<RailStation>? railStations = formatVersion >= 11 ? _railStations : null;
            IReadOnlyList<RailVehicle>? railVehicles = formatVersion >= 11 ? _railVehicles : null;
            return Reconstruct(
                formatVersion,
                roadTypeIds,
                railTypeIds,
                _roadStations,
                roadVehicles,
                companyCash,
                railStations,
                railVehicles,
                ResolveTownsForFormat(formatVersion));
        }

        /// <summary>
        /// Copies this snapshot and adds rail-station and rail-vehicle lists for a format upgrade.
        /// </summary>
        public SaveSnapshot WithRailStationsAndVehicles(
            int formatVersion,
            IReadOnlyList<RailStation> railStations,
            IReadOnlyList<RailVehicle> railVehicles)
        {
            IReadOnlyList<int>? roadTypeIds = HasRoadTypeIds ? (IReadOnlyList<int>)_roadTypeIds : null;
            IReadOnlyList<int>? railTypeIds = HasRailTypeIds ? (IReadOnlyList<int>)_railTypeIds : null;
            long companyCash = formatVersion >= 6 && FormatVersion >= 6 ? CompanyCash : 0L;
            return Reconstruct(
                formatVersion,
                roadTypeIds,
                railTypeIds,
                _roadStations,
                _roadVehicles,
                companyCash,
                railStations,
                railVehicles,
                ResolveTownsForFormat(formatVersion));
        }

        /// <summary>
        /// Copies this snapshot with format version 13+ town population and needs.
        /// </summary>
        public SaveSnapshot WithTowns(int formatVersion, IReadOnlyList<Town> towns)
        {
            if (formatVersion < 13)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(formatVersion),
                    formatVersion,
                    "Town population state requires format version 13+.");
            }

            IReadOnlyList<int>? roadTypeIds = HasRoadTypeIds ? (IReadOnlyList<int>)_roadTypeIds : null;
            IReadOnlyList<int>? railTypeIds = HasRailTypeIds ? (IReadOnlyList<int>)_railTypeIds : null;
            long companyCash = formatVersion >= 6 && FormatVersion >= 6 ? CompanyCash : 0L;
            IReadOnlyList<RailStation>? railStations = formatVersion >= 11 ? _railStations : null;
            IReadOnlyList<RailVehicle>? railVehicles = formatVersion >= 11 ? _railVehicles : null;
            return Reconstruct(
                formatVersion,
                roadTypeIds,
                railTypeIds,
                _roadStations,
                _roadVehicles,
                companyCash,
                railStations,
                railVehicles,
                towns);
        }

        private SaveSnapshot Reconstruct(
            int formatVersion,
            IReadOnlyList<int>? roadTypeIds,
            IReadOnlyList<int>? railTypeIds,
            IReadOnlyList<RoadStation>? roadStations,
            IReadOnlyList<RoadVehicle>? roadVehicles,
            long companyCash,
            IReadOnlyList<RailStation>? railStations,
            IReadOnlyList<RailVehicle>? railVehicles,
            IReadOnlyList<Town>? towns)
        {
            int lastUpkeepYear = 0;
            int lastUpkeepMonth = 0;
            if (formatVersion >= 17 && FormatVersion >= 17)
            {
                lastUpkeepYear = LastUpkeepYear;
                lastUpkeepMonth = LastUpkeepMonth;
            }

            IReadOnlyList<string>? unlockedVehicleTypeIds = null;
            if (formatVersion >= 19)
            {
                unlockedVehicleTypeIds = FormatVersion >= 19
                    ? _unlockedVehicleTypeIds
                    : Array.Empty<string>();
            }

            IReadOnlyList<AchievementCompletion>? achievementCompletions = null;
            IReadOnlyList<CargoDeliveryCounter>? cargoDeliveryCounters = null;
            if (formatVersion >= 20)
            {
                achievementCompletions = FormatVersion >= 20
                    ? _achievementCompletions
                    : Array.Empty<AchievementCompletion>();
                cargoDeliveryCounters = FormatVersion >= 20
                    ? _cargoDeliveryCounters
                    : Array.Empty<CargoDeliveryCounter>();
            }

            IReadOnlyList<Contract>? openContracts = null;
            int nextContractId = 0;
            Pcg32State? contractRandomState = null;
            if (formatVersion >= 21)
            {
                openContracts = FormatVersion >= 21 ? _openContracts : Array.Empty<Contract>();
                if (FormatVersion >= 21)
                {
                    nextContractId = NextContractId;
                    contractRandomState = ContractRandomState;
                }
            }

            FinanceLedger? financeLedger = null;
            if (formatVersion >= 22)
            {
                financeLedger = FormatVersion >= 22 ? _financeLedger : new FinanceLedger();
            }

            CompanyStatistics? companyStatistics = null;
            if (formatVersion >= 25 && FormatVersion >= 25)
            {
                companyStatistics = _companyStatistics;
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
                railTypeIds,
                _townAnchors,
                _industries,
                roadStations,
                roadVehicles,
                companyCash,
                railStations,
                railVehicles,
                towns,
                lastUpkeepYear,
                lastUpkeepMonth,
                unlockedVehicleTypeIds,
                achievementCompletions,
                cargoDeliveryCounters,
                openContracts,
                nextContractId,
                contractRandomState,
                financeLedger,
                companyStatistics);
        }

        private ReadOnlyCollection<Town>? ResolveTownsForFormat(int formatVersion)
        {
            if (formatVersion < 13)
            {
                return null;
            }

            if (FormatVersion >= 13)
            {
                return _towns;
            }

            return null;
        }

        private static Town[] CopyTowns(IReadOnlyList<Town> towns, TileCoordinate[] townAnchors)
        {
#pragma warning disable CA1510
            if (towns is null)
            {
                throw new ArgumentNullException(nameof(towns));
            }
#pragma warning restore CA1510

            if (towns.Count != townAnchors.Length)
            {
                throw new ArgumentException(
                    "Town population list must match town anchor count.",
                    nameof(towns));
            }

            var copy = new Town[towns.Count];
            for (int index = 0; index < towns.Count; index++)
            {
                Town town = towns[index];
#pragma warning disable CA1510
                if (town is null)
                {
                    throw new ArgumentNullException(nameof(towns), $"Town at index {index} is null.");
                }
#pragma warning restore CA1510

                if (town.Coordinate != townAnchors[index])
                {
                    throw new ArgumentException(
                        $"Town {index} coordinate does not match its anchor.",
                        nameof(towns));
                }

                if (town.Id != index)
                {
                    throw new ArgumentException($"Town at index {index} has id {town.Id}.", nameof(towns));
                }

                copy[index] = new Town(
                    town.Id,
                    town.Coordinate,
                    town.Population,
                    town.Needs,
                    town.StatsYear,
                    town.StatsMonth,
                    town.WaitingCargo.Stacks,
                    Town.PlaceholderPassengerWaitingCapacity,
                    town.LoadRoundRobinIndex,
                    town.MonthPassengersGenerated,
                    town.MonthPassengersTransported,
                    town.MonthMailGenerated,
                    town.MonthMailTransported,
                    town.Name,
                    town.NeedPlan);
            }

            return copy;
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

                copy[index] = new RoadStation(station.Id, station.Coordinate);
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
                    vehicle.IsUnloadComplete,
                    vehicle.LifetimeDeliveryIncome,
                    vehicle.PassengerCargoOriginTownId,
                    vehicle.CargoOriginTile,
                    vehicle.DeliveryPayRemainder);
            }

            Array.Sort(copy, CompareVehiclesById);
            return copy;
        }

        private static int CompareVehiclesById(RoadVehicle left, RoadVehicle right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private static RailStation[] CopyRailStations(
            IReadOnlyList<RailStation> railStations,
            int sideLength,
            int[] objectIds,
            int[] roadTypeIds,
            int[] railTypeIds,
            ReadOnlyCollection<RoadStation> roadStations)
        {
            var copy = new RailStation[railStations.Count];
            var seenIds = new HashSet<int>();
            var seenTiles = new HashSet<int>();
            var roadStationTiles = new HashSet<int>();
            for (int index = 0; index < roadStations.Count; index++)
            {
                TileCoordinate coordinate = roadStations[index].Coordinate;
                roadStationTiles.Add((coordinate.Y * sideLength) + coordinate.X);
            }

            for (int index = 0; index < railStations.Count; index++)
            {
                RailStation station = railStations[index];
#pragma warning disable CA1510
                if (station == null)
                {
                    throw new ArgumentNullException(nameof(railStations), $"Rail station at index {index} is null.");
                }
#pragma warning restore CA1510

                if (station.Id < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(railStations), "Station ids cannot be negative.");
                }

                if (!seenIds.Add(station.Id))
                {
                    throw new ArgumentException($"Duplicate rail station id {station.Id}.", nameof(railStations));
                }

                ValidateCoordinate(station.Coordinate, sideLength, nameof(railStations));
                int tileId = (station.Coordinate.Y * sideLength) + station.Coordinate.X;
                if (!seenTiles.Add(tileId))
                {
                    throw new ArgumentException(
                        $"Duplicate rail station tile ({station.Coordinate.X}, {station.Coordinate.Y}).",
                        nameof(railStations));
                }

                if (roadStationTiles.Contains(tileId))
                {
                    throw new ArgumentException(
                        $"Rail station {station.Id} overlaps a road station tile.",
                        nameof(railStations));
                }

                if (objectIds[tileId] != TileMap.NoObjectId)
                {
                    throw new ArgumentException(
                        $"Rail station {station.Id} overlaps a town or industry tile.",
                        nameof(railStations));
                }

                if (roadTypeIds.Length > 0 && roadTypeIds[tileId] != TileMap.NoRoadTypeId)
                {
                    throw new ArgumentException(
                        $"Rail station {station.Id} overlaps a road tile.",
                        nameof(railStations));
                }

                if (railTypeIds.Length > 0 && railTypeIds[tileId] != TileMap.NoRailTypeId)
                {
                    throw new ArgumentException(
                        $"Rail station {station.Id} overlaps a rail tile.",
                        nameof(railStations));
                }

                copy[index] = new RailStation(station.Id, station.Coordinate);
            }

            Array.Sort(copy, CompareRailStationsById);
            return copy;
        }

        private static int CompareRailStationsById(RailStation left, RailStation right)
        {
            return left.Id.CompareTo(right.Id);
        }

        private static RailVehicle[] CopyRailVehicles(IReadOnlyList<RailVehicle> railVehicles)
        {
            var copy = new RailVehicle[railVehicles.Count];
            var seenIds = new HashSet<int>();
            for (int index = 0; index < railVehicles.Count; index++)
            {
                RailVehicle vehicle = railVehicles[index];
#pragma warning disable CA1510
                if (vehicle == null)
                {
                    throw new ArgumentNullException(nameof(railVehicles), $"Rail vehicle at index {index} is null.");
                }
#pragma warning restore CA1510

                if (!seenIds.Add(vehicle.Id))
                {
                    throw new ArgumentException($"Duplicate rail vehicle id {vehicle.Id}.", nameof(railVehicles));
                }

                var orderCopy = new RailVehicleOrder[vehicle.Orders.Count];
                for (int orderIndex = 0; orderIndex < vehicle.Orders.Count; orderIndex++)
                {
                    orderCopy[orderIndex] = vehicle.Orders[orderIndex];
                }

                copy[index] = new RailVehicle(
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
                    vehicle.WagonCount,
                    vehicle.CargoCapacity,
                    vehicle.IsServicingStop,
                    vehicle.IsUnloadComplete,
                    vehicle.LifetimeDeliveryIncome,
                    vehicle.PassengerCargoOriginTownId,
                    vehicle.MaxWagons,
                    vehicle.CapacityPerWagon,
                    vehicle.CargoOriginTile,
                    vehicle.DeliveryPayRemainder);
            }

            Array.Sort(copy, CompareRailVehiclesById);
            return copy;
        }

        private static int CompareRailVehiclesById(RailVehicle left, RailVehicle right)
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

        private static AchievementCompletion[] CopyUniqueCompletions(
            IReadOnlyList<AchievementCompletion>? completions,
            string parameterName)
        {
            if (completions == null || completions.Count == 0)
            {
                return Array.Empty<AchievementCompletion>();
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            var copy = new AchievementCompletion[completions.Count];
            for (int index = 0; index < completions.Count; index++)
            {
                AchievementCompletion completion = completions[index];
                if (string.IsNullOrWhiteSpace(completion.AchievementId))
                {
                    throw new ArgumentException(
                        $"Achievement completion at index {index} has no achievement id.",
                        parameterName);
                }

                if (!unique.Add(completion.AchievementId))
                {
                    throw new ArgumentException(
                        $"Duplicate completed achievement id '{completion.AchievementId}'.",
                        parameterName);
                }

                copy[index] = completion;
            }

            return copy;
        }

        private static Contract[] CopyOrderedUniqueContracts(
            IReadOnlyList<Contract>? contracts,
            int nextContractId,
            string parameterName)
        {
            if (contracts == null || contracts.Count == 0)
            {
                return Array.Empty<Contract>();
            }

            var targets = new HashSet<string>(StringComparer.Ordinal);
            var copy = new Contract[contracts.Count];
            int previousId = 0;
            for (int index = 0; index < contracts.Count; index++)
            {
                Contract contract = contracts[index];
                if (contract == null)
                {
                    throw new ArgumentException($"Contract at index {index} is null.", parameterName);
                }

                if (contract.Id <= previousId || contract.Id >= nextContractId)
                {
                    throw new ArgumentException(
                        $"Contract id {contract.Id} must ascend and stay below the next contract id {nextContractId}.",
                        parameterName);
                }

                if (!targets.Add(
                        contract.CargoId + "|" + (int)contract.TargetKind + "|" + contract.TargetIndex))
                {
                    throw new ArgumentException(
                        $"Duplicate open contract for '{contract.CargoId}' at {contract.TargetKind} {contract.TargetIndex}.",
                        parameterName);
                }

                previousId = contract.Id;
                copy[index] = contract.Copy();
            }

            return copy;
        }

        private static CargoDeliveryCounter[] CopySortedUniqueCounters(
            IReadOnlyList<CargoDeliveryCounter>? counters,
            string parameterName)
        {
            if (counters == null || counters.Count == 0)
            {
                return Array.Empty<CargoDeliveryCounter>();
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            var copy = new List<CargoDeliveryCounter>(counters.Count);
            for (int index = 0; index < counters.Count; index++)
            {
                CargoDeliveryCounter counter = counters[index];
                if (string.IsNullOrWhiteSpace(counter.CargoId))
                {
                    throw new ArgumentException(
                        $"Delivery counter at index {index} has no cargo id.",
                        parameterName);
                }

                if (!unique.Add(counter.CargoId + "|" + (int)counter.DestinationKind))
                {
                    throw new ArgumentException(
                        $"Duplicate delivery counter for '{counter.CargoId}' ({counter.DestinationKind}).",
                        parameterName);
                }

                copy.Add(counter);
            }

            copy.Sort(
                (left, right) =>
                {
                    int byCargo = string.CompareOrdinal(left.CargoId, right.CargoId);
                    return byCargo != 0 ? byCargo : left.DestinationKind.CompareTo(right.DestinationKind);
                });
            return copy.ToArray();
        }

        private static string[] CopySortedUniqueVehicleTypeIds(IReadOnlyList<string>? typeIds, string parameterName)
        {
            if (typeIds == null || typeIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            var copy = new List<string>(typeIds.Count);
            for (int index = 0; index < typeIds.Count; index++)
            {
                string typeId = typeIds[index];
                if (string.IsNullOrWhiteSpace(typeId))
                {
                    throw new ArgumentException(
                        $"Unlocked vehicle type id at index {index} is null or whitespace.",
                        parameterName);
                }

                if (!unique.Add(typeId))
                {
                    continue;
                }

                copy.Add(typeId);
            }

            copy.Sort(StringComparer.Ordinal);
            return copy.ToArray();
        }
    }
}
