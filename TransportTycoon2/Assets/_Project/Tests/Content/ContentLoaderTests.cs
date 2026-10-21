#nullable enable
using System.IO;
using NUnit.Framework;
using TransportTycoon.Core.Content;
using UnityEngine;

namespace TransportTycoon.Content.Tests
{
    public sealed class ContentLoaderTests
    {
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

            Assert.That(catalog.Cargo, Has.Count.EqualTo(5));
            Assert.That(catalog.Industries, Has.Count.EqualTo(4));
            Assert.That(catalog.TryGetCargo("passengers", out CargoDefinition? passengers), Is.True);
            Assert.That(passengers!.NameKey, Is.EqualTo("cargo.passengers"));
            Assert.That(passengers.Class, Is.EqualTo(CargoClass.Passengers));
            Assert.That(passengers.PricePerUnit, Is.EqualTo(10));
            Assert.That(catalog.TryGetCargo("wood", out CargoDefinition? wood), Is.True);
            Assert.That(wood!.NameKey, Is.EqualTo("cargo.wood"));
            Assert.That(wood.Class, Is.EqualTo(CargoClass.Cargo));
            Assert.That(wood.PricePerUnit, Is.EqualTo(8));
            Assert.That(catalog.TryGetCargo("Wood", out _), Is.False);
            Assert.That(catalog.TryGetIndustry("forest", out IndustryDefinition? forest), Is.True);
            Assert.That(forest!.Level, Is.EqualTo(1));
            Assert.That(forest.Inputs, Is.Empty);
            Assert.That(forest.Outputs[0].CargoId, Is.EqualTo("wood"));
            Assert.That(forest.Outputs[0].Amount, Is.EqualTo(4));
            Assert.That(catalog.TryGetIndustry("mine", out IndustryDefinition? mine), Is.True);
            Assert.That(mine!.Outputs[0].CargoId, Is.EqualTo("metal"));
            Assert.That(mine.Outputs[0].Amount, Is.EqualTo(2));
            Assert.That(catalog.TryGetIndustry("sawmill", out IndustryDefinition? sawmill), Is.True);
            Assert.That(sawmill!.Inputs[0].CargoId, Is.EqualTo("wood"));
            Assert.That(sawmill.Inputs[0].Amount, Is.EqualTo(2));
            Assert.That(sawmill.Outputs[0].CargoId, Is.EqualTo("planks"));
            Assert.That(sawmill.Outputs[0].Amount, Is.EqualTo(1));
            Assert.That(catalog.TryGetIndustry("furniture_factory", out IndustryDefinition? factory), Is.True);
            Assert.That(factory!.NameKey, Is.EqualTo("industry.furniture_factory"));
            Assert.That(factory.Level, Is.EqualTo(3));
            Assert.That(factory!.Inputs, Has.Count.EqualTo(2));
            Assert.That(factory.Inputs[0].CargoId, Is.EqualTo("planks"));
            Assert.That(factory.Inputs[0].Amount, Is.EqualTo(2));
            Assert.That(factory.Inputs[1].CargoId, Is.EqualTo("metal"));
            Assert.That(factory.Outputs[0].CargoId, Is.EqualTo("furniture"));
            Assert.That(factory.Outputs[0].Amount, Is.EqualTo(1));
            Assert.That(catalog.TryGetIndustry("Forest", out _), Is.False);
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

        private ContentCatalog LoadCheckedInContent()
        {
            string contentPath = Path.Combine(Application.dataPath, "_Project", "Content", "Data");
            _cargoAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "cargo.json")));
            _industryAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "industries.json")));
            return ContentLoader.Load(_cargoAsset, _industryAsset);
        }
    }
}
