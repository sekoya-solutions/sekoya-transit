#nullable enable
using System.IO;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;
using UnityEngine;

namespace SekoyaTransit.Content.Tests
{
    public sealed class ContentLoaderTests
    {
        private const string Scale = "\"goalScalePercent\":{\"small\":50,\"medium\":100,\"large\":200},";
        private const string Milestones =
            "\"milestones\":[{\"goal\":1,\"nameKey\":\"b\"},{\"goal\":2,\"nameKey\":\"s\"},{\"goal\":3,\"nameKey\":\"g\"}]";

        private TextAsset? _cargoAsset;
        private TextAsset? _industryAsset;

        [TearDown]
        public void TearDown()
        {
            if (_cargoAsset != null)
            {
                Object.DestroyImmediate(_cargoAsset);
            }

            if (_industryAsset != null)
            {
                Object.DestroyImmediate(_industryAsset);
            }
        }

        [Test]
        public void Load_CheckedInContentPreservesDefinitionsAndRecipes()
        {
            ContentCatalog catalog = LoadCheckedInContent();

            Assert.That(catalog.Cargo, Has.Count.EqualTo(17));
            Assert.That(catalog.Industries, Has.Count.EqualTo(15));
            Assert.That(catalog.TryGetCargo("passengers", out CargoDefinition? passengers), Is.True);
            Assert.That(passengers!.NameKey, Is.EqualTo("cargo.passengers"));
            Assert.That(passengers.Class, Is.EqualTo(CargoClass.Passengers));
            Assert.That(passengers.PricePerUnit, Is.EqualTo(10));
            Assert.That(passengers.ManufacturingLevel, Is.EqualTo(0));
            Assert.That(catalog.TryGetCargo("wood", out CargoDefinition? wood), Is.True);
            Assert.That(wood!.NameKey, Is.EqualTo("cargo.wood"));
            Assert.That(wood.Class, Is.EqualTo(CargoClass.Cargo));
            Assert.That(wood.PricePerUnit, Is.EqualTo(10));
            Assert.That(wood.ManufacturingLevel, Is.EqualTo(1));
            Assert.That(catalog.TownNeeds.MinStartingPopulation, Is.EqualTo(100));
            Assert.That(catalog.TownNeeds.MaxStartingPopulation, Is.EqualTo(500));
            Assert.That(catalog.TownNeeds.DemandPerNeedPerMonth, Is.EqualTo(new TownNeedLevelAmounts(500, 400, 300)));
            Assert.That(catalog.TownNeeds.PartlyMetMinDemandPercent, Is.EqualTo(50));
            Assert.That(catalog.TownNeeds.PopulationPerPartlyMetNeed, Is.EqualTo(new TownNeedLevelAmounts(10, 15, 20)));
            Assert.That(catalog.TownNeeds.PopulationPerFullyMetNeed, Is.EqualTo(new TownNeedLevelAmounts(20, 30, 40)));
            Assert.That(catalog.TownNeeds.AllNeedsMetGrowthMultiplier, Is.EqualTo(2));
            Assert.That(catalog.TownNeeds.Tiers, Has.Count.EqualTo(5));
            Assert.That(catalog.TownNeeds.Tiers[1].MinPopulation, Is.EqualTo(1000));
            Assert.That(catalog.TownNeeds.Tiers[4].MinPopulation, Is.EqualTo(10000));
            Assert.That(catalog.TownNeeds.Tiers[4].Level3Count, Is.EqualTo(3));
            Assert.That(catalog.Economy.StartingCash, Is.EqualTo(100000L));
            Assert.That(catalog.Economy.DeliveryPay.ReferenceDistance, Is.EqualTo(30));
            Assert.That(catalog.Economy.DeliveryPay.MinimumDistance, Is.EqualTo(4));
            Assert.That(catalog.Economy.IndustryGrowth.ServedPercent, Is.EqualTo(80));
            Assert.That(catalog.Economy.IndustryGrowth.MonthsToGrow, Is.EqualTo(12));
            Assert.That(catalog.Economy.IndustryGrowth.MaxLevel, Is.EqualTo(4));
            Assert.That(catalog.Economy.IndustryGrowth.LevelMultiplier, Is.EqualTo(2));
            Assert.That(catalog.StartingRoadVehicle.Id, Is.EqualTo("truck_mk1"));
            Assert.That(catalog.StartingRoadVehicle.Purchase, Is.EqualTo(10000L));
            Assert.That(catalog.StartingRailLocomotive.Id, Is.EqualTo("loco_mk1"));
            Assert.That(catalog.StartingRailLocomotive.Purchase, Is.EqualTo(20000L));
            Assert.That(catalog.RailWagon.Id, Is.EqualTo("rail_wagon"));
            Assert.That(catalog.RailWagon.Purchase, Is.EqualTo(4000L));
            Assert.That(catalog.StartingRoadVehicle.GetMonthlyUpkeep(0), Is.EqualTo(100L));
            Assert.That(catalog.MonthlyUpkeepForRail("loco_mk1", 1), Is.EqualTo(320L));
            Assert.That(catalog.MonthlyUpkeepForRail("loco_mk1", 4), Is.EqualTo(680L));
            Assert.That(catalog.RailWagon.UpkeepPerUnitPerMonth, Is.Zero);
            Assert.That(catalog.RailWagon.FixedUpkeepPerMonth, Is.Zero);
            Assert.That(catalog.Vehicles, Has.Count.EqualTo(11));
            Assert.That(catalog.Achievements, Has.Count.EqualTo(26));
            Assert.That(catalog.Achievements[0].Id, Is.EqualTo("global"));
            Assert.That(catalog.Achievements[0].Kind, Is.EqualTo(AchievementKind.AchievementsUnlocked));
            Assert.That(catalog.Achievements[0].Milestones[2].Goal, Is.EqualTo(100L));
            Assert.That(catalog.Achievements[1].Kind, Is.EqualTo(AchievementKind.LifetimeIncome));
            Assert.That(catalog.Achievements[1].ScalesWithMapSize, Is.True);
            Assert.That(catalog.Achievements[1].Milestones[0].NameKey, Is.EqualTo("achievement.lifetime_income.bronze"));
            Assert.That(catalog.Achievements[11].CargoId, Is.EqualTo("wood"));
            Assert.That(catalog.Achievements[11].LabelKey, Is.Null);
            Assert.That(catalog.AchievementGoalScale.SmallPercent, Is.EqualTo(50));
            Assert.That(catalog.AchievementGoalScale.LargePercent, Is.EqualTo(200));
            Assert.That(catalog.Contracts.GetLimits(MapSize.Small).MaxAccepted, Is.EqualTo(3));
            Assert.That(catalog.Contracts.MinAmount, Is.EqualTo(50));
            Assert.That(catalog.Contracts.MaxAmount, Is.EqualTo(200));
            Assert.That(catalog.Contracts.GetDurationMonths(1), Is.EqualTo(2));
            Assert.That(catalog.Contracts.GetDurationMonths(2), Is.EqualTo(4));
            Assert.That(catalog.Contracts.GetDurationMonths(3), Is.EqualTo(6));
            Assert.That(catalog.Contracts.InitialDurationMonths, Is.EqualTo(12));
            Assert.That(catalog.Contracts.RewardMultiplier, Is.EqualTo(10.0m));
            Assert.That(catalog.Contracts.TownPopulationBonus, Is.EqualTo(100));
            Assert.That(catalog.WorldGeneration.Get(MapSize.Small).TownCount, Is.EqualTo(15));
            Assert.That(catalog.WorldGeneration.Get(MapSize.Small).Industries, Has.Count.EqualTo(15));
            Assert.That(catalog.TownNeeds.MinTownsPerNeededCargo, Is.EqualTo(3));
            Assert.That(catalog.TownNeeds.Names, Has.Count.EqualTo(188));
            Assert.That(catalog.TownNeeds.Names, Does.Contain("Paris"));
            Assert.That(catalog.WorldGeneration.Get(MapSize.Large).Industries[0].IndustryId, Is.EqualTo("forest"));
            Assert.That(catalog.TryGetCargo("Wood", out _), Is.False);
            Assert.That(catalog.TryGetIndustry("forest", out IndustryDefinition? forest), Is.True);
            Assert.That(forest!.Level, Is.EqualTo(1));
            Assert.That(forest.Inputs, Is.Empty);
            Assert.That(forest.Outputs[0].CargoId, Is.EqualTo("wood"));
            Assert.That(forest.Outputs[0].Amount, Is.EqualTo(4));
            Assert.That(catalog.TryGetIndustry("iron_mine", out IndustryDefinition? ironMine), Is.True);
            Assert.That(ironMine!.Outputs[0].CargoId, Is.EqualTo("iron_ore"));
            Assert.That(ironMine.Outputs[0].Amount, Is.EqualTo(4));
            Assert.That(catalog.TryGetIndustry("sawmill", out IndustryDefinition? sawmill), Is.True);
            Assert.That(sawmill!.Inputs[0].CargoId, Is.EqualTo("wood"));
            Assert.That(sawmill.Inputs[0].Amount, Is.EqualTo(2));
            Assert.That(sawmill.Outputs[0].CargoId, Is.EqualTo("planks"));
            Assert.That(sawmill.Outputs[0].Amount, Is.EqualTo(1));
            Assert.That(catalog.TryGetIndustry("furniture_factory", out IndustryDefinition? factory), Is.True);
            Assert.That(factory!.NameKey, Is.EqualTo("industry.furniture_factory"));
            Assert.That(factory.Level, Is.EqualTo(3));
            Assert.That(factory.Inputs, Has.Count.EqualTo(1));
            Assert.That(factory.Inputs[0].CargoId, Is.EqualTo("planks"));
            Assert.That(factory.Inputs[0].Amount, Is.EqualTo(2));
            Assert.That(factory.Outputs[0].CargoId, Is.EqualTo("furniture"));
            Assert.That(factory.Outputs[0].Amount, Is.EqualTo(1));
            Assert.That(catalog.TryGetIndustry("Forest", out _), Is.False);

            string[] industryIds =
            {
                "forest", "sawmill", "furniture_factory",
                "wheat_farm", "flour_mill", "bakery",
                "iron_mine", "iron_foundry", "machine_shop",
                "livestock_ranch", "butcher", "cannery",
                "sheep_farm", "textile_mill", "clothing_workshop"
            };
            for (int index = 0; index < industryIds.Length; index++)
            {
                Assert.That(catalog.TryGetIndustry(industryIds[index], out _), Is.True, industryIds[index]);
            }
        }

        [Test]
        public void Load_CheckedInIndustryGrowth_DoublesOutputUpToLevelFive()
        {
            // Issue #328: raw industries grow through levels 1 to 5 (zero-based 0 to 4), x2 per level.
            IndustryGrowthSettings growth = LoadCheckedInContent().Economy.IndustryGrowth;

            Assert.That(growth.GetFactor(0), Is.EqualTo(1));
            Assert.That(growth.GetFactor(1), Is.EqualTo(2));
            Assert.That(growth.GetFactor(2), Is.EqualTo(4));
            Assert.That(growth.GetFactor(3), Is.EqualTo(8));
            Assert.That(growth.GetFactor(4), Is.EqualTo(16));
            Assert.That(growth.GetFactor(5), Is.EqualTo(16));
        }

        [Test]
        public void Load_CheckedInCargoPricesFollowManufacturingLevel()
        {
            ContentCatalog catalog = LoadCheckedInContent();
            int[] priceByLevel = { 10, 10, 20, 40 };

            for (int index = 0; index < catalog.Cargo.Count; index++)
            {
                CargoDefinition cargo = catalog.Cargo[index];
                Assert.That(cargo.PricePerUnit, Is.EqualTo(priceByLevel[cargo.ManufacturingLevel]), cargo.Id);
            }

            Assert.That(catalog.TryGetCargo("mail", out CargoDefinition? mail), Is.True);
            Assert.That(mail!.PricePerUnit, Is.EqualTo(10));
            Assert.That(catalog.Contracts.RewardMultiplier, Is.EqualTo(10.0m));
        }

        [Test]
        public void Load_MissingAssetFailsWithoutReturningCatalog()
        {
            _industryAsset = new TextAsset("{\"industries\":[]}");

            ContentLoadException exception = Assert.Throws<ContentLoadException>(
                () => ContentLoader.Load(null, _industryAsset))!;

            StringAssert.Contains("cargo", exception.Message);
            StringAssert.Contains("missing", exception.Message);
        }

        [Test]
        public void Load_MalformedDocumentFailsWithAssetContext()
        {
            _cargoAsset = new TextAsset("{");
            _industryAsset = new TextAsset("{\"industries\":[]}");

            ContentLoadException exception = Assert.Throws<ContentLoadException>(
                () => ContentLoader.Load(_cargoAsset, _industryAsset))!;

            StringAssert.Contains("cargo", exception.Message);
        }

        [Test]
        public void Load_UnsupportedCargoClassFailsExplicitly()
        {
            _cargoAsset = new TextAsset(
                "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"unknown\",\"pricePerUnit\":1}]}");
            _industryAsset = new TextAsset("{\"industries\":[{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":1}]}]}");

            ContentLoadException exception = Assert.Throws<ContentLoadException>(
                () => ContentLoader.Load(_cargoAsset, _industryAsset))!;

            StringAssert.Contains("unsupported class", exception.Message);
            StringAssert.Contains("wood", exception.Message);
        }

        [Test]
        public void Load_MissingRequiredCargoValueFailsExplicitly()
        {
            _cargoAsset = new TextAsset(
                "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"cargo\"}]}");
            _industryAsset = new TextAsset("{\"industries\":[{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":1}]}]}");

            ContentLoadException exception = Assert.Throws<ContentLoadException>(
                () => ContentLoader.Load(_cargoAsset, _industryAsset))!;

            StringAssert.Contains("wood", exception.Message);
            StringAssert.Contains("positive", exception.Message);
        }

        [TestCase("{" + Scale + "\"achievements\":[{\"id\":\"a\",\"kind\":\"trainCount\",\"labelKey\":\"l\",\"milestones\":[{\"goal\":0,\"nameKey\":\"b\"},{\"goal\":2,\"nameKey\":\"s\"},{\"goal\":3,\"nameKey\":\"g\"}]}]}", "positive")]
        [TestCase("{" + Scale + "\"achievements\":[{\"id\":\"a\",\"kind\":\"bogus\",\"labelKey\":\"l\"," + Milestones + "}]}", "unsupported kind")]
        [TestCase("{" + Scale + "\"achievements\":[{\"id\":\"a\",\"kind\":\"cargoSold\",\"cargo\":\"nope\"," + Milestones + "}]}", "unknown cargo")]
        [TestCase("{" + Scale + "\"achievements\":[{\"id\":\"a\",\"kind\":\"trainCount\",\"labelKey\":\"l\"," + Milestones + "},{\"id\":\"a\",\"kind\":\"trainCount\",\"labelKey\":\"l\"," + Milestones + "}]}", "duplicates")]
        [TestCase("{" + Scale + "\"achievements\":[{\"id\":\"a\",\"kind\":\"achievementsUnlocked\",\"labelKey\":\"l\"," + Milestones + "},{\"id\":\"b\",\"kind\":\"achievementsUnlocked\",\"labelKey\":\"l\"," + Milestones + "}]}", "only one")]
        [TestCase("{" + Scale + "\"achievements\":[{\"id\":\"a\",\"kind\":\"trainCount\",\"labelKey\":\"l\",\"milestones\":[{\"goal\":5,\"nameKey\":\"b\"},{\"goal\":5,\"nameKey\":\"s\"},{\"goal\":9,\"nameKey\":\"g\"}]}]}", "strictly increase")]
        [TestCase("{" + Scale + "\"achievements\":[{\"id\":\"a\",\"kind\":\"trainCount\",\"labelKey\":\"l\",\"milestones\":[{\"goal\":1,\"nameKey\":\"b\"},{\"goal\":2,\"nameKey\":\"s\"}]}]}", "exactly 3")]
        [TestCase("{" + Scale + "\"achievements\":[{\"id\":\"a\",\"kind\":\"trainCount\"," + Milestones + "}]}", "label key")]
        [TestCase("{\"achievements\":[]}", "goalScalePercent")]
        public void Load_BadAchievementDataFailsExplicitly(string achievementsJson, string expectedMessage)
        {
            _cargoAsset = new TextAsset(
                "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"cargo\",\"pricePerUnit\":1,\"manufacturingLevel\":1}]}");
            _industryAsset = new TextAsset("{\"industries\":[{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":1}]}]}");
            var achievementsAsset = new TextAsset(achievementsJson);

            ContentLoadException exception = Assert.Throws<ContentLoadException>(
                () => ContentLoader.Load(_cargoAsset, _industryAsset, null, null, null, achievementsAsset))!;

            StringAssert.Contains(expectedMessage, exception.Message);
            Object.DestroyImmediate(achievementsAsset);
        }

        private const string _validLimits =
            "\"limits\":{\"small\":{\"maxContracts\":6,\"maxAccepted\":3},\"medium\":{\"maxContracts\":8,\"maxAccepted\":4},\"large\":{\"maxContracts\":10,\"maxAccepted\":5}}";

        [TestCase("{" + _validLimits + ",\"initialProposals\":-1,\"minAmount\":50,\"maxAmount\":200,\"durationMonthsByLevel\":[1,2,3],\"initialDurationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}", "Initial proposals")]
        [TestCase("{" + _validLimits + ",\"initialProposals\":3,\"minAmount\":0,\"maxAmount\":200,\"durationMonthsByLevel\":[1,2,3],\"initialDurationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}", "Minimum contract amount")]
        [TestCase("{" + _validLimits + ",\"initialProposals\":3,\"minAmount\":300,\"maxAmount\":200,\"durationMonthsByLevel\":[1,2,3],\"initialDurationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}", "Maximum contract amount")]
        [TestCase("{" + _validLimits + ",\"initialProposals\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonthsByLevel\":[0,2,3],\"initialDurationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}", "duration")]
        [TestCase("{" + _validLimits + ",\"initialProposals\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonthsByLevel\":[1,2,3],\"initialDurationMonths\":0,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}", "Initial contract duration")]
        [TestCase("{" + _validLimits + ",\"initialProposals\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonthsByLevel\":[1,2,3],\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}", "Initial contract duration")]
        [TestCase("{" + _validLimits + ",\"initialProposals\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonthsByLevel\":[1,2,3],\"initialDurationMonths\":12,\"rewardMultiplier\":-0.5,\"townPopulationBonus\":100}", "Reward multiplier")]
        [TestCase("{" + _validLimits + ",\"initialProposals\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonthsByLevel\":[1,2,3],\"initialDurationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":-1}", "Town population bonus")]
        [TestCase("{\"limits\":{\"small\":{\"maxContracts\":6,\"maxAccepted\":7},\"medium\":{\"maxContracts\":8,\"maxAccepted\":4},\"large\":{\"maxContracts\":10,\"maxAccepted\":5}},\"initialProposals\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonthsByLevel\":[1,2,3],\"initialDurationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}", "Maximum accepted")]
        [TestCase("{\"initialProposals\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonthsByLevel\":[1,2,3],\"initialDurationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}", "limits")]
        public void Load_BadContractDataFailsExplicitly(string contractsJson, string expectedMessage)
        {
            _cargoAsset = new TextAsset(
                "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"cargo\",\"pricePerUnit\":1,\"manufacturingLevel\":1}]}");
            _industryAsset = new TextAsset("{\"industries\":[{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":1}]}]}");
            var contractsAsset = new TextAsset(contractsJson);

            ContentLoadException exception = Assert.Throws<ContentLoadException>(
                () => ContentLoader.Load(_cargoAsset, _industryAsset, null, null, null, null, contractsAsset))!;

            StringAssert.Contains(expectedMessage, exception.Message);
            Object.DestroyImmediate(contractsAsset);
        }

        private const string _vehicleCommon =
            "\"purchase\":1,\"availableYear\":1900,\"unlockCost\":0,\"starting\":true";
        private const string _truckBody =
            "\"id\":\"truck_mk1\",\"nameKey\":\"v.t\",\"mode\":\"road\",\"cargoCapacity\":10,\"speedThousandths\":250,";
        private const string _locoBody =
            "\"id\":\"loco_mk1\",\"nameKey\":\"v.l\",\"mode\":\"rail\",\"maxWagons\":4,\"capacityPerWagon\":12,\"speedThousandths\":300,";
        private const string _validTruck = "{" + _truckBody + "\"upkeepPerUnitPerMonth\":10," + _vehicleCommon + "}";
        private const string _validLoco =
            "{" + _locoBody + "\"upkeepPerUnitPerMonth\":10,\"fixedUpkeepPerMonth\":200," + _vehicleCommon + "}";
        private const string _validWagon = "{\"id\":\"rail_wagon\",\"nameKey\":\"v.w\",\"mode\":\"rail_wagon\"," + _vehicleCommon + "}";

        // Issue #314: JsonUtility reads an absent number as 0, which would make a vehicle free, so the loader rejects a
        // missing upkeep field where the mode uses it, and the definition rejects upkeep where the mode does not.
        [TestCase("{" + _truckBody + _vehicleCommon + "}", _validLoco, _validWagon, "no upkeepPerUnitPerMonth")]
        [TestCase(_validTruck, "{" + _locoBody + "\"fixedUpkeepPerMonth\":200," + _vehicleCommon + "}", _validWagon, "no upkeepPerUnitPerMonth")]
        [TestCase(_validTruck, "{" + _locoBody + "\"upkeepPerUnitPerMonth\":10," + _vehicleCommon + "}", _validWagon, "no fixedUpkeepPerMonth")]
        [TestCase("{" + _truckBody + "\"upkeepPerUnitPerMonth\":10,\"fixedUpkeepPerMonth\":50," + _vehicleCommon + "}", _validLoco, _validWagon, "fixed upkeep")]
        [TestCase(_validTruck, _validLoco, "{\"id\":\"rail_wagon\",\"nameKey\":\"v.w\",\"mode\":\"rail_wagon\",\"upkeepPerUnitPerMonth\":10," + _vehicleCommon + "}", "must not set upkeep")]
        [TestCase("{" + _truckBody + "\"upkeepPerUnitPerMonth\":-1," + _vehicleCommon + "}", _validLoco, _validWagon, "cannot be negative")]
        public void Load_BadVehicleUpkeepFailsExplicitly(string truck, string loco, string wagon, string expectedMessage)
        {
            _cargoAsset = new TextAsset(
                "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"cargo\",\"pricePerUnit\":1,\"manufacturingLevel\":1}]}");
            _industryAsset = new TextAsset("{\"industries\":[{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":1}]}]}");
            var vehiclesAsset = new TextAsset("{\"vehicles\":[" + truck + "," + loco + "," + wagon + "]}");

            ContentLoadException exception = Assert.Throws<ContentLoadException>(
                () => ContentLoader.Load(_cargoAsset, _industryAsset, null, null, vehiclesAsset))!;

            StringAssert.Contains(expectedMessage, exception.Message);
            Object.DestroyImmediate(vehiclesAsset);
        }

        [Test]
        public void Load_ValidVehicleUpkeep_LoadsRatesAndFee()
        {
            _cargoAsset = new TextAsset(
                "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"cargo\",\"pricePerUnit\":1,\"manufacturingLevel\":1}]}");
            _industryAsset = new TextAsset("{\"industries\":[{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":1}]}]}");
            var vehiclesAsset = new TextAsset("{\"vehicles\":[" + _validTruck + "," + _validLoco + "," + _validWagon + "]}");

            ContentCatalog catalog = ContentLoader.Load(_cargoAsset, _industryAsset, null, null, vehiclesAsset);

            Assert.That(catalog.StartingRoadVehicle.GetMonthlyUpkeep(0), Is.EqualTo(100L));
            Assert.That(catalog.MonthlyUpkeepForRail("loco_mk1", 0), Is.EqualTo(200L));
            Assert.That(catalog.MonthlyUpkeepForRail("loco_mk1", 4), Is.EqualTo(680L));
            Object.DestroyImmediate(vehiclesAsset);
        }

        private const string _forestEntry ="{\"id\":\"forest\",\"count\":2,\"maxProducerDistance\":0}";

        private static string WorldGenSize(string size, string industries)
        {
            return "{\"size\":\"" + size + "\",\"townCount\":2,\"townSpacing\":16,\"industrySpacing\":5,\"industries\":[" +
                industries + "]}";
        }

        private static string WorldGenDocument(params string[] sizes)
        {
            return "{\"sizes\":[" + string.Join(",", sizes) + "]}";
        }

        [Test]
        public void Load_WorldGenMissingSize_FailsExplicitly()
        {
            string json = WorldGenDocument(
                WorldGenSize("small", _forestEntry),
                WorldGenSize("medium", _forestEntry));

            AssertBadWorldGen(json, "Large");
        }

        [Test]
        public void Load_WorldGenDuplicateSize_FailsExplicitly()
        {
            string json = WorldGenDocument(
                WorldGenSize("small", _forestEntry),
                WorldGenSize("small", _forestEntry),
                WorldGenSize("medium", _forestEntry),
                WorldGenSize("large", _forestEntry));

            AssertBadWorldGen(json, "more than once");
        }

        [Test]
        public void Load_WorldGenUnknownIndustry_FailsExplicitly()
        {
            const string unknown = "{\"id\":\"mystery\",\"count\":1,\"maxProducerDistance\":0}";
            string json = WorldGenDocument(
                WorldGenSize("small", _forestEntry),
                WorldGenSize("medium", _forestEntry),
                WorldGenSize("large", unknown));

            AssertBadWorldGen(json, "unknown industry ID 'mystery'");
        }

        [TestCase("{\"id\":\"forest\",\"count\":0,\"maxProducerDistance\":0}", "count")]
        [TestCase("{\"id\":\"forest\",\"count\":1,\"maxProducerDistance\":-1}", "producer distance")]
        public void Load_WorldGenBadEntry_FailsExplicitly(string entry, string expectedMessage)
        {
            string json = WorldGenDocument(
                WorldGenSize("small", entry),
                WorldGenSize("medium", _forestEntry),
                WorldGenSize("large", _forestEntry));

            AssertBadWorldGen(json, expectedMessage);
        }

        [Test]
        public void Load_WorldGenUnsupportedSize_FailsExplicitly()
        {
            string json = WorldGenDocument(WorldGenSize("huge", _forestEntry));

            AssertBadWorldGen(json, "unsupported size 'huge'");
        }

        [Test]
        public void Load_EconomyWithoutPayOrGrowthRules_Throws()
        {
            AssertBadEconomy("{\"startingCash\":100}", "Economy settings are invalid");
        }

        [Test]
        public void Load_EconomyWithInvalidPayRule_Throws()
        {
            AssertBadEconomy(
                "{\"startingCash\":100,\"deliveryPay\":{\"referenceDistance\":30,\"minimumDistance\":0},\"industryGrowth\":{\"servedPercent\":60,\"monthsToGrow\":12,\"maxLevel\":6,\"levelStepPercent\":25}}",
                "Economy settings are invalid");
        }

        private void AssertBadEconomy(string economyJson, string expectedMessage)
        {
            _cargoAsset = new TextAsset(
                "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"cargo\",\"pricePerUnit\":1,\"manufacturingLevel\":1}]}");
            _industryAsset = new TextAsset("{\"industries\":[{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":1}]}]}");
            var economyAsset = new TextAsset(economyJson);

            ContentLoadException exception = Assert.Throws<ContentLoadException>(
                () => ContentLoader.Load(_cargoAsset, _industryAsset, null, economyAsset))!;

            StringAssert.Contains(expectedMessage, exception.Message);
            Object.DestroyImmediate(economyAsset);
        }

        private void AssertBadWorldGen(string worldGenJson, string expectedMessage)
        {
            _cargoAsset = new TextAsset(
                "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"cargo\",\"pricePerUnit\":1,\"manufacturingLevel\":1}]}");
            _industryAsset = new TextAsset("{\"industries\":[{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":1}]}]}");
            var worldGenAsset = new TextAsset(worldGenJson);

            ContentLoadException exception = Assert.Throws<ContentLoadException>(
                () => ContentLoader.Load(_cargoAsset, _industryAsset, null, null, null, null, null, worldGenAsset))!;

            StringAssert.Contains(expectedMessage, exception.Message);
            Object.DestroyImmediate(worldGenAsset);
        }

        private ContentCatalog LoadCheckedInContent()
        {
            string contentPath = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            _cargoAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "cargo.json")));
            _industryAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "industries.json")));
            var townsAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "towns.json")));
            var economyAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "economy.json")));
            var vehiclesAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "vehicles.json")));
            var achievementsAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "achievements.json")));
            var contractsAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "contracts.json")));
            var worldGenAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "worldgen.json")));
            return ContentLoader.Load(
                _cargoAsset,
                _industryAsset,
                townsAsset,
                economyAsset,
                vehiclesAsset,
                achievementsAsset,
                contractsAsset,
                worldGenAsset);
        }
    }
}
