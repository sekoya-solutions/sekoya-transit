#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Content
{
    /// <summary>
    /// Loads immutable Core definitions from Unity text assets containing content JSON.
    /// </summary>
    public static class ContentLoader
    {
        /// <summary>
        /// Loads cargo, industry, town-needs, and economy definitions from their JSON text assets.
        /// </summary>
        public static ContentCatalog Load(
            TextAsset? cargoAsset,
            TextAsset? industryAsset,
            TextAsset? townsAsset = null,
            TextAsset? economyAsset = null)
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

            return new ContentCatalog(cargo, industries, townNeeds, economy);
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
                    document.passengerWaitingCapacity);
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
                return new EconomySettings(
                    document.startingCash,
                    document.roadVehiclePurchase,
                    document.railLocomotivePurchase,
                    document.railWagonPurchase,
                    document.roadVehicleUpkeepPerMonth,
                    document.railLocomotiveUpkeepPerMonth,
                    document.railWagonUpkeepPerMonth);
            }
            catch (ArgumentException exception)
            {
                throw new ContentLoadException($"Economy settings are invalid: {exception.Message}", exception);
            }
        }

        [Serializable]
        private sealed class EconomyDocument
        {
            public int startingCash;
            public int roadVehiclePurchase;
            public int railLocomotivePurchase;
            public int railWagonPurchase;
            public int roadVehicleUpkeepPerMonth;
            public int railLocomotiveUpkeepPerMonth;
            public int railWagonUpkeepPerMonth;
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
