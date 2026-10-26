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
            Assert.That(wood.PricePerUnit, Is.EqualTo(8));
            Assert.That(wood.ManufacturingLevel, Is.EqualTo(1));
            Assert.That(catalog.TownNeeds.MinStartingPopulation, Is.EqualTo(100));
            Assert.That(catalog.TownNeeds.MaxStartingPopulation, Is.EqualTo(500));
            Assert.That(catalog.TownNeeds.DemandPerNeedPerMonth, Is.EqualTo(10));
            Assert.That(catalog.TownNeeds.PopulationPerSatisfiedNeed, Is.EqualTo(20));
            Assert.That(catalog.TownNeeds.Tiers, Has.Count.EqualTo(11));
            Assert.That(catalog.Economy.StartingCash, Is.EqualTo(100000L));
            Assert.That(catalog.StartingRoadVehicle.Id, Is.EqualTo("truck_mk1"));
            Assert.That(catalog.StartingRoadVehicle.Purchase, Is.EqualTo(8000L));
            Assert.That(catalog.StartingRailLocomotive.Id, Is.EqualTo("loco_mk1"));
            Assert.That(catalog.StartingRailLocomotive.Purchase, Is.EqualTo(20000L));
            Assert.That(catalog.RailWagon.Id, Is.EqualTo("rail_wagon"));
            Assert.That(catalog.RailWagon.Purchase, Is.EqualTo(4000L));
            Assert.That(catalog.Vehicles, Has.Count.EqualTo(10));
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
            var townsAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "towns.json")));
            var economyAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "economy.json")));
            var vehiclesAsset = new TextAsset(File.ReadAllText(Path.Combine(contentPath, "vehicles.json")));
            return ContentLoader.Load(_cargoAsset, _industryAsset, townsAsset, economyAsset, vehiclesAsset);
        }
    }
}
