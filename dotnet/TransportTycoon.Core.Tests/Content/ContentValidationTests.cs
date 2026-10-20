#nullable enable
using Json.Schema;
using NUnit.Framework;

namespace TransportTycoon.Core.Tests.Content
{
    [TestFixture]
    public sealed class ContentValidationTests
    {
        private const string _validCargo =
            "{\"cargo\":[{\"id\":\"wood\",\"nameKey\":\"cargo.wood\",\"class\":\"cargo\",\"pricePerUnit\":8}," +
            "{\"id\":\"planks\",\"nameKey\":\"cargo.planks\",\"class\":\"cargo\",\"pricePerUnit\":14}]}";

        private const string _validIndustries =
            "{\"industries\":[" +
            "{\"id\":\"forest\",\"nameKey\":\"industry.forest\",\"level\":1,\"inputs\":[],\"outputs\":[{\"cargo\":\"wood\",\"amount\":4}]}," +
            "{\"id\":\"sawmill\",\"nameKey\":\"industry.sawmill\",\"level\":2,\"inputs\":[{\"cargo\":\"wood\",\"amount\":2}],\"outputs\":[{\"cargo\":\"planks\",\"amount\":1}]}]}";

        private JsonSchema _cargoSchema = null!;
        private JsonSchema _industriesSchema = null!;

        [OneTimeSetUp]
        public void LoadSchemas()
        {
            _cargoSchema = ContentValidator.LoadSchema("cargo.schema.json");
            _industriesSchema = ContentValidator.LoadSchema("industries.schema.json");
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
        public void ContentData_HasReferentialIntegrity()
        {
            var errors = ContentValidator.ValidateIntegrity(
                ContentValidator.ReadData("cargo.json"),
                ContentValidator.ReadData("industries.json"));

            Assert.That(errors, Is.Empty);
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
