#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;
using UnityEngine;

namespace SekoyaTransit.Content
{
    /// <summary>
    /// Loads immutable Core definitions from Unity text assets containing content JSON.
    /// </summary>
    public static class ContentLoader
    {
        /// <summary>
        /// Loads cargo, industry, town-needs, economy, vehicle, achievement, and contract definitions from JSON text assets.
        /// </summary>
        public static ContentCatalog Load(
            TextAsset? cargoAsset,
            TextAsset? industryAsset,
            TextAsset? townsAsset = null,
            TextAsset? economyAsset = null,
            TextAsset? vehiclesAsset = null,
            TextAsset? achievementsAsset = null,
            TextAsset? contractsAsset = null,
            TextAsset? worldGenAsset = null)
        {
            CargoDocument cargoDocument = Parse<CargoDocument>(cargoAsset, "cargo");
            IndustryDocument industryDocument = Parse<IndustryDocument>(industryAsset, "industries");

            List<CargoDefinition> cargo = BuildCargo(cargoDocument);
            List<IndustryDefinition> industries = BuildIndustries(industryDocument);
            TownNeedsSettings townNeeds = townsAsset == null
                ? TownNeedsSettings.CreatePlaceholder()
                : BuildTownNeeds(Parse<TownsDocument>(townsAsset, "towns"));
            EconomySettings economy = economyAsset == null
                ? EconomySettings.CreatePlaceholder()
                : BuildEconomy(Parse<EconomyDocument>(economyAsset, "economy"));
            IReadOnlyList<VehicleDefinition> vehicles = vehiclesAsset == null
                ? ContentCatalog.CreatePlaceholderVehicles()
                : BuildVehicles(Parse<VehiclesDocument>(vehiclesAsset, "vehicles"));

            IReadOnlyList<AchievementDefinition> achievements = Array.Empty<AchievementDefinition>();
            AchievementGoalScale? achievementGoalScale = null;
            if (achievementsAsset != null)
            {
                AchievementsDocument achievementsDocument = Parse<AchievementsDocument>(achievementsAsset, "achievements");
                achievements = BuildAchievements(achievementsDocument, cargo);
                achievementGoalScale = BuildAchievementGoalScale(achievementsDocument);
            }

            ContractSettings contracts = contractsAsset == null
                ? ContractSettings.CreatePlaceholder()
                : BuildContracts(Parse<ContractsDocument>(contractsAsset, "contracts"));

            WorldGenerationData? worldGeneration = worldGenAsset == null
                ? null
                : BuildWorldGeneration(Parse<WorldGenDocument>(worldGenAsset, "worldgen"));

            try
            {
                return new ContentCatalog(
                    cargo,
                    industries,
                    townNeeds,
                    economy,
                    vehicles,
                    achievements,
                    contracts,
                    worldGeneration,
                    achievementGoalScale);
            }
            catch (ArgumentException exception) when (worldGeneration != null)
            {
                throw new ContentLoadException($"The content catalog is invalid: {exception.Message}", exception);
            }
        }

        private static WorldGenerationData BuildWorldGeneration(WorldGenDocument document)
        {
            if (document.sizes == null || document.sizes.Length == 0)
            {
                throw new ContentLoadException("The worldgen JSON must contain a sizes array.");
            }

            var definitions = new List<WorldGenerationSizeDefinition>(document.sizes.Length);
            for (int index = 0; index < document.sizes.Length; index++)
            {
                WorldGenSizeEntry? entry = document.sizes[index];
                if (entry == null)
                {
                    throw new ContentLoadException($"World generation size entry {index} is null.");
                }

                MapSize mapSize = entry.size switch
                {
                    "small" => MapSize.Small,
                    "medium" => MapSize.Medium,
                    "large" => MapSize.Large,
                    _ => throw new ContentLoadException(
                        $"World generation size entry {index} has unsupported size '{entry.size}'.")
                };

                if (entry.industries == null)
                {
                    throw new ContentLoadException($"World generation size '{entry.size}' has no industries array.");
                }

                var industries = new List<WorldGenerationIndustryEntry>(entry.industries.Length);
                for (int industryIndex = 0; industryIndex < entry.industries.Length; industryIndex++)
                {
                    WorldGenIndustryEntry? industry = entry.industries[industryIndex];
                    if (industry == null)
                    {
                        throw new ContentLoadException(
                            $"World generation size '{entry.size}' industry entry {industryIndex} is null.");
                    }

                    try
                    {
                        industries.Add(
                            new WorldGenerationIndustryEntry(industry.id, industry.count, industry.maxProducerDistance));
                    }
                    catch (ArgumentException exception)
                    {
                        throw new ContentLoadException(
                            $"World generation size '{entry.size}' industry entry {industryIndex} is invalid: {exception.Message}",
                            exception);
                    }
                }

                try
                {
                    definitions.Add(
                        new WorldGenerationSizeDefinition(
                            mapSize,
                            entry.townCount,
                            entry.townSpacing,
                            entry.industrySpacing,
                            industries));
                }
                catch (ArgumentException exception)
                {
                    throw new ContentLoadException(
                        $"World generation size '{entry.size}' is invalid: {exception.Message}",
                        exception);
                }
            }

            try
            {
                return new WorldGenerationData(definitions);
            }
            catch (ArgumentException exception)
            {
                throw new ContentLoadException($"World generation is invalid: {exception.Message}", exception);
            }
        }

        [Serializable]
        private sealed class WorldGenDocument
        {
            public WorldGenSizeEntry?[]? sizes;
        }

        [Serializable]
        private sealed class WorldGenSizeEntry
        {
            public string size = string.Empty;
            public int townCount;
            public int townSpacing;
            public int industrySpacing;
            public WorldGenIndustryEntry?[]? industries;
        }

        [Serializable]
        private sealed class WorldGenIndustryEntry
        {
            public string id = string.Empty;
            public int count;
            public int maxProducerDistance;
        }

        private static ContractSettings BuildContracts(ContractsDocument document)
        {
            try
            {
                if (document.limits == null)
                {
                    throw new ArgumentException("The contract limits are missing.");
                }

                return new ContractSettings(
                    BuildLimits(document.limits.small),
                    BuildLimits(document.limits.medium),
                    BuildLimits(document.limits.large),
                    document.initialProposals,
                    document.minAmount,
                    document.maxAmount,
                    document.durationMonths,
                    (decimal)document.rewardMultiplier,
                    document.townPopulationBonus);
            }
            catch (ArgumentException exception)
            {
                throw new ContentLoadException($"Contract settings are invalid: {exception.Message}", exception);
            }
        }

        private static ContractLimits BuildLimits(ContractLimitsEntry? entry)
        {
            if (entry == null)
            {
                throw new ArgumentException("The contract limits of a map size are missing.");
            }

            return new ContractLimits(entry.maxContracts, entry.maxAccepted);
        }

        [Serializable]
        private sealed class ContractLimitsEntry
        {
            public int maxContracts;
            public int maxAccepted;
        }

        [Serializable]
        private sealed class ContractLimitsDocument
        {
            public ContractLimitsEntry? small;
            public ContractLimitsEntry? medium;
            public ContractLimitsEntry? large;
        }

        [Serializable]
        private sealed class ContractsDocument
        {
            public ContractLimitsDocument? limits;
            public int initialProposals;
            public int minAmount;
            public int maxAmount;
            public int durationMonths;
            public double rewardMultiplier;
            public int townPopulationBonus;
        }

        private static TDocument Parse<TDocument>(TextAsset? asset, string familyName)
            where TDocument : class
        {
            if (asset == null)
            {
                throw new ContentLoadException($"The {familyName} JSON TextAsset is missing.");
            }

            if (string.IsNullOrWhiteSpace(asset.text))
            {
                throw new ContentLoadException($"The {familyName} JSON TextAsset '{asset.name}' is empty.");
            }

            try
            {
                TDocument? document = JsonUtility.FromJson<TDocument>(asset.text);
                if (document == null)
                {
                    throw new ContentLoadException($"The {familyName} JSON TextAsset '{asset.name}' has no document.");
                }

                return document;
            }
            catch (ArgumentException exception)
            {
                throw new ContentLoadException(
                    $"The {familyName} JSON TextAsset '{asset.name}' could not be parsed: {exception.Message}",
                    exception);
            }
        }

        private static List<CargoDefinition> BuildCargo(CargoDocument document)
        {
            if (document.cargo == null || document.cargo.Length == 0)
            {
                throw new ContentLoadException("The cargo JSON must contain at least one cargo definition.");
            }

            var definitions = new List<CargoDefinition>(document.cargo.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < document.cargo.Length; index++)
            {
                CargoEntry? entry = document.cargo[index];
                if (entry == null)
                {
                    throw new ContentLoadException($"Cargo entry {index} is null.");
                }

                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    throw new ContentLoadException($"Cargo entry {index} has no ID.");
                }

                if (!ids.Add(entry.id))
                {
                    throw new ContentLoadException($"Cargo entry {index} duplicates cargo ID '{entry.id}'.");
                }

                CargoClass cargoClass = entry.@class switch
                {
                    "passengers" => CargoClass.Passengers,
                    "cargo" => CargoClass.Cargo,
                    _ => throw new ContentLoadException(
                        $"Cargo '{entry.id}' has unsupported class '{entry.@class}'.")
                };

                if (string.IsNullOrWhiteSpace(entry.nameKey))
                {
                    throw new ContentLoadException($"Cargo '{entry.id}' has no localization key.");
                }

                try
                {
                    definitions.Add(
                        new CargoDefinition(
                            entry.id,
                            entry.nameKey,
                            cargoClass,
                            entry.pricePerUnit,
                            entry.manufacturingLevel));
                }
                catch (ArgumentException exception)
                {
                    throw new ContentLoadException($"Cargo '{entry.id}' is invalid: {exception.Message}", exception);
                }
            }

            return definitions;
        }

        private static TownNeedsSettings BuildTownNeeds(TownsDocument document)
        {
            if (document.tiers == null || document.tiers.Length == 0)
            {
                throw new ContentLoadException("The towns JSON must contain at least one growth tier.");
            }

            if (document.names == null || document.names.Length == 0)
            {
                throw new ContentLoadException("The towns JSON must list town names.");
            }

            var tiers = new List<TownNeedTier>(document.tiers.Length);
            for (int index = 0; index < document.tiers.Length; index++)
            {
                TownTierEntry? entry = document.tiers[index];
                if (entry == null)
                {
                    throw new ContentLoadException($"Towns tier entry {index} is null.");
                }

                try
                {
                    tiers.Add(new TownNeedTier(entry.minPopulation, entry.level1, entry.level2, entry.level3));
                }
                catch (ArgumentException exception)
                {
                    throw new ContentLoadException($"Towns tier {index} is invalid: {exception.Message}", exception);
                }
            }

            try
            {
                return new TownNeedsSettings(
                    document.minStartingPopulation,
                    document.maxStartingPopulation,
                    document.maxPopulation,
                    tiers,
                    document.demandPerNeedPerMonth,
                    document.populationPerSatisfiedNeed,
                    document.passengersPerPopulationUnit,
                    document.mailPerPopulationUnit,
                    document.passengerWaitingCapacity,
                    document.minTownsPerNeededCargo,
                    document.names ?? Array.Empty<string>());
            }
            catch (ArgumentException exception)
            {
                throw new ContentLoadException($"Towns settings are invalid: {exception.Message}", exception);
            }
        }

        private static List<IndustryDefinition> BuildIndustries(IndustryDocument document)
        {
            if (document.industries == null || document.industries.Length == 0)
            {
                throw new ContentLoadException("The industries JSON must contain at least one industry definition.");
            }

            var definitions = new List<IndustryDefinition>(document.industries.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < document.industries.Length; index++)
            {
                IndustryEntry? entry = document.industries[index];
                if (entry == null)
                {
                    throw new ContentLoadException($"Industry entry {index} is null.");
                }

                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    throw new ContentLoadException($"Industry entry {index} has no ID.");
                }

                if (!ids.Add(entry.id))
                {
                    throw new ContentLoadException($"Industry entry {index} duplicates industry ID '{entry.id}'.");
                }

                if (string.IsNullOrWhiteSpace(entry.nameKey))
                {
                    throw new ContentLoadException($"Industry '{entry.id}' has no localization key.");
                }

                if (entry.inputs == null)
                {
                    throw new ContentLoadException($"Industry '{entry.id}' has no inputs array.");
                }

                if (entry.outputs == null || entry.outputs.Length == 0)
                {
                    throw new ContentLoadException($"Industry '{entry.id}' must have at least one output.");
                }

                try
                {
                    definitions.Add(
                        new IndustryDefinition(
                            entry.id,
                            entry.nameKey,
                            entry.level,
                            BuildAmounts(entry.id, "input", entry.inputs),
                            BuildAmounts(entry.id, "output", entry.outputs)));
                }
                catch (ArgumentException exception)
                {
                    throw new ContentLoadException($"Industry '{entry.id}' is invalid: {exception.Message}", exception);
                }
            }

            return definitions;
        }

        private static List<CargoAmountDefinition> BuildAmounts(
            string industryId,
            string role,
            CargoAmountEntry?[] entries)
        {
            var amounts = new List<CargoAmountDefinition>(entries.Length);
            for (int index = 0; index < entries.Length; index++)
            {
                CargoAmountEntry? entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.cargo))
                {
                    throw new ContentLoadException($"Industry '{industryId}' {role} entry {index} has no cargo ID.");
                }

                try
                {
                    amounts.Add(new CargoAmountDefinition(entry.cargo, entry.amount));
                }
                catch (ArgumentException exception)
                {
                    throw new ContentLoadException(
                        $"Industry '{industryId}' {role} entry {index} is invalid: {exception.Message}",
                        exception);
                }
            }

            return amounts;
        }

        private static EconomySettings BuildEconomy(EconomyDocument document)
        {
            try
            {
                if (document.deliveryPay == null || document.industryGrowth == null)
                {
                    throw new ContentLoadException("Economy settings are invalid: deliveryPay and industryGrowth are required.");
                }

                return new EconomySettings(
                    document.startingCash,
                    new DeliveryPaySettings(document.deliveryPay.referenceDistance, document.deliveryPay.minimumDistance),
                    new IndustryGrowthSettings(
                        document.industryGrowth.servedPercent,
                        document.industryGrowth.monthsToGrow,
                        document.industryGrowth.maxLevel,
                        document.industryGrowth.levelStepPercent));
            }
            catch (ArgumentException exception)
            {
                throw new ContentLoadException($"Economy settings are invalid: {exception.Message}", exception);
            }
        }

        private static List<VehicleDefinition> BuildVehicles(VehiclesDocument document)
        {
            if (document.vehicles == null || document.vehicles.Length == 0)
            {
                throw new ContentLoadException("The vehicles JSON must contain at least one vehicle definition.");
            }

            var definitions = new List<VehicleDefinition>(document.vehicles.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < document.vehicles.Length; index++)
            {
                VehicleEntry? entry = document.vehicles[index];
                if (entry == null)
                {
                    throw new ContentLoadException($"Vehicle entry {index} is null.");
                }

                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    throw new ContentLoadException($"Vehicle entry {index} has no ID.");
                }

                if (!ids.Add(entry.id))
                {
                    throw new ContentLoadException($"Vehicle entry {index} duplicates vehicle ID '{entry.id}'.");
                }

                VehicleMode mode = entry.mode switch
                {
                    "road" => VehicleMode.Road,
                    "rail" => VehicleMode.Rail,
                    "rail_wagon" => VehicleMode.RailWagon,
                    _ => throw new ContentLoadException(
                        $"Vehicle '{entry.id}' has unsupported mode '{entry.mode}'.")
                };

                if (string.IsNullOrWhiteSpace(entry.nameKey))
                {
                    throw new ContentLoadException($"Vehicle '{entry.id}' has no localization key.");
                }

                try
                {
                    definitions.Add(
                        new VehicleDefinition(
                            entry.id,
                            entry.nameKey,
                            mode,
                            entry.purchase,
                            entry.upkeepPerMonth,
                            entry.availableYear,
                            entry.unlockCost,
                            entry.starting,
                            entry.cargoCapacity,
                            entry.maxWagons,
                            entry.capacityPerWagon,
                            entry.speedThousandths));
                }
                catch (ArgumentException exception)
                {
                    throw new ContentLoadException(
                        $"Vehicle '{entry.id}' is invalid: {exception.Message}",
                        exception);
                }
            }

            return definitions;
        }

        private static List<AchievementDefinition> BuildAchievements(
            AchievementsDocument document,
            IReadOnlyList<CargoDefinition> cargo)
        {
            if (document.achievements == null)
            {
                throw new ContentLoadException("The achievements JSON must contain an achievements array.");
            }

            var cargoIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < cargo.Count; index++)
            {
                cargoIds.Add(cargo[index].Id);
            }

            var definitions = new List<AchievementDefinition>(document.achievements.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            bool hasGlobalTrack = false;
            for (int index = 0; index < document.achievements.Length; index++)
            {
                AchievementEntry? entry = document.achievements[index];
                if (entry == null)
                {
                    throw new ContentLoadException($"Achievement entry {index} is null.");
                }

                if (string.IsNullOrWhiteSpace(entry.id))
                {
                    throw new ContentLoadException($"Achievement entry {index} has no ID.");
                }

                if (!ids.Add(entry.id))
                {
                    throw new ContentLoadException($"Achievement entry {index} duplicates achievement ID '{entry.id}'.");
                }

                AchievementKind kind = entry.kind switch
                {
                    "achievementsUnlocked" => AchievementKind.AchievementsUnlocked,
                    "lifetimeIncome" => AchievementKind.LifetimeIncome,
                    "contractsCompleted" => AchievementKind.ContractsCompleted,
                    "vehicleTypesUnlocked" => AchievementKind.VehicleTypesUnlocked,
                    "roadVehicleCount" => AchievementKind.RoadVehicleCount,
                    "trainCount" => AchievementKind.TrainCount,
                    "roadTilesTravelled" => AchievementKind.RoadTilesTravelled,
                    "railTilesTravelled" => AchievementKind.RailTilesTravelled,
                    "largestTown" => AchievementKind.LargestTown,
                    "cargoSold" => AchievementKind.CargoSold,
                    _ => throw new ContentLoadException(
                        $"Achievement '{entry.id}' has unsupported kind '{entry.kind}'.")
                };

                if (kind == AchievementKind.AchievementsUnlocked)
                {
                    if (hasGlobalTrack)
                    {
                        throw new ContentLoadException(
                            $"Achievement '{entry.id}' is a second achievementsUnlocked track; only one is allowed.");
                    }

                    hasGlobalTrack = true;
                }

                string? cargoId = string.IsNullOrEmpty(entry.cargo) ? null : entry.cargo;
                if (cargoId != null && !cargoIds.Contains(cargoId))
                {
                    throw new ContentLoadException(
                        $"Achievement '{entry.id}' references unknown cargo ID '{cargoId}'.");
                }

                if (entry.milestones == null)
                {
                    throw new ContentLoadException($"Achievement '{entry.id}' has no milestones array.");
                }

                try
                {
                    var milestones = new AchievementMilestone[entry.milestones.Length];
                    for (int tier = 0; tier < milestones.Length; tier++)
                    {
                        AchievementMilestoneEntry milestone = entry.milestones[tier] ??
                            throw new ContentLoadException($"Achievement '{entry.id}' milestone {tier} is null.");
                        milestones[tier] = new AchievementMilestone(milestone.goal, milestone.nameKey);
                    }

                    definitions.Add(
                        new AchievementDefinition(
                            entry.id,
                            kind,
                            milestones,
                            string.IsNullOrEmpty(entry.labelKey) ? null : entry.labelKey,
                            cargoId,
                            entry.scalesWithMapSize));
                }
                catch (ArgumentException exception)
                {
                    throw new ContentLoadException(
                        $"Achievement '{entry.id}' is invalid: {exception.Message}",
                        exception);
                }
            }

            return definitions;
        }

        private static AchievementGoalScale BuildAchievementGoalScale(AchievementsDocument document)
        {
            AchievementGoalScaleEntry? scale = document.goalScalePercent;
            if (scale == null)
            {
                throw new ContentLoadException("The achievements JSON must contain a goalScalePercent object.");
            }

            try
            {
                return new AchievementGoalScale(scale.small, scale.medium, scale.large);
            }
            catch (ArgumentException exception)
            {
                throw new ContentLoadException(
                    $"The achievements goalScalePercent is invalid: {exception.Message}",
                    exception);
            }
        }

        [Serializable]
        private sealed class AchievementsDocument
        {
            public AchievementGoalScaleEntry? goalScalePercent;
            public AchievementEntry?[]? achievements;
        }

        [Serializable]
        private sealed class AchievementGoalScaleEntry
        {
            public int small;
            public int medium;
            public int large;
        }

        [Serializable]
        private sealed class AchievementEntry
        {
            public string id = string.Empty;
            public string kind = string.Empty;
            public string? labelKey;
            public string? cargo;
            public bool scalesWithMapSize;
            public AchievementMilestoneEntry?[]? milestones;
        }

        [Serializable]
        private sealed class AchievementMilestoneEntry
        {
            public long goal;
            public string nameKey = string.Empty;
        }

        [Serializable]
        private sealed class EconomyDocument
        {
            public int startingCash;
            public DeliveryPayEntry? deliveryPay;
            public IndustryGrowthEntry? industryGrowth;
        }

        [Serializable]
        private sealed class DeliveryPayEntry
        {
            public int referenceDistance;
            public int minimumDistance;
        }

        [Serializable]
        private sealed class IndustryGrowthEntry
        {
            public int servedPercent;
            public int monthsToGrow;
            public int maxLevel;
            public int levelStepPercent;
        }

        [Serializable]
        private sealed class VehiclesDocument
        {
            public VehicleEntry[]? vehicles;
        }

        [Serializable]
        private sealed class VehicleEntry
        {
            public string id = string.Empty;
            public string nameKey = string.Empty;
            public string mode = string.Empty;
            public int cargoCapacity;
            public int maxWagons;
            public int capacityPerWagon;
            public int speedThousandths;
            public long purchase;
            public long upkeepPerMonth;
            public int availableYear;
            public long unlockCost;
            public bool starting;
        }

        [Serializable]
        private sealed class CargoDocument
        {
            public CargoEntry?[]? cargo;
        }

        [Serializable]
        private sealed class CargoEntry
        {
            public string? id;
            public string? nameKey;
            public string? @class;
            public int pricePerUnit;
            public int manufacturingLevel;
        }

        [Serializable]
        private sealed class TownsDocument
        {
            public int minStartingPopulation;
            public int maxStartingPopulation;
            public int maxPopulation;
            public int demandPerNeedPerMonth;
            public int populationPerSatisfiedNeed;
            public int passengersPerPopulationUnit;
            public int mailPerPopulationUnit;
            public int passengerWaitingCapacity;
            public TownTierEntry?[]? tiers;
            public int minTownsPerNeededCargo;
            public string[]? names;
        }

        [Serializable]
        private sealed class TownTierEntry
        {
            public int minPopulation;
            public int level1;
            public int level2;
            public int level3;
        }

        [Serializable]
        private sealed class IndustryDocument
        {
            public IndustryEntry?[]? industries;
        }

        [Serializable]
        private sealed class IndustryEntry
        {
            public string? id;
            public string? nameKey;
            public int level;
            public CargoAmountEntry?[]? inputs;
            public CargoAmountEntry?[]? outputs;
        }

        [Serializable]
        private sealed class CargoAmountEntry
        {
            public string? cargo;
            public int amount;
        }
    }
}
