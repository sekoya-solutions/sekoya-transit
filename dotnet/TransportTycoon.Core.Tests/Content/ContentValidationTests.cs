#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Json.Schema;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.Content
{
    [TestFixture]
    public sealed class ContentValidationTests
    {
        private const string _validCargo =
            "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"cargo\",\"pricePerUnit\":8,\"manufacturingLevel\":1}," +
            "{\"id\":\"planks\",\"nameKey\":\"cargo.planks\",\"class\":\"cargo\",\"pricePerUnit\":14,\"manufacturingLevel\":2}]}";

        private const string _validIndustries =
            "{\"industries\":[" +
            "{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":4}]}," +
            "{\"id\":\"sawmill\",\"nameKey\":\"industry.sawmill\",\"level\":2,\"inputs\":[{\"cargo\":\"wood\",\"amount\":2}],\"outputs\":[{\"cargo\":\"planks\",\"amount\":1}]}]}";

        private JsonSchema _cargoSchema = null!;
        private JsonSchema _industriesSchema = null!;
        private JsonSchema _townsSchema = null!;
        private JsonSchema _economySchema = null!;
        private JsonSchema _vehiclesSchema = null!;
        private JsonSchema _achievementsSchema = null!;
        private JsonSchema _contractsSchema = null!;
        private JsonSchema _worldGenSchema = null!;

        [OneTimeSetUp]
        public void LoadSchemas()
        {
            _cargoSchema = ContentValidator.LoadSchema("cargo.schema.json");
            _industriesSchema = ContentValidator.LoadSchema("industries.schema.json");
            _townsSchema = ContentValidator.LoadSchema("towns.schema.json");
            _economySchema = ContentValidator.LoadSchema("economy.schema.json");
            _vehiclesSchema = ContentValidator.LoadSchema("vehicles.schema.json");
            _achievementsSchema = ContentValidator.LoadSchema("achievements.schema.json");
            _contractsSchema = ContentValidator.LoadSchema("contracts.schema.json");
            _worldGenSchema = ContentValidator.LoadSchema("worldgen.schema.json");
        }

        [Test]
        public void CargoData_ConformsToSchema()
        {
            Assert.That(ContentValidator.ValidateSchema(_cargoSchema, ContentValidator.ReadData("cargo.json")), Is.Empty);
        }

        [Test]
        public void IndustriesData_ConformsToSchema()
        {
            Assert.That(ContentValidator.ValidateSchema(_industriesSchema, ContentValidator.ReadData("industries.json")), Is.Empty);
        }

        [Test]
        public void TownsData_ConformsToSchema()
        {
            Assert.That(ContentValidator.ValidateSchema(_townsSchema, ContentValidator.ReadData("towns.json")), Is.Empty);
        }

        [Test]
        public void EconomyData_ConformsToSchema()
        {
            Assert.That(ContentValidator.ValidateSchema(_economySchema, ContentValidator.ReadData("economy.json")), Is.Empty);
        }

        [Test]
        public void VehiclesData_ConformsToSchema()
        {
            Assert.That(ContentValidator.ValidateSchema(_vehiclesSchema, ContentValidator.ReadData("vehicles.json")), Is.Empty);
        }

        [Test]
        public void AchievementsData_ConformsToSchema()
        {
            Assert.That(
                ContentValidator.ValidateSchema(_achievementsSchema, ContentValidator.ReadData("achievements.json")),
                Is.Empty);
        }

        [Test]
        public void ContractsData_ConformsToSchema()
        {
            Assert.That(
                ContentValidator.ValidateSchema(_contractsSchema, ContentValidator.ReadData("contracts.json")),
                Is.Empty);
        }

        [TestCase("{\"maxOpenContracts\":-1,\"minAmount\":50,\"maxAmount\":200,\"durationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}")]
        [TestCase("{\"maxOpenContracts\":3,\"minAmount\":0,\"maxAmount\":200,\"durationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}")]
        [TestCase("{\"maxOpenContracts\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonths\":0,\"rewardMultiplier\":1.0,\"townPopulationBonus\":100}")]
        [TestCase("{\"maxOpenContracts\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonths\":12,\"rewardMultiplier\":-1,\"townPopulationBonus\":100}")]
        [TestCase("{\"maxOpenContracts\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonths\":12,\"rewardMultiplier\":1.0,\"townPopulationBonus\":-1}")]
        [TestCase("{\"maxOpenContracts\":3,\"minAmount\":50,\"maxAmount\":200,\"durationMonths\":12,\"rewardMultiplier\":1.0}")]
        public void ContractsSchema_RejectsBadValues(string json)
        {
            Assert.That(ContentValidator.ValidateSchema(_contractsSchema, json), Is.Not.Empty);
        }

        [TestCase("{\"achievements\":[{\"id\":\"a\",\"nameKey\":\"k\",\"kind\":\"companyCash\",\"goal\":0}]}")]
        [TestCase("{\"achievements\":[{\"id\":\"a\",\"nameKey\":\"k\",\"kind\":\"unknownKind\",\"goal\":5}]}")]
        [TestCase("{\"achievements\":[{\"id\":\"a\",\"nameKey\":\"k\",\"kind\":\"cargoDelivered\",\"goal\":5}]}")]
        [TestCase("{\"achievements\":[{\"id\":\"a\",\"nameKey\":\"k\",\"kind\":\"vehicleCount\",\"goal\":5,\"cargo\":\"bread\"}]}")]
        public void AchievementsSchema_RejectsBadEntries(string json)
        {
            Assert.That(ContentValidator.ValidateSchema(_achievementsSchema, json), Is.Not.Empty);
        }

        [Test]
        public void AchievementsIntegrity_RejectsDuplicateIdsAndUnknownCargo()
        {
            const string achievements =
                "{\"achievements\":[" +
                "{\"id\":\"a\",\"nameKey\":\"k\",\"kind\":\"vehicleCount\",\"goal\":1}," +
                "{\"id\":\"a\",\"nameKey\":\"k\",\"kind\":\"vehicleCount\",\"goal\":2}," +
                "{\"id\":\"b\",\"nameKey\":\"k\",\"kind\":\"cargoDelivered\",\"goal\":2,\"cargo\":\"nope\",\"destination\":\"any\"}]}";

            var errors = ContentValidator.ValidateIntegrity(
                ContentValidator.ReadData("cargo.json"),
                ContentValidator.ReadData("industries.json"),
                null,
                achievements);

            Assert.That(errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void WorldGenData_ConformsToSchema()
        {
            Assert.That(
                ContentValidator.ValidateSchema(_worldGenSchema, ContentValidator.ReadData("worldgen.json")),
                Is.Empty);
        }

        [TestCase("{\"sizes\":[]}")]
        [TestCase("{\"sizes\":[{\"size\":\"tiny\",\"townCount\":1,\"townSpacing\":8,\"industrySpacing\":4,\"industries\":[{\"id\":\"a\",\"count\":1,\"maxProducerDistance\":0}]},{\"size\":\"medium\",\"townCount\":1,\"townSpacing\":8,\"industrySpacing\":4,\"industries\":[{\"id\":\"a\",\"count\":1,\"maxProducerDistance\":0}]},{\"size\":\"large\",\"townCount\":1,\"townSpacing\":8,\"industrySpacing\":4,\"industries\":[{\"id\":\"a\",\"count\":1,\"maxProducerDistance\":0}]}]}")]
        [TestCase("{\"sizes\":[{\"size\":\"small\",\"townCount\":1,\"townSpacing\":8,\"industrySpacing\":2,\"industries\":[{\"id\":\"a\",\"count\":1,\"maxProducerDistance\":0}]},{\"size\":\"medium\",\"townCount\":1,\"townSpacing\":8,\"industrySpacing\":4,\"industries\":[{\"id\":\"a\",\"count\":1,\"maxProducerDistance\":0}]},{\"size\":\"large\",\"townCount\":1,\"townSpacing\":8,\"industrySpacing\":4,\"industries\":[{\"id\":\"a\",\"count\":1,\"maxProducerDistance\":0}]}]}")]
        [TestCase("{\"sizes\":[{\"size\":\"small\",\"townCount\":1,\"townSpacing\":8,\"industrySpacing\":4,\"industries\":[{\"id\":\"a\",\"count\":0,\"maxProducerDistance\":0}]},{\"size\":\"medium\",\"townCount\":1,\"townSpacing\":8,\"industrySpacing\":4,\"industries\":[{\"id\":\"a\",\"count\":1,\"maxProducerDistance\":0}]},{\"size\":\"large\",\"townCount\":1,\"townSpacing\":8,\"industrySpacing\":4,\"industries\":[{\"id\":\"a\",\"count\":1,\"maxProducerDistance\":0}]}]}")]
        public void WorldGenSchema_RejectsBadValues(string json)
        {
            Assert.That(ContentValidator.ValidateSchema(_worldGenSchema, json), Is.Not.Empty);
        }

        [Test]
        public void WorldGenData_MatchesPlaceholderAndResolvesAgainstShippedIndustries()
        {
            JsonArray sizes = JsonNode.Parse(ContentValidator.ReadData("worldgen.json"))!["sizes"]!.AsArray();
            var definitions = new List<WorldGenerationSizeDefinition>();
            foreach (JsonNode? size in sizes)
            {
                var industries = new List<WorldGenerationIndustryEntry>();
                foreach (JsonNode? industry in size!["industries"]!.AsArray())
                {
                    industries.Add(
                        new WorldGenerationIndustryEntry(
                            industry!["id"]!.GetValue<string>(),
                            industry["count"]!.GetValue<int>(),
                            industry["maxProducerDistance"]!.GetValue<int>()));
                }

                definitions.Add(
                    new WorldGenerationSizeDefinition(
                        Enum.Parse<MapSize>(size["size"]!.GetValue<string>(), true),
                        size["townCount"]!.GetValue<int>(),
                        size["townSpacing"]!.GetValue<int>(),
                        size["industrySpacing"]!.GetValue<int>(),
                        industries));
            }

            var shipped = new WorldGenerationData(definitions);
            WorldGenerationData placeholder = WorldGenerationData.CreatePlaceholder();
            var industryDefinitions = new List<IndustryDefinition>();
            foreach (JsonNode? industry in JsonNode.Parse(ContentValidator.ReadData("industries.json"))!["industries"]!.AsArray())
            {
                industryDefinitions.Add(
                    new IndustryDefinition(
                        industry!["id"]!.GetValue<string>(),
                        industry["nameKey"]!.GetValue<string>(),
                        industry["level"]!.GetValue<int>(),
                        ToAmounts(industry["inputs"]!.AsArray()),
                        ToAmounts(industry["outputs"]!.AsArray())));
            }

            foreach (MapSize mapSize in Enum.GetValues<MapSize>())
            {
                WorldGenerationSizeDefinition expected = placeholder.Get(mapSize);
                WorldGenerationSizeDefinition actual = shipped.Get(mapSize);
                Assert.That(actual.TownCount, Is.EqualTo(expected.TownCount), mapSize.ToString());
                Assert.That(actual.TownSpacing, Is.EqualTo(expected.TownSpacing), mapSize.ToString());
                Assert.That(actual.IndustrySpacing, Is.EqualTo(expected.IndustrySpacing), mapSize.ToString());
                Assert.That(actual.Industries.Count, Is.EqualTo(expected.Industries.Count), mapSize.ToString());
                for (int index = 0; index < expected.Industries.Count; index++)
                {
                    Assert.That(actual.Industries[index].IndustryId, Is.EqualTo(expected.Industries[index].IndustryId));
                    Assert.That(actual.Industries[index].Count, Is.EqualTo(expected.Industries[index].Count));
                    Assert.That(
                        actual.Industries[index].MaximumProducerDistance,
                        Is.EqualTo(expected.Industries[index].MaximumProducerDistance));
                }

                Assert.DoesNotThrow(() => shipped.CreateSettings(mapSize, industryDefinitions), mapSize.ToString());
            }
        }

        private static List<CargoAmountDefinition> ToAmounts(JsonArray amounts)
        {
            var result = new List<CargoAmountDefinition>();
            foreach (JsonNode? amount in amounts)
            {
                result.Add(new CargoAmountDefinition(amount!["cargo"]!.GetValue<string>(), amount["amount"]!.GetValue<int>()));
            }

            return result;
        }

        [Test]
        public void ContentData_HasReferentialIntegrity()
        {
            var errors = ContentValidator.ValidateIntegrity(
                ContentValidator.ReadData("cargo.json"),
                ContentValidator.ReadData("industries.json"),
                ContentValidator.ReadData("vehicles.json"),
                ContentValidator.ReadData("achievements.json"));

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void ContentData_ContainsFiveThreeLevelProductionChains()
        {
            JsonArray industries = JsonNode.Parse(ContentValidator.ReadData("industries.json"))!["industries"]!.AsArray();
            JsonArray cargo = JsonNode.Parse(ContentValidator.ReadData("cargo.json"))!["cargo"]!.AsArray();
            Assert.That(industries, Has.Count.EqualTo(15));
            Assert.That(cargo, Has.Count.EqualTo(17));
            Assert.That(industries.Count(entry => entry!["level"]!.GetValue<int>() == 1), Is.EqualTo(5));
            Assert.That(industries.Count(entry => entry!["level"]!.GetValue<int>() == 2), Is.EqualTo(5));
            Assert.That(industries.Count(entry => entry!["level"]!.GetValue<int>() == 3), Is.EqualTo(5));

            var industriesById = industries.ToDictionary(
                entry => entry!["id"]!.GetValue<string>(),
                entry => entry!);
            var cargoById = cargo.ToDictionary(
                entry => entry!["id"]!.GetValue<string>(),
                entry => entry!);

            string[][] chains =
            {
                new[] { "forest", "sawmill", "furniture_factory", "wood", "planks", "furniture" },
                new[] { "wheat_farm", "flour_mill", "bakery", "wheat", "flour", "bread" },
                new[] { "iron_mine", "iron_foundry", "machine_shop", "iron_ore", "iron", "machinery" },
                new[] { "livestock_ranch", "butcher", "cannery", "livestock", "meat", "canned_food" },
                new[] { "sheep_farm", "textile_mill", "clothing_workshop", "wool", "fabric", "clothing" }
            };

            foreach (string[] chain in chains)
            {
                for (int level = 0; level < 3; level++)
                {
                    JsonNode industry = industriesById[chain[level]]!;
                    Assert.That(industry["level"]!.GetValue<int>(), Is.EqualTo(level + 1), chain[level]);
                    JsonArray outputs = industry["outputs"]!.AsArray();
                    Assert.That(outputs, Has.Count.EqualTo(1), chain[level]);
                    Assert.That(outputs[0]!["cargo"]!.GetValue<string>(), Is.EqualTo(chain[level + 3]), chain[level]);
                    Assert.That(outputs[0]!["amount"]!.GetValue<int>(), Is.GreaterThan(0), chain[level]);

                    JsonArray inputs = industry["inputs"]!.AsArray();
                    if (level == 0)
                    {
                        Assert.That(inputs, Is.Empty, chain[level]);
                    }
                    else
                    {
                        Assert.That(inputs, Has.Count.EqualTo(1), chain[level]);
                        Assert.That(inputs[0]!["cargo"]!.GetValue<string>(), Is.EqualTo(chain[level + 2]), chain[level]);
                        Assert.That(inputs[0]!["amount"]!.GetValue<int>(), Is.GreaterThan(0), chain[level]);
                    }
                }

                int rawPrice = cargoById[chain[3]]!["pricePerUnit"]!.GetValue<int>();
                int finalPrice = cargoById[chain[5]]!["pricePerUnit"]!.GetValue<int>();
                Assert.That(rawPrice, Is.GreaterThan(0), chain[3]);
                Assert.That(finalPrice, Is.GreaterThan(rawPrice), chain[5]);
            }
        }

        [Test]
        public void ValidateIntegrity_MinimalValidContent_ReportsNoErrors()
        {
            Assert.That(ContentValidator.ValidateIntegrity(_validCargo, _validIndustries), Is.Empty);
        }

        [Test]
        public void ValidateSchema_MissingRequiredProperty_ReportsError()
        {
            var json = _validCargo.Replace("\"pricePerUnit\":8", "\"unused\":8");

            Assert.That(ContentValidator.ValidateSchema(_cargoSchema, json), Is.Not.Empty);
        }

        [Test]
        public void ValidateSchema_UnknownCargoClass_ReportsError()
        {
            var json = _validCargo.Replace("\"class\":\"cargo\"", "\"class\":\"liquid\"");

            Assert.That(ContentValidator.ValidateSchema(_cargoSchema, json), Is.Not.Empty);
        }

        [Test]
        public void ValidateSchema_NonPositiveAmount_ReportsError()
        {
            var json = _validIndustries.Replace("\"amount\":4", "\"amount\":0");

            Assert.That(ContentValidator.ValidateSchema(_industriesSchema, json), Is.Not.Empty);
        }

        [Test]
        public void ValidateSchema_DisplayStringInsteadOfKey_ReportsError()
        {
            var json = _validCargo.Replace("cargo.wood", "Wood logs");

            Assert.That(ContentValidator.ValidateSchema(_cargoSchema, json), Is.Not.Empty);
        }

        [Test]
        public void ValidateIntegrity_UnknownCargoReference_ReportsError()
        {
            var json = _validIndustries.Replace("\"cargo\":\"wood\",\"amount\":2", "\"cargo\":\"ghost\",\"amount\":2");

            Assert.That(ContentValidator.ValidateIntegrity(_validCargo, json), Has.Some.Contain("unknown cargo 'ghost'"));
        }

        [Test]
        public void ValidateIntegrity_DuplicateCargoId_ReportsError()
        {
            var json = _validCargo.Replace("\"id\":\"planks\"", "\"id\":\"wood\"");

            Assert.That(ContentValidator.ValidateIntegrity(json, _validIndustries), Has.Some.Contain("Duplicate cargo id 'wood'"));
        }

        [Test]
        public void ValidateIntegrity_DuplicateIndustryId_ReportsError()
        {
            var json = _validIndustries.Replace("\"id\":\"sawmill\"", "\"id\":\"forest\"");

            Assert.That(ContentValidator.ValidateIntegrity(_validCargo, json), Has.Some.Contain("Duplicate industry id 'forest'"));
        }

        [Test]
        public void ValidateIntegrity_PrimaryIndustryWithInputs_ReportsError()
        {
            var json = _validIndustries.Replace("\"level\":2", "\"level\":1");

            Assert.That(ContentValidator.ValidateIntegrity(_validCargo, json), Has.Some.Contain("level 1 (primary) but declares inputs"));
        }

        [Test]
        public void ValidateIntegrity_HigherLevelIndustryWithoutInputs_ReportsError()
        {
            var json = _validIndustries.Replace(
                "\"inputs\":[{\"cargo\":\"wood\",\"amount\":2}]",
                "\"inputs\":[]");

            Assert.That(ContentValidator.ValidateIntegrity(_validCargo, json), Has.Some.Contain("level 2 but declares no inputs"));
        }

        [Test]
        public void ValidateIntegrity_InputWithoutLowerLevelProducer_ReportsError()
        {
            var json = _validIndustries.Replace("\"level\":1", "\"level\":2");

            Assert.That(
                ContentValidator.ValidateIntegrity(_validCargo, json),
                Has.Some.Contain("Industry 'sawmill' consumes cargo 'wood' at level 2, but it is not produced by any industry at a lower level."));
        }

        [Test]
        public void ValidateIntegrity_UnproducedCargo_ReportsError()
        {
            var json = _validCargo.Replace("\"id\":\"planks\"", "\"id\":\"orphan\"");

            Assert.That(ContentValidator.ValidateIntegrity(json, _validIndustries), Has.Some.Contain("'orphan' is not produced by any industry"));
        }

        [Test]
        public void ValidateIntegrity_CyclicChain_ReportsError()
        {
            const string industries =
                "{\"industries\":[" +
                "{\"id\":\"a\",\"nameKey\":\"industry.a\",\"level\":2,\"inputs\":[{\"cargo\":\"wood\",\"amount\":1}],\"outputs\":[{\"cargo\":\"planks\",\"amount\":1}]}," +
                "{\"id\":\"b\",\"nameKey\":\"industry.b\",\"level\":2,\"inputs\":[{\"cargo\":\"planks\",\"amount\":1}],\"outputs\":[{\"cargo\":\"wood\",\"amount\":1}]}]}";

            Assert.That(ContentValidator.ValidateIntegrity(_validCargo, industries), Has.Some.Contain("Production chain cycle: planks -> wood -> planks"));
        }
    }
}
