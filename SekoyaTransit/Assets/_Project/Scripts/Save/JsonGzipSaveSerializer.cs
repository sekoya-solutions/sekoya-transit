#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Text;
using SekoyaTransit.Core.Save;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;
using UnityEngine;

namespace SekoyaTransit.Save
{
    /// <summary>
    /// Serializes versioned snapshots as Unity JSON compressed with gzip.
    /// </summary>
    public sealed class JsonGzipSaveSerializer : ISaveSerializer
    {
        private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, true);
        private readonly SaveMigrationPipeline _migrationPipeline;

        /// <summary>
        /// Initializes the serializer with the migration chain for the current format.
        /// </summary>
        public JsonGzipSaveSerializer(SaveMigrationPipeline migrationPipeline)
        {
#pragma warning disable CA1510 // Keep the Unity 6.3-compatible Core API surface.
            if (migrationPipeline == null)
            {
                throw new ArgumentNullException(nameof(migrationPipeline));
            }
#pragma warning restore CA1510

            _migrationPipeline = migrationPipeline;
        }

        /// <inheritdoc />
        public byte[] Serialize(SaveSnapshot snapshot)
        {
#pragma warning disable CA1510 // Keep the Unity 6.3-compatible Core API surface.
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
#pragma warning restore CA1510

            if (snapshot.FormatVersion != _migrationPipeline.CurrentVersion)
            {
                throw new SaveFormatException(
                    $"Only current format version {_migrationPipeline.CurrentVersion} can be written.");
            }

            string json;
            try
            {
                json = JsonUtility.ToJson(CreateDocument(snapshot));
            }
            catch (ArgumentException exception)
            {
                throw new SaveFormatException("The save snapshot could not be converted to JSON.", exception);
            }

            byte[] jsonBytes = _utf8.GetBytes(json);
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    gzip.Write(jsonBytes, 0, jsonBytes.Length);
                }

                return output.ToArray();
            }
        }

        /// <inheritdoc />
        public SaveSnapshot Deserialize(byte[] compressedData)
        {
#pragma warning disable CA1510 // Keep the Unity 6.3-compatible Core API surface.
            if (compressedData == null)
            {
                throw new ArgumentNullException(nameof(compressedData));
            }
#pragma warning restore CA1510

            if (compressedData.Length == 0)
            {
                throw new SaveFormatException("The compressed save is empty.");
            }

            string json = Decompress(compressedData);
            SaveDocument? document;
            try
            {
                document = JsonUtility.FromJson<SaveDocument>(json);
            }
            catch (ArgumentException exception)
            {
                throw new SaveFormatException("The decompressed save is not valid JSON.", exception);
            }

            if (document == null || document.formatVersion < 1)
            {
                throw new SaveFormatException("The JSON save has no valid format version.");
            }

            if (document.formatVersion > _migrationPipeline.CurrentVersion)
            {
                throw new SaveFormatException(
                    $"Save format version {document.formatVersion} is newer than supported version {_migrationPipeline.CurrentVersion}.");
            }

            if (document.terrainTypeIds == null ||
                document.objectIds == null ||
                document.ownerIds == null ||
                document.townAnchors == null ||
                document.industries == null ||
                string.IsNullOrWhiteSpace(document.mapSeed) ||
                string.IsNullOrWhiteSpace(document.currentTick))
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 2 && document.roadTypeIds == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 10 && document.railTypeIds == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 3 && document.roadStations == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 4 && document.roadVehicles == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 11 && document.railStations == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 11 && document.railVehicles == null)
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing required world data.");
            }

            if (document.formatVersion >= 6 && string.IsNullOrWhiteSpace(document.companyCash))
            {
                throw new SaveFormatException("The JSON save is incomplete and is missing company cash.");
            }

            if (!ulong.TryParse(document.mapSeed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong mapSeed))
            {
                throw new SaveFormatException("The JSON save has an invalid map seed.");
            }

            if (!long.TryParse(document.currentTick, NumberStyles.None, CultureInfo.InvariantCulture, out long currentTick))
            {
                throw new SaveFormatException("The JSON save has an invalid simulation tick.");
            }

            long companyCash = 0L;
            if (document.formatVersion >= 6)
            {
                if (!long.TryParse(document.companyCash, NumberStyles.None, CultureInfo.InvariantCulture, out companyCash) ||
                    companyCash < 0)
                {
                    throw new SaveFormatException("The JSON save has an invalid company cash value.");
                }
            }

            var towns = new TileCoordinate[document.townAnchors.Length];
            Town[]? townStates = document.formatVersion >= 13
                ? new Town[document.townAnchors.Length]
                : null;
            for (int index = 0; index < towns.Length; index++)
            {
                SavedCoordinate? coordinate = document.townAnchors[index];
                if (coordinate == null)
                {
                    throw new SaveFormatException($"Town anchor {index} is missing.");
                }

                var tile = new TileCoordinate(coordinate.x, coordinate.y);
                towns[index] = tile;
                if (townStates != null)
                {
                    if (coordinate.population < 0)
                    {
                        throw new SaveFormatException($"Town {index} has an invalid population.");
                    }

                    TownNeed[] needs = ReadTownNeeds(
                        coordinate.needs,
                        index,
                        document.formatVersion >= 14,
                        document.formatVersion >= 28);
                    int statsYear = document.formatVersion >= 14 ? coordinate.statsYear : 0;
                    int statsMonth = document.formatVersion >= 14 ? coordinate.statsMonth : 0;
                    CargoAmount[] waitingCargo = document.formatVersion >= 15
                        ? ReadCargoEntries(coordinate.waitingCargo, $"Town {index} waiting cargo")
                        : Array.Empty<CargoAmount>();
                    int loadRoundRobinIndex = document.formatVersion >= 15
                        ? coordinate.loadRoundRobinIndex
                        : 0;
                    if (loadRoundRobinIndex < 0)
                    {
                        throw new SaveFormatException($"Town {index} has an invalid load round-robin index.");
                    }

                    long monthPassengersGenerated = 0L;
                    long monthPassengersTransported = 0L;
                    long monthMailGenerated = 0L;
                    long monthMailTransported = 0L;
                    if (document.formatVersion >= 16)
                    {
                        monthPassengersGenerated = ReadNonNegativeLong(
                            coordinate.monthPassengersGenerated,
                            $"Town {index} monthPassengersGenerated");
                        monthPassengersTransported = ReadNonNegativeLong(
                            coordinate.monthPassengersTransported,
                            $"Town {index} monthPassengersTransported");
                        monthMailGenerated = ReadNonNegativeLong(
                            coordinate.monthMailGenerated,
                            $"Town {index} monthMailGenerated");
                        monthMailTransported = ReadNonNegativeLong(
                            coordinate.monthMailTransported,
                            $"Town {index} monthMailTransported");
                    }

                    string name = string.Empty;
                    TownNeedPlan needPlan = TownNeedPlan.Empty;
                    if (document.formatVersion >= 23)
                    {
                        name = coordinate.name ?? string.Empty;
                        needPlan = ReadNeedPlan(coordinate, $"Town {index}");
                    }

                    int lastMonthGrowth = document.formatVersion >= 28 ? coordinate.lastMonthGrowth : 0;
                    if (lastMonthGrowth < 0)
                    {
                        throw new SaveFormatException($"Town {index} has an invalid lastMonthGrowth.");
                    }

                    townStates[index] = new Town(
                        index,
                        tile,
                        coordinate.population,
                        needs,
                        statsYear,
                        statsMonth,
                        waitingCargo,
                        Town.PlaceholderPassengerWaitingCapacity,
                        loadRoundRobinIndex,
                        monthPassengersGenerated,
                        monthPassengersTransported,
                        monthMailGenerated,
                        monthMailTransported,
                        name,
                        needPlan,
                        lastMonthGrowth);
                }
            }

            var industries = new PlacedIndustry[document.industries.Length];
            for (int index = 0; index < industries.Length; index++)
            {
                SavedIndustry? industry = document.industries[index];
                if (industry == null)
                {
                    throw new SaveFormatException($"Industry placement {index} is missing.");
                }

                CargoAmount[] industryCargo = document.formatVersion >= 6
                    ? ReadCargoEntries(industry.cargo, $"Industry placement {index}")
                    : Array.Empty<CargoAmount>();
                long monthProduced = 0L;
                long monthTransported = 0L;
                int statsYear = 0;
                int statsMonth = 0;
                if (document.formatVersion >= 8)
                {
                    if (string.IsNullOrWhiteSpace(industry.monthProduced) ||
                        !long.TryParse(
                            industry.monthProduced,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out monthProduced) ||
                        monthProduced < 0L)
                    {
                        throw new SaveFormatException(
                            $"Industry placement {index} has an invalid monthProduced value.");
                    }

                    if (string.IsNullOrWhiteSpace(industry.monthTransported) ||
                        !long.TryParse(
                            industry.monthTransported,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out monthTransported) ||
                        monthTransported < 0L)
                    {
                        throw new SaveFormatException(
                            $"Industry placement {index} has an invalid monthTransported value.");
                    }

                    statsYear = industry.statsYear;
                    statsMonth = industry.statsMonth;
                }

                int loadRoundRobinIndex = 0;
                if (document.formatVersion >= 9)
                {
                    if (industry.loadRoundRobinIndex < 0)
                    {
                        throw new SaveFormatException(
                            $"Industry placement {index} has an invalid loadRoundRobinIndex value.");
                    }

                    loadRoundRobinIndex = industry.loadRoundRobinIndex;
                }

                CargoAmount[] inputLeftover = document.formatVersion >= 23
                    ? ReadCargoEntries(industry.inputLeftover, $"Industry placement {index} input leftover")
                    : Array.Empty<CargoAmount>();

                int productionLevel = 0;
                int servedMonths = 0;
                if (document.formatVersion >= 24)
                {
                    if (industry.productionLevel < 0 || industry.servedMonths < 0)
                    {
                        throw new SaveFormatException(
                            $"Industry placement {index} has an invalid production level or served month count.");
                    }

                    productionLevel = industry.productionLevel;
                    servedMonths = industry.servedMonths;
                }

                industries[index] = new PlacedIndustry(
                    industry.industryId,
                    industry.instanceIndex,
                    new TileCoordinate(industry.x, industry.y),
                    industryCargo,
                    monthProduced,
                    monthTransported,
                    statsYear,
                    statsMonth,
                    loadRoundRobinIndex,
                    inputLeftover,
                    productionLevel,
                    servedMonths);
            }

            RoadStation[]? roadStations = null;
            if (document.formatVersion >= 3)
            {
                SavedRoadStation[] savedStations = document.roadStations!;
                roadStations = new RoadStation[savedStations.Length];
                for (int index = 0; index < savedStations.Length; index++)
                {
                    SavedRoadStation? station = savedStations[index];
                    if (station == null)
                    {
                        throw new SaveFormatException($"Road station {index} is missing.");
                    }

                    // Format v5–v8 may still serialize station cargo; v9+ ignores it (stations are stops only).
                    roadStations[index] = new RoadStation(
                        station.id,
                        new TileCoordinate(station.x, station.y));
                }
            }

            RoadVehicle[]? roadVehicles = null;
            if (document.formatVersion >= 4)
            {
                roadVehicles = ReadRoadVehicles(document.formatVersion, document.roadVehicles!, "Road vehicle");
            }

            RailStation[]? railStations = null;
            if (document.formatVersion >= 11)
            {
                SavedRailStation[] savedStations = document.railStations!;
                railStations = new RailStation[savedStations.Length];
                for (int index = 0; index < savedStations.Length; index++)
                {
                    SavedRailStation? station = savedStations[index];
                    if (station == null)
                    {
                        throw new SaveFormatException($"Rail station {index} is missing.");
                    }

                    railStations[index] = new RailStation(
                        station.id,
                        new TileCoordinate(station.x, station.y));
                }
            }

            RailVehicle[]? railVehicles = null;
            if (document.formatVersion >= 11)
            {
                railVehicles = ReadRailVehicles(document.formatVersion, document.railVehicles!, "Rail vehicle");
            }

            IReadOnlyList<string>? unlockedVehicleTypeIds = null;
            if (document.formatVersion >= 19)
            {
                unlockedVehicleTypeIds = document.unlockedVehicleTypeIds ?? Array.Empty<string>();
            }

            IReadOnlyList<AchievementCompletion>? achievementCompletions = null;
            IReadOnlyList<CargoDeliveryCounter>? cargoDeliveryCounters = null;
            if (document.formatVersion >= 20)
            {
                achievementCompletions = ReadAchievementCompletions(document.achievementCompletions);
                cargoDeliveryCounters = ReadCargoDeliveryCounters(document.cargoDeliveryCounters);
            }

            IReadOnlyList<Contract>? contractList = null;
            int nextContractId = 0;
            Pcg32State? contractRandomState = null;
            if (document.formatVersion >= 21)
            {
                // Format 26 reworked contracts (Issue #227): older saves keep the id counter and generator but lose
                // their contracts, so their entries are not read.
                contractList = document.formatVersion >= 26
                    ? ReadContractList(document.contractList)
                    : Array.Empty<Contract>();
                nextContractId = document.nextContractId;
                contractRandomState = ReadContractRandomState(document.contractRandom);
            }

            FinanceLedger? financeLedger = null;
            if (document.formatVersion >= 22)
            {
                financeLedger = ReadFinanceLedger(document.financeLedger);
            }

            CompanyStatistics? companyStatistics = null;
            if (document.formatVersion >= 25)
            {
                companyStatistics = ReadCompanyStatistics(document.companyStatistics);
            }

            RouteSaveData? routeData = null;
            if (document.formatVersion >= 27)
            {
                try
                {
                    routeData = ReadRouteData(document);
                }
                catch (ArgumentException exception)
                {
                    throw new SaveFormatException($"The JSON save contains invalid routes: {exception.Message}", exception);
                }
            }

            SaveSnapshot snapshot;
            try
            {
                snapshot = new SaveSnapshot(
                    document.formatVersion,
                    mapSeed,
                    new Tick(currentTick),
                    (MapSize)document.mapSize,
                    document.terrainTypeIds,
                    document.objectIds,
                    document.ownerIds,
                    document.formatVersion >= 2 ? document.roadTypeIds : null,
                    document.formatVersion >= 10 ? document.railTypeIds : null,
                    towns,
                    industries,
                    roadStations,
                    roadVehicles,
                    companyCash,
                    railStations,
                    railVehicles,
                    townStates,
                    document.formatVersion >= 17 ? document.lastUpkeepYear : 0,
                    document.formatVersion >= 17 ? document.lastUpkeepMonth : 0,
                    unlockedVehicleTypeIds,
                    achievementCompletions,
                    cargoDeliveryCounters,
                    contractList,
                    nextContractId,
                    contractRandomState,
                    financeLedger,
                    companyStatistics,
                    routeData);
            }
            catch (ArgumentException exception)
            {
                throw new SaveFormatException($"The JSON save contains invalid world data: {exception.Message}", exception);
            }

            return _migrationPipeline.Migrate(snapshot);
        }

        private static string Decompress(byte[] compressedData)
        {
            try
            {
                using (var input = new MemoryStream(compressedData, false))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    gzip.CopyTo(output);
                    return _utf8.GetString(output.ToArray());
                }
            }
            catch (IOException exception)
            {
                throw new SaveFormatException("The gzip save data is corrupt or truncated.", exception);
            }
            catch (InvalidDataException exception)
            {
                throw new SaveFormatException("The gzip save data is corrupt or truncated.", exception);
            }
            catch (DecoderFallbackException exception)
            {
                throw new SaveFormatException("The decompressed save is not valid UTF-8.", exception);
            }
        }

        private static SaveDocument CreateDocument(SaveSnapshot snapshot)
        {
            var townAnchors = new SavedCoordinate[snapshot.TownAnchors.Count];
            for (int index = 0; index < townAnchors.Length; index++)
            {
                TileCoordinate coordinate = snapshot.TownAnchors[index];
                var saved = new SavedCoordinate { x = coordinate.X, y = coordinate.Y };
                if (snapshot.HasTowns)
                {
                    Town town = snapshot.Towns[index];
                    saved.population = town.Population;
                    saved.needs = WriteTownNeeds(town.Needs);
                    saved.statsYear = town.StatsYear;
                    saved.statsMonth = town.StatsMonth;
                    saved.waitingCargo = WriteCargoEntries(town.WaitingCargo);
                    saved.loadRoundRobinIndex = town.LoadRoundRobinIndex;
                    saved.monthPassengersGenerated = town.MonthPassengersGenerated.ToString(
                        CultureInfo.InvariantCulture);
                    saved.monthPassengersTransported = town.MonthPassengersTransported.ToString(
                        CultureInfo.InvariantCulture);
                    saved.monthMailGenerated = town.MonthMailGenerated.ToString(CultureInfo.InvariantCulture);
                    saved.monthMailTransported = town.MonthMailTransported.ToString(CultureInfo.InvariantCulture);
                    saved.name = town.Name;
                    saved.needPlanLevel1 = CopyIds(town.NeedPlan.GetLevel(1));
                    saved.needPlanLevel2 = CopyIds(town.NeedPlan.GetLevel(2));
                    saved.needPlanLevel3 = CopyIds(town.NeedPlan.GetLevel(3));
                    saved.lastMonthGrowth = town.LastMonthGrowth;
                }

                townAnchors[index] = saved;
            }

            var industries = new SavedIndustry[snapshot.Industries.Count];
            for (int index = 0; index < industries.Length; index++)
            {
                PlacedIndustry industry = snapshot.Industries[index];
                industries[index] = new SavedIndustry
                {
                    industryId = industry.IndustryId,
                    instanceIndex = industry.InstanceIndex,
                    x = industry.Coordinate.X,
                    y = industry.Coordinate.Y,
                    cargo = WriteCargoEntries(industry.Cargo),
                    monthProduced = industry.MonthProduced.ToString(CultureInfo.InvariantCulture),
                    monthTransported = industry.MonthTransported.ToString(CultureInfo.InvariantCulture),
                    statsYear = industry.StatsYear,
                    statsMonth = industry.StatsMonth,
                    loadRoundRobinIndex = industry.LoadRoundRobinIndex,
                    inputLeftover = WriteCargoEntries(industry.InputLeftover),
                    productionLevel = industry.ProductionLevel,
                    servedMonths = industry.ServedMonths
                };
            }

            var terrainTypeIds = new int[snapshot.TileCount];
            var objectIds = new int[snapshot.TileCount];
            var ownerIds = new int[snapshot.TileCount];
            var roadTypeIds = new int[snapshot.TileCount];
            var railTypeIds = new int[snapshot.TileCount];
            snapshot.TerrainTypeIds.CopyTo(terrainTypeIds);
            snapshot.ObjectIds.CopyTo(objectIds);
            snapshot.OwnerIds.CopyTo(ownerIds);
            snapshot.RoadTypeIds.CopyTo(roadTypeIds);
            snapshot.RailTypeIds.CopyTo(railTypeIds);

            var roadStations = new SavedRoadStation[snapshot.RoadStations.Count];
            for (int index = 0; index < roadStations.Length; index++)
            {
                RoadStation station = snapshot.RoadStations[index];
                roadStations[index] = new SavedRoadStation
                {
                    id = station.Id,
                    x = station.Coordinate.X,
                    y = station.Coordinate.Y
                };
            }

            SavedRoadVehicle[] roadVehicles = WriteRoadVehicles(snapshot.RoadVehicles);

            var railStations = new SavedRailStation[snapshot.RailStations.Count];
            for (int index = 0; index < railStations.Length; index++)
            {
                RailStation station = snapshot.RailStations[index];
                railStations[index] = new SavedRailStation
                {
                    id = station.Id,
                    x = station.Coordinate.X,
                    y = station.Coordinate.Y
                };
            }

            SavedRailVehicle[] railVehicles = WriteRailVehicles(snapshot.RailVehicles);

            string[] unlockedVehicleTypeIds = Array.Empty<string>();
            if (snapshot.HasUnlockedVehicleTypes)
            {
                unlockedVehicleTypeIds = new string[snapshot.UnlockedVehicleTypeIds.Count];
                for (int index = 0; index < unlockedVehicleTypeIds.Length; index++)
                {
                    unlockedVehicleTypeIds[index] = snapshot.UnlockedVehicleTypeIds[index];
                }
            }

            SavedAchievementCompletion[] achievementCompletions = Array.Empty<SavedAchievementCompletion>();
            SavedCargoDeliveryCounter[] cargoDeliveryCounters = Array.Empty<SavedCargoDeliveryCounter>();
            if (snapshot.HasAchievementProgress)
            {
                achievementCompletions = new SavedAchievementCompletion[snapshot.AchievementCompletions.Count];
                for (int index = 0; index < achievementCompletions.Length; index++)
                {
                    AchievementCompletion completion = snapshot.AchievementCompletions[index];
                    achievementCompletions[index] = new SavedAchievementCompletion
                    {
                        id = completion.AchievementId,
                        year = completion.Date.Year,
                        month = completion.Date.Month,
                        day = completion.Date.Day
                    };
                }

                cargoDeliveryCounters = new SavedCargoDeliveryCounter[snapshot.CargoDeliveryCounters.Count];
                for (int index = 0; index < cargoDeliveryCounters.Length; index++)
                {
                    CargoDeliveryCounter counter = snapshot.CargoDeliveryCounters[index];
                    cargoDeliveryCounters[index] = new SavedCargoDeliveryCounter
                    {
                        cargoId = counter.CargoId,
                        destinationKind = (int)counter.DestinationKind,
                        amount = counter.Amount.ToString(CultureInfo.InvariantCulture)
                    };
                }
            }

            SavedContract[] contractList = Array.Empty<SavedContract>();
            SavedRandomState? contractRandom = null;
            if (snapshot.HasContracts)
            {
                contractList = new SavedContract[snapshot.ContractList.Count];
                for (int index = 0; index < contractList.Length; index++)
                {
                    Contract contract = snapshot.ContractList[index];
                    contractList[index] = new SavedContract
                    {
                        id = contract.Id,
                        cargoId = contract.CargoId,
                        townIndex = contract.TownIndex,
                        amount = contract.Amount,
                        delivered = contract.Delivered.ToString(CultureInfo.InvariantCulture),
                        status = (int)contract.Status,
                        startYear = contract.StartDate.Year,
                        startMonth = contract.StartDate.Month,
                        startDay = contract.StartDate.Day,
                        deadlineYear = contract.Deadline.Year,
                        deadlineMonth = contract.Deadline.Month,
                        deadlineDay = contract.Deadline.Day,
                        settledYear = contract.SettledDate.Year,
                        settledMonth = contract.SettledDate.Month,
                        settledDay = contract.SettledDate.Day
                    };
                }

                contractRandom = new SavedRandomState
                {
                    state = snapshot.ContractRandomState.State.ToString(CultureInfo.InvariantCulture),
                    increment = snapshot.ContractRandomState.Increment.ToString(CultureInfo.InvariantCulture)
                };
            }

            SavedFinanceLedger? financeLedger = null;
            if (snapshot.HasFinanceLedger)
            {
                FinanceLedger ledger = snapshot.FinanceLedger;
                financeLedger = new SavedFinanceLedger
                {
                    year = ledger.Year,
                    current = WriteFinanceAmounts(ledger.Current),
                    previous = WriteFinanceAmounts(ledger.Previous)
                };
            }

            SavedCompanyStatistics? companyStatistics = null;
            if (snapshot.HasCompanyStatistics)
            {
                CompanyStatistics statistics = snapshot.CompanyStatistics;
                companyStatistics = new SavedCompanyStatistics
                {
                    lifetimeIncome = statistics.LifetimeIncome.ToString(CultureInfo.InvariantCulture),
                    contractsCompleted = statistics.ContractsCompleted.ToString(CultureInfo.InvariantCulture),
                    roadTilesTravelled = statistics.RoadTilesTravelled.ToString(CultureInfo.InvariantCulture),
                    railTilesTravelled = statistics.RailTilesTravelled.ToString(CultureInfo.InvariantCulture)
                };
            }

            SavedRoute[] routes = Array.Empty<SavedRoute>();
            SavedRoadVehicle[] unassignedRoadVehicles = Array.Empty<SavedRoadVehicle>();
            SavedRailVehicle[] unassignedRailVehicles = Array.Empty<SavedRailVehicle>();
            int nextRouteId = 0;
            int lastUpkeepDay = 0;
            if (snapshot.FormatVersion >= 27)
            {
                RouteSaveData routeData = snapshot.RouteData;
                routes = WriteRoutes(routeData.Routes);
                unassignedRoadVehicles = WriteRoadVehicles(routeData.UnassignedRoadVehicles);
                unassignedRailVehicles = WriteRailVehicles(routeData.UnassignedRailVehicles);
                nextRouteId = routeData.NextRouteId;
                lastUpkeepDay = routeData.LastUpkeepDay;
            }

            return new SaveDocument
            {
                formatVersion = snapshot.FormatVersion,
                mapSeed = snapshot.MapSeed.ToString(CultureInfo.InvariantCulture),
                currentTick = snapshot.CurrentTick.Value.ToString(CultureInfo.InvariantCulture),
                mapSize = (int)snapshot.MapSize,
                companyCash = snapshot.CompanyCash.ToString(CultureInfo.InvariantCulture),
                lastUpkeepYear = snapshot.LastUpkeepYear,
                lastUpkeepMonth = snapshot.LastUpkeepMonth,
                unlockedVehicleTypeIds = unlockedVehicleTypeIds,
                achievementCompletions = achievementCompletions,
                cargoDeliveryCounters = cargoDeliveryCounters,
                contractList = contractList,
                nextContractId = snapshot.NextContractId,
                contractRandom = contractRandom,
                financeLedger = financeLedger,
                companyStatistics = companyStatistics,
                terrainTypeIds = terrainTypeIds,
                objectIds = objectIds,
                ownerIds = ownerIds,
                roadTypeIds = roadTypeIds,
                railTypeIds = railTypeIds,
                townAnchors = townAnchors,
                industries = industries,
                roadStations = roadStations,
                roadVehicles = roadVehicles,
                railStations = railStations,
                railVehicles = railVehicles,
                routes = routes,
                unassignedRoadVehicles = unassignedRoadVehicles,
                unassignedRailVehicles = unassignedRailVehicles,
                nextRouteId = nextRouteId,
                lastUpkeepDay = lastUpkeepDay
            };
        }

        private static SavedRoute[] WriteRoutes(IReadOnlyList<Route> routes)
        {
            var saved = new SavedRoute[routes.Count];
            for (int index = 0; index < saved.Length; index++)
            {
                Route route = routes[index];
                var stationIds = new int[route.Stops.Count];
                var waitForFull = new bool[route.Stops.Count];
                for (int stopIndex = 0; stopIndex < stationIds.Length; stopIndex++)
                {
                    stationIds[stopIndex] = route.Stops[stopIndex].StationId;
                    waitForFull[stopIndex] = route.Stops[stopIndex].WaitForFull;
                }

                saved[index] = new SavedRoute
                {
                    id = route.Id,
                    name = route.Name,
                    mode = (int)route.Mode,
                    stopStationIds = stationIds,
                    stopWaitForFull = waitForFull,
                    currentMonthRunningCost = route.CurrentMonthRunningCost.ToString(CultureInfo.InvariantCulture),
                    currentMonthRevenue = route.CurrentMonthRevenue.ToString(CultureInfo.InvariantCulture),
                    hasLastMonth = route.HasLastMonth,
                    lastMonthRunningCost = route.LastMonthRunningCost.ToString(CultureInfo.InvariantCulture),
                    lastMonthRevenue = route.LastMonthRevenue.ToString(CultureInfo.InvariantCulture)
                };
            }

            return saved;
        }

        // Format 27 (Issue #229): routes with their stops and finance, the unassigned vehicles, the next route id, and
        // the day of the last daily upkeep charge.
        private static RouteSaveData ReadRouteData(SaveDocument document)
        {
            SavedRoute[] savedRoutes = document.routes ?? throw new SaveFormatException("Routes are missing.");
            var routes = new Route[savedRoutes.Length];
            for (int index = 0; index < savedRoutes.Length; index++)
            {
                SavedRoute route = savedRoutes[index] ?? throw new SaveFormatException($"Route {index} is missing.");
                string context = $"Route {index}";
                int[] stationIds = route.stopStationIds ?? Array.Empty<int>();
                bool[] waitForFull = route.stopWaitForFull ?? Array.Empty<bool>();
                if (waitForFull.Length != stationIds.Length)
                {
                    throw new SaveFormatException($"{context} stop wait-for-full flags length mismatch.");
                }

                var stops = new RouteStop[stationIds.Length];
                for (int stopIndex = 0; stopIndex < stops.Length; stopIndex++)
                {
                    if (stationIds[stopIndex] < 0)
                    {
                        throw new SaveFormatException($"{context} has an invalid stop station id.");
                    }

                    stops[stopIndex] = new RouteStop(stationIds[stopIndex], waitForFull[stopIndex]);
                }

                routes[index] = new Route(
                    route.id,
                    route.name,
                    (RouteMode)route.mode,
                    stops,
                    ReadNonNegativeLong(route.currentMonthRunningCost, $"{context} running cost"),
                    ReadNonNegativeLong(route.currentMonthRevenue, $"{context} revenue"),
                    route.hasLastMonth,
                    ReadNonNegativeLong(route.lastMonthRunningCost, $"{context} last month running cost"),
                    ReadNonNegativeLong(route.lastMonthRevenue, $"{context} last month revenue"));
            }

            return new RouteSaveData(
                routes,
                ReadRoadVehicles(
                    document.formatVersion,
                    document.unassignedRoadVehicles ?? throw new SaveFormatException("Unassigned road vehicles are missing."),
                    "Unassigned road vehicle"),
                ReadRailVehicles(
                    document.formatVersion,
                    document.unassignedRailVehicles ?? throw new SaveFormatException("Unassigned rail vehicles are missing."),
                    "Unassigned rail vehicle"),
                document.nextRouteId,
                document.lastUpkeepDay);
        }

        private static SavedRoadVehicle[] WriteRoadVehicles(IReadOnlyList<RoadVehicle> vehicles)
        {
            var saved = new SavedRoadVehicle[vehicles.Count];
            for (int index = 0; index < saved.Length; index++)
            {
                RoadVehicle vehicle = vehicles[index];
                var orderStationIds = new int[vehicle.Orders.Count];
                var orderWaitForFull = new bool[vehicle.Orders.Count];
                for (int orderIndex = 0; orderIndex < vehicle.Orders.Count; orderIndex++)
                {
                    orderStationIds[orderIndex] = vehicle.Orders[orderIndex].StationId;
                    orderWaitForFull[orderIndex] = vehicle.Orders[orderIndex].WaitForFull;
                }

                saved[index] = new SavedRoadVehicle
                {
                    id = vehicle.Id,
                    typeId = RoadVehicle.LegacyPlaceholderTypeId,
                    typeDefinitionId = vehicle.TypeId,
                    orderStationIds = orderStationIds,
                    orderWaitForFull = orderWaitForFull,
                    orderIndex = vehicle.OrderIndex,
                    isStuck = vehicle.IsStuck,
                    isOnEdge = vehicle.IsOnEdge,
                    edgeFromNodeId = vehicle.EdgeFromNodeId,
                    edgeToNodeId = vehicle.EdgeToNodeId,
                    progressAlongEdgeRaw = vehicle.ProgressAlongEdge.RawValue,
                    cargo = WriteCargoEntries(vehicle.Cargo),
                    cargoCapacity = vehicle.CargoCapacity,
                    isServicingStop = vehicle.IsServicingStop,
                    isUnloadComplete = vehicle.IsUnloadComplete,
                    lifetimeDeliveryIncome = vehicle.LifetimeDeliveryIncome.ToString(CultureInfo.InvariantCulture),
                    passengerCargoOriginTownId = vehicle.PassengerCargoOriginTownId,
                    hasCargoOrigin = vehicle.CargoOriginTile.HasValue,
                    cargoOriginX = vehicle.CargoOriginTile.HasValue ? vehicle.CargoOriginTile.Value.X : 0,
                    cargoOriginY = vehicle.CargoOriginTile.HasValue ? vehicle.CargoOriginTile.Value.Y : 0,
                    deliveryPayRemainder = vehicle.DeliveryPayRemainder,
                    routeId = vehicle.RouteId
                };
            }

            return saved;
        }

        private static SavedRailVehicle[] WriteRailVehicles(IReadOnlyList<RailVehicle> vehicles)
        {
            var saved = new SavedRailVehicle[vehicles.Count];
            for (int index = 0; index < saved.Length; index++)
            {
                RailVehicle vehicle = vehicles[index];
                var orderStationIds = new int[vehicle.Orders.Count];
                var orderWaitForFull = new bool[vehicle.Orders.Count];
                for (int orderIndex = 0; orderIndex < vehicle.Orders.Count; orderIndex++)
                {
                    orderStationIds[orderIndex] = vehicle.Orders[orderIndex].StationId;
                    orderWaitForFull[orderIndex] = vehicle.Orders[orderIndex].WaitForFull;
                }

                saved[index] = new SavedRailVehicle
                {
                    id = vehicle.Id,
                    typeId = RailVehicle.LegacyPlaceholderTypeId,
                    typeDefinitionId = vehicle.TypeId,
                    orderStationIds = orderStationIds,
                    orderWaitForFull = orderWaitForFull,
                    orderIndex = vehicle.OrderIndex,
                    isStuck = vehicle.IsStuck,
                    isOnEdge = vehicle.IsOnEdge,
                    edgeFromNodeId = vehicle.EdgeFromNodeId,
                    edgeToNodeId = vehicle.EdgeToNodeId,
                    progressAlongEdgeRaw = vehicle.ProgressAlongEdge.RawValue,
                    cargo = WriteCargoEntries(vehicle.Cargo),
                    wagonCount = vehicle.WagonCount,
                    cargoCapacity = vehicle.CargoCapacity,
                    maxWagons = vehicle.MaxWagons,
                    capacityPerWagon = vehicle.CapacityPerWagon,
                    isServicingStop = vehicle.IsServicingStop,
                    isUnloadComplete = vehicle.IsUnloadComplete,
                    lifetimeDeliveryIncome = vehicle.LifetimeDeliveryIncome.ToString(CultureInfo.InvariantCulture),
                    passengerCargoOriginTownId = vehicle.PassengerCargoOriginTownId,
                    hasCargoOrigin = vehicle.CargoOriginTile.HasValue,
                    cargoOriginX = vehicle.CargoOriginTile.HasValue ? vehicle.CargoOriginTile.Value.X : 0,
                    cargoOriginY = vehicle.CargoOriginTile.HasValue ? vehicle.CargoOriginTile.Value.Y : 0,
                    deliveryPayRemainder = vehicle.DeliveryPayRemainder,
                    routeId = vehicle.RouteId
                };
            }

            return saved;
        }

        private static RoadVehicle[] ReadRoadVehicles(int formatVersion, SavedRoadVehicle[] savedVehicles, string label)
        {
            var vehicles = new RoadVehicle[savedVehicles.Length];
            for (int index = 0; index < savedVehicles.Length; index++)
            {
                SavedRoadVehicle? vehicle = savedVehicles[index];
                if (vehicle == null)
                {
                    throw new SaveFormatException($"{label} {index} is missing.");
                }

                int[] orderStationIds = vehicle.orderStationIds ?? Array.Empty<int>();
                bool[] waitForFullFlags = vehicle.orderWaitForFull ?? Array.Empty<bool>();
                if (formatVersion >= 5 && waitForFullFlags.Length != orderStationIds.Length)
                {
                    throw new SaveFormatException(
                        $"{label} {index} order wait-for-full flags length mismatch.");
                }

                var orders = new RoadVehicleOrder[orderStationIds.Length];
                for (int orderIndex = 0; orderIndex < orderStationIds.Length; orderIndex++)
                {
                    bool waitForFull = formatVersion >= 5 &&
                                       orderIndex < waitForFullFlags.Length &&
                                       waitForFullFlags[orderIndex];
                    orders[orderIndex] = new RoadVehicleOrder(orderStationIds[orderIndex], waitForFull);
                }

                CargoAmount[] vehicleCargo = formatVersion >= 5
                    ? ReadCargoEntries(vehicle.cargo, $"{label} {index}")
                    : Array.Empty<CargoAmount>();
                int cargoCapacity = formatVersion >= 5 && vehicle.cargoCapacity > 0
                    ? vehicle.cargoCapacity
                    : RoadVehicle.PlaceholderCargoCapacity;

                long lifetimeIncome = 0L;
                if (formatVersion >= 7)
                {
                    if (string.IsNullOrWhiteSpace(vehicle.lifetimeDeliveryIncome) ||
                        !long.TryParse(
                            vehicle.lifetimeDeliveryIncome,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out lifetimeIncome) ||
                        lifetimeIncome < 0L)
                    {
                        throw new SaveFormatException(
                            $"{label} {index} has an invalid lifetime delivery income.");
                    }
                }

                int passengerOrigin = PassengerCargo.NoOriginTownId;
                if (formatVersion >= 15)
                {
                    passengerOrigin = vehicle.passengerCargoOriginTownId;
                    if (passengerOrigin < PassengerCargo.NoOriginTownId)
                    {
                        throw new SaveFormatException(
                            $"{label} {index} has an invalid passenger cargo origin town id.");
                    }
                }

                vehicles[index] = new RoadVehicle(
                    vehicle.id,
                    ResolveRoadTypeDefinitionId(formatVersion, vehicle),
                    orders,
                    vehicle.orderIndex,
                    vehicle.isStuck,
                    vehicle.isOnEdge,
                    vehicle.edgeFromNodeId,
                    vehicle.edgeToNodeId,
                    FixedPoint.FromRaw(vehicle.progressAlongEdgeRaw),
                    vehicleCargo,
                    cargoCapacity,
                    formatVersion >= 5 && vehicle.isServicingStop,
                    formatVersion >= 5 && vehicle.isUnloadComplete,
                    lifetimeIncome,
                    passengerOrigin,
                    ReadCargoOrigin(formatVersion, vehicle.hasCargoOrigin, vehicle.cargoOriginX, vehicle.cargoOriginY, $"{label} {index}"),
                    ReadDeliveryPayRemainder(formatVersion, vehicle.deliveryPayRemainder, $"{label} {index}"),
                    ReadRouteId(formatVersion, vehicle.routeId, $"{label} {index}"));
            }

            return vehicles;
        }

        private static RailVehicle[] ReadRailVehicles(int formatVersion, SavedRailVehicle[] savedVehicles, string label)
        {
            var vehicles = new RailVehicle[savedVehicles.Length];
            for (int index = 0; index < savedVehicles.Length; index++)
            {
                SavedRailVehicle? vehicle = savedVehicles[index];
                if (vehicle == null)
                {
                    throw new SaveFormatException($"{label} {index} is missing.");
                }

                int[] orderStationIds = vehicle.orderStationIds ?? Array.Empty<int>();
                bool[] waitForFullFlags = vehicle.orderWaitForFull ?? Array.Empty<bool>();
                if (waitForFullFlags.Length != orderStationIds.Length)
                {
                    throw new SaveFormatException(
                        $"{label} {index} order wait-for-full flags length mismatch.");
                }

                var orders = new RailVehicleOrder[orderStationIds.Length];
                for (int orderIndex = 0; orderIndex < orderStationIds.Length; orderIndex++)
                {
                    bool waitForFull = orderIndex < waitForFullFlags.Length && waitForFullFlags[orderIndex];
                    orders[orderIndex] = new RailVehicleOrder(orderStationIds[orderIndex], waitForFull);
                }

                CargoAmount[] vehicleCargo = ReadCargoEntries(vehicle.cargo, $"{label} {index}");
                int wagonCount;
                int cargoCapacity;
                int maxWagons;
                int capacityPerWagon;
                if (formatVersion >= 18)
                {
                    maxWagons = vehicle.maxWagons > 0 ? vehicle.maxWagons : RailVehicle.LegacyMaxWagons;
                    capacityPerWagon = vehicle.capacityPerWagon > 0
                        ? vehicle.capacityPerWagon
                        : RailVehicle.LegacyCapacityPerWagon;
                    wagonCount = vehicle.wagonCount;
                    if (wagonCount < 0 || wagonCount > maxWagons)
                    {
                        throw new SaveFormatException(
                            $"{label} {index} has an invalid wagon count.");
                    }

                    cargoCapacity = wagonCount * capacityPerWagon;
                    if (vehicle.cargoCapacity > 0 && vehicle.cargoCapacity != cargoCapacity)
                    {
                        throw new SaveFormatException(
                            $"{label} {index} cargo capacity does not match wagon count.");
                    }
                }
                else if (formatVersion >= 12)
                {
                    maxWagons = RailVehicle.LegacyMaxWagons;
                    capacityPerWagon = RailVehicle.LegacyCapacityPerWagon;
                    wagonCount = vehicle.wagonCount;
                    if (wagonCount < 0 || wagonCount > maxWagons)
                    {
                        throw new SaveFormatException(
                            $"{label} {index} has an invalid wagon count.");
                    }

                    cargoCapacity = wagonCount * capacityPerWagon;
                    if (vehicle.cargoCapacity > 0 && vehicle.cargoCapacity != cargoCapacity)
                    {
                        throw new SaveFormatException(
                            $"{label} {index} cargo capacity does not match wagon count.");
                    }
                }
                else
                {
                    maxWagons = RailVehicle.LegacyMaxWagons;
                    capacityPerWagon = RailVehicle.LegacyCapacityPerWagon;
                    int rawCapacity = vehicle.cargoCapacity > 0
                        ? vehicle.cargoCapacity
                        : RailVehicle.PlaceholderCargoCapacity;
                    wagonCount = DeriveRailWagonCount(rawCapacity);
                    cargoCapacity = wagonCount * capacityPerWagon;
                }

                var cargoInventory = new CargoInventory(vehicleCargo);
                cargoInventory.TrimToMaxTotal(cargoCapacity);

                if (string.IsNullOrWhiteSpace(vehicle.lifetimeDeliveryIncome) ||
                    !long.TryParse(
                        vehicle.lifetimeDeliveryIncome,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long lifetimeIncome) ||
                    lifetimeIncome < 0L)
                {
                    throw new SaveFormatException(
                        $"{label} {index} has an invalid lifetime delivery income.");
                }

                int passengerOrigin = PassengerCargo.NoOriginTownId;
                if (formatVersion >= 15)
                {
                    passengerOrigin = vehicle.passengerCargoOriginTownId;
                    if (passengerOrigin < PassengerCargo.NoOriginTownId)
                    {
                        throw new SaveFormatException(
                            $"{label} {index} has an invalid passenger cargo origin town id.");
                    }
                }

                vehicles[index] = new RailVehicle(
                    vehicle.id,
                    ResolveRailTypeDefinitionId(formatVersion, vehicle),
                    orders,
                    vehicle.orderIndex,
                    vehicle.isStuck,
                    vehicle.isOnEdge,
                    vehicle.edgeFromNodeId,
                    vehicle.edgeToNodeId,
                    FixedPoint.FromRaw(vehicle.progressAlongEdgeRaw),
                    cargoInventory.Stacks,
                    wagonCount,
                    cargoCapacity,
                    vehicle.isServicingStop,
                    vehicle.isUnloadComplete,
                    lifetimeIncome,
                    passengerOrigin,
                    maxWagons,
                    capacityPerWagon,
                    ReadCargoOrigin(formatVersion, vehicle.hasCargoOrigin, vehicle.cargoOriginX, vehicle.cargoOriginY, $"{label} {index}"),
                    ReadDeliveryPayRemainder(formatVersion, vehicle.deliveryPayRemainder, $"{label} {index}"),
                    ReadRouteId(formatVersion, vehicle.routeId, $"{label} {index}"));
            }

            return vehicles;
        }

        [Serializable]
        private sealed class SaveDocument
        {
            public int formatVersion;
            public string? mapSeed;
            public string? currentTick;
            public int mapSize;
            public string? companyCash;
            public int lastUpkeepYear;
            public int lastUpkeepMonth;
            public string[]? unlockedVehicleTypeIds;
            public SavedAchievementCompletion[]? achievementCompletions;
            public SavedCargoDeliveryCounter[]? cargoDeliveryCounters;
            public SavedContract[]? contractList;
            public int nextContractId;
            public SavedRandomState? contractRandom;
            public SavedFinanceLedger? financeLedger;
            public SavedCompanyStatistics? companyStatistics;
            public int[]? terrainTypeIds;
            public int[]? objectIds;
            public int[]? ownerIds;
            public int[]? roadTypeIds;
            public int[]? railTypeIds;
            public SavedCoordinate[]? townAnchors;
            public SavedIndustry[]? industries;
            public SavedRoadStation[]? roadStations;
            public SavedRoadVehicle[]? roadVehicles;
            public SavedRailStation[]? railStations;
            public SavedRailVehicle[]? railVehicles;
            public SavedRoute[]? routes;
            public SavedRoadVehicle[]? unassignedRoadVehicles;
            public SavedRailVehicle[]? unassignedRailVehicles;
            public int nextRouteId;
            public int lastUpkeepDay;
        }

        [Serializable]
        private sealed class SavedRoute
        {
            public int id;
            public string name = string.Empty;
            public int mode;
            public int[]? stopStationIds;
            public bool[]? stopWaitForFull;
            public string? currentMonthRunningCost;
            public string? currentMonthRevenue;
            public bool hasLastMonth;
            public string? lastMonthRunningCost;
            public string? lastMonthRevenue;
        }

        [Serializable]
        private sealed class SavedCoordinate
        {
            public int x;
            public int y;
            public int population;
            public int statsYear;
            public int statsMonth;
            public SavedTownNeed[]? needs;
            public SavedCargoEntry[]? waitingCargo;
            public int loadRoundRobinIndex;
            public string? monthPassengersGenerated;
            public string? monthPassengersTransported;
            public string? monthMailGenerated;
            public string? monthMailTransported;
            public string? name;
            public string[]? needPlanLevel1;
            public string[]? needPlanLevel2;
            public string[]? needPlanLevel3;
            public int lastMonthGrowth;
        }

        [Serializable]
        private sealed class SavedTownNeed
        {
            public string cargoId = string.Empty;
            public int monthSupply;
            public int lastMonthSupply;
        }

        [Serializable]
        private sealed class SavedIndustry
        {
            public string industryId = string.Empty;
            public int instanceIndex;
            public int x;
            public int y;
            public SavedCargoEntry[]? cargo;
            public string? monthProduced;
            public string? monthTransported;
            public int statsYear;
            public int statsMonth;
            public int loadRoundRobinIndex;
            public SavedCargoEntry[]? inputLeftover;
            public int productionLevel;
            public int servedMonths;
        }

        [Serializable]
        private sealed class SavedRoadStation
        {
            public int id;
            public int x;
            public int y;
            public SavedCargoEntry[]? cargo;
        }

        [Serializable]
        private sealed class SavedRoadVehicle
        {
            public int id;
            public int typeId;
            public string? typeDefinitionId;
            public int[]? orderStationIds;
            public bool[]? orderWaitForFull;
            public int orderIndex;
            public bool isStuck;
            public bool isOnEdge;
            public int edgeFromNodeId;
            public int edgeToNodeId;
            public long progressAlongEdgeRaw;
            public SavedCargoEntry[]? cargo;
            public int cargoCapacity;
            public bool isServicingStop;
            public bool isUnloadComplete;
            public string? lifetimeDeliveryIncome;
            public int passengerCargoOriginTownId;
            public bool hasCargoOrigin;
            public int cargoOriginX;
            public int cargoOriginY;
            public int deliveryPayRemainder;
            public int routeId;
        }

        [Serializable]
        private sealed class SavedRailStation
        {
            public int id;
            public int x;
            public int y;
        }

        [Serializable]
        private sealed class SavedRailVehicle
        {
            public int id;
            public int typeId;
            public string? typeDefinitionId;
            public int[]? orderStationIds;
            public bool[]? orderWaitForFull;
            public int orderIndex;
            public bool isStuck;
            public bool isOnEdge;
            public int edgeFromNodeId;
            public int edgeToNodeId;
            public long progressAlongEdgeRaw;
            public SavedCargoEntry[]? cargo;
            public int wagonCount;
            public int cargoCapacity;
            public int maxWagons;
            public int capacityPerWagon;
            public bool isServicingStop;
            public bool isUnloadComplete;
            public string? lifetimeDeliveryIncome;
            public int passengerCargoOriginTownId;
            public bool hasCargoOrigin;
            public int cargoOriginX;
            public int cargoOriginY;
            public int deliveryPayRemainder;
            public int routeId;
        }

        [Serializable]
        private sealed class SavedCargoEntry
        {
            public string cargoId = string.Empty;
            public int amount;
        }

        private static string[] CopyIds(IReadOnlyList<string> ids)
        {
            var copy = new string[ids.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = ids[index];
            }

            return copy;
        }

        private static TileCoordinate? ReadCargoOrigin(int formatVersion, bool hasOrigin, int x, int y, string context)
        {
            // Saves before format 24 do not know where a load started; delivery then pays the minimum distance.
            if (formatVersion < 24 || !hasOrigin)
            {
                return null;
            }

            if (x < 0 || y < 0)
            {
                throw new SaveFormatException($"{context} has an invalid cargo origin tile.");
            }

            return new TileCoordinate(x, y);
        }

        private static int ReadDeliveryPayRemainder(int formatVersion, int remainder, string context)
        {
            if (formatVersion < 24)
            {
                return 0;
            }

            if (remainder < 0)
            {
                throw new SaveFormatException($"{context} has an invalid delivery pay remainder.");
            }

            return remainder;
        }

        // Format 27 saves the route of each vehicle (Issue #229); on-map vehicles have one, unassigned ones -1.
        private static int ReadRouteId(int formatVersion, int routeId, string context)
        {
            if (formatVersion < 27)
            {
                return RoadVehicle.NoRouteId;
            }

            if (routeId < RoadVehicle.NoRouteId)
            {
                throw new SaveFormatException($"{context} has an invalid route id.");
            }

            return routeId;
        }

        private static TownNeedPlan ReadNeedPlan(SavedCoordinate town, string context)
        {
            try
            {
                return new TownNeedPlan(
                    town.needPlanLevel1 ?? Array.Empty<string>(),
                    town.needPlanLevel2 ?? Array.Empty<string>(),
                    town.needPlanLevel3 ?? Array.Empty<string>());
            }
            catch (ArgumentException exception)
            {
                throw new SaveFormatException($"{context} has an invalid need plan: {exception.Message}", exception);
            }
        }

        private static SavedTownNeed[] WriteTownNeeds(IReadOnlyList<TownNeed> needs)
        {
            var saved = new SavedTownNeed[needs.Count];
            for (int index = 0; index < saved.Length; index++)
            {
                saved[index] = new SavedTownNeed
                {
                    cargoId = needs[index].CargoId,
                    monthSupply = needs[index].MonthSupply,
                    lastMonthSupply = needs[index].LastMonthSupply
                };
            }

            return saved;
        }

        private static TownNeed[] ReadTownNeeds(
            SavedTownNeed[]? saved,
            int townIndex,
            bool includeMonthSupply,
            bool includeLastMonthSupply)
        {
            if (saved == null || saved.Length == 0)
            {
                return Array.Empty<TownNeed>();
            }

            var needs = new TownNeed[saved.Length];
            for (int index = 0; index < saved.Length; index++)
            {
                SavedTownNeed? need = saved[index];
                if (need == null || string.IsNullOrWhiteSpace(need.cargoId))
                {
                    throw new SaveFormatException($"Town {townIndex} need {index} is invalid.");
                }

                int monthSupply = includeMonthSupply ? need.monthSupply : 0;
                if (monthSupply < 0)
                {
                    throw new SaveFormatException($"Town {townIndex} need {index} has invalid monthSupply.");
                }

                int lastMonthSupply = includeLastMonthSupply ? need.lastMonthSupply : 0;
                if (lastMonthSupply < 0)
                {
                    throw new SaveFormatException($"Town {townIndex} need {index} has invalid lastMonthSupply.");
                }

                needs[index] = new TownNeed(need.cargoId, monthSupply, lastMonthSupply);
            }

            return needs;
        }

        private static SavedCargoEntry[] WriteCargoEntries(CargoInventory inventory)
        {
            var stacks = new SavedCargoEntry[inventory.Stacks.Count];
            for (int index = 0; index < stacks.Length; index++)
            {
                CargoAmount stack = inventory.Stacks[index];
                stacks[index] = new SavedCargoEntry
                {
                    cargoId = stack.CargoId,
                    amount = stack.Amount
                };
            }

            return stacks;
        }

        private static CargoAmount[] ReadCargoEntries(SavedCargoEntry[]? saved, string context)
        {
            if (saved == null || saved.Length == 0)
            {
                return Array.Empty<CargoAmount>();
            }

            var stacks = new CargoAmount[saved.Length];
            for (int index = 0; index < saved.Length; index++)
            {
                SavedCargoEntry? stack = saved[index];
                if (stack == null || string.IsNullOrWhiteSpace(stack.cargoId) || stack.amount <= 0)
                {
                    throw new SaveFormatException($"{context} cargo stack {index} is invalid.");
                }

                stacks[index] = new CargoAmount(stack.cargoId, stack.amount);
            }

            return stacks;
        }

        [Serializable]
        private sealed class SavedAchievementCompletion
        {
            public string id = string.Empty;
            public int year;
            public int month;
            public int day;
        }

        [Serializable]
        private sealed class SavedCargoDeliveryCounter
        {
            public string cargoId = string.Empty;
            public int destinationKind;
            public string? amount;
        }

        private static AchievementCompletion[] ReadAchievementCompletions(SavedAchievementCompletion[]? saved)
        {
            if (saved == null)
            {
                return Array.Empty<AchievementCompletion>();
            }

            var completions = new AchievementCompletion[saved.Length];
            for (int index = 0; index < saved.Length; index++)
            {
                SavedAchievementCompletion? entry = saved[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                {
                    throw new SaveFormatException($"Achievement completion {index} has no id.");
                }

                try
                {
                    completions[index] = new AchievementCompletion(
                        entry.id,
                        new GameDate(entry.year, entry.month, entry.day));
                }
                catch (ArgumentException exception)
                {
                    throw new SaveFormatException(
                        $"Achievement completion {index} has an invalid date.",
                        exception);
                }
            }

            return completions;
        }

        private static CargoDeliveryCounter[] ReadCargoDeliveryCounters(SavedCargoDeliveryCounter[]? saved)
        {
            if (saved == null)
            {
                return Array.Empty<CargoDeliveryCounter>();
            }

            var counters = new CargoDeliveryCounter[saved.Length];
            for (int index = 0; index < saved.Length; index++)
            {
                SavedCargoDeliveryCounter? entry = saved[index];
                if (entry == null)
                {
                    throw new SaveFormatException($"Cargo delivery counter {index} is null.");
                }

                long amount = ReadNonNegativeLong(entry.amount, $"Cargo delivery counter {index} amount");
                try
                {
                    counters[index] = new CargoDeliveryCounter(
                        entry.cargoId,
                        (SekoyaTransit.Core.Content.DeliveryDestinationKind)entry.destinationKind,
                        amount);
                }
                catch (ArgumentException exception)
                {
                    throw new SaveFormatException(
                        $"Cargo delivery counter {index} is invalid: {exception.Message}",
                        exception);
                }
            }

            return counters;
        }

        [Serializable]
        private sealed class SavedContract
        {
            public int id;
            public string cargoId = string.Empty;
            public int townIndex;
            public int amount;
            public string? delivered;
            public int status;
            public int startYear;
            public int startMonth;
            public int startDay;
            public int deadlineYear;
            public int deadlineMonth;
            public int deadlineDay;
            public int settledYear;
            public int settledMonth;
            public int settledDay;
        }

        [Serializable]
        private sealed class SavedRandomState
        {
            public string? state;
            public string? increment;
        }

        private static Contract[] ReadContractList(SavedContract[]? saved)
        {
            if (saved == null)
            {
                return Array.Empty<Contract>();
            }

            var contracts = new Contract[saved.Length];
            for (int index = 0; index < saved.Length; index++)
            {
                SavedContract? entry = saved[index];
                if (entry == null)
                {
                    throw new SaveFormatException($"Contract {index} is null.");
                }

                long delivered = ReadNonNegativeLong(entry.delivered, $"Contract {index} delivered");
                try
                {
                    contracts[index] = new Contract(
                        entry.id,
                        entry.cargoId,
                        entry.townIndex,
                        entry.amount,
                        delivered,
                        (ContractStatus)entry.status,
                        new GameDate(entry.startYear, entry.startMonth, entry.startDay),
                        new GameDate(entry.deadlineYear, entry.deadlineMonth, entry.deadlineDay),
                        new GameDate(entry.settledYear, entry.settledMonth, entry.settledDay));
                }
                catch (ArgumentException exception)
                {
                    throw new SaveFormatException($"Contract {index} is invalid: {exception.Message}", exception);
                }
            }

            return contracts;
        }

        private static Pcg32State ReadContractRandomState(SavedRandomState? saved)
        {
            if (saved == null ||
                !ulong.TryParse(saved.state, NumberStyles.None, CultureInfo.InvariantCulture, out ulong state) ||
                !ulong.TryParse(saved.increment, NumberStyles.None, CultureInfo.InvariantCulture, out ulong increment))
            {
                throw new SaveFormatException("The contract generator state is missing or invalid.");
            }

            try
            {
                return new Pcg32State(state, increment);
            }
            catch (ArgumentException exception)
            {
                throw new SaveFormatException("The contract generator state is invalid.", exception);
            }
        }

        [Serializable]
        private sealed class SavedFinanceLedger
        {
            public int year;
            public string[]? current;
            public string[]? previous;
        }

        private static string[] WriteFinanceAmounts(FinanceYearTotals totals)
        {
            long[] amounts = totals.ToArray();
            var written = new string[amounts.Length];
            for (int index = 0; index < amounts.Length; index++)
            {
                written[index] = amounts[index].ToString(CultureInfo.InvariantCulture);
            }

            return written;
        }

        private static FinanceLedger ReadFinanceLedger(SavedFinanceLedger? saved)
        {
            if (saved == null || saved.year < 0)
            {
                throw new SaveFormatException("The finance ledger is missing or invalid.");
            }

            var ledger = new FinanceLedger();
            try
            {
                ledger.Restore(
                    saved.year,
                    ReadFinanceAmounts(saved.current, "Finance ledger current year"),
                    ReadFinanceAmounts(saved.previous, "Finance ledger previous year"));
            }
            catch (ArgumentException exception)
            {
                throw new SaveFormatException($"The finance ledger is invalid: {exception.Message}", exception);
            }

            return ledger;
        }

        [Serializable]
        private sealed class SavedCompanyStatistics
        {
            public string? lifetimeIncome;
            public string? contractsCompleted;
            public string? roadTilesTravelled;
            public string? railTilesTravelled;
        }

        private static CompanyStatistics ReadCompanyStatistics(SavedCompanyStatistics? saved)
        {
            if (saved == null)
            {
                throw new SaveFormatException("The company statistics are missing.");
            }

            var statistics = new CompanyStatistics();
            statistics.Restore(
                ReadNonNegativeLong(saved.lifetimeIncome, "Company lifetime income"),
                ReadNonNegativeLong(saved.contractsCompleted, "Company contracts completed"),
                ReadNonNegativeLong(saved.roadTilesTravelled, "Company road tiles travelled"),
                ReadNonNegativeLong(saved.railTilesTravelled, "Company rail tiles travelled"));
            return statistics;
        }

        private static FinanceYearTotals ReadFinanceAmounts(string[]? saved, string context)
        {
            if (saved == null || saved.Length != FinanceCategories.Count)
            {
                throw new SaveFormatException($"{context} must list {FinanceCategories.Count} amounts.");
            }

            var amounts = new long[saved.Length];
            for (int index = 0; index < saved.Length; index++)
            {
                amounts[index] = ReadNonNegativeLong(saved[index], $"{context} amount {index}");
            }

            return new FinanceYearTotals(amounts);
        }

        private static long ReadNonNegativeLong(string? raw, string context)
        {
            if (string.IsNullOrWhiteSpace(raw) ||
                !long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out long value) ||
                value < 0L)
            {
                throw new SaveFormatException($"{context} has an invalid value.");
            }

            return value;
        }

        private static int DeriveRailWagonCount(int cargoCapacity)
        {
            if (cargoCapacity <= 0)
            {
                return 0;
            }

            int wagonCount = cargoCapacity / RailVehicle.LegacyCapacityPerWagon;
            if (wagonCount > RailVehicle.LegacyMaxWagons)
            {
                return RailVehicle.LegacyMaxWagons;
            }

            return wagonCount;
        }

        private static string ResolveRoadTypeDefinitionId(int formatVersion, SavedRoadVehicle vehicle)
        {
            if (formatVersion >= 18)
            {
                if (string.IsNullOrWhiteSpace(vehicle.typeDefinitionId))
                {
                    throw new SaveFormatException("Road vehicle is missing typeDefinitionId.");
                }

                return vehicle.typeDefinitionId;
            }

            if (vehicle.typeId != RoadVehicle.LegacyPlaceholderTypeId)
            {
                throw new SaveFormatException(
                    $"Road vehicle has unsupported legacy type id {vehicle.typeId}.");
            }

            return RoadVehicle.StarterTypeId;
        }

        private static string ResolveRailTypeDefinitionId(int formatVersion, SavedRailVehicle vehicle)
        {
            if (formatVersion >= 18)
            {
                if (string.IsNullOrWhiteSpace(vehicle.typeDefinitionId))
                {
                    throw new SaveFormatException("Rail vehicle is missing typeDefinitionId.");
                }

                return vehicle.typeDefinitionId;
            }

            if (vehicle.typeId != RailVehicle.LegacyPlaceholderTypeId)
            {
                throw new SaveFormatException(
                    $"Rail vehicle has unsupported legacy type id {vehicle.typeId}.");
            }

            return RailVehicle.StarterTypeId;
        }
    }
}
