#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.Core.Content;

namespace TransportTycoon.Core.Tests.Content
{
    public sealed class ContentDefinitionTests
    {
        [Test]
        public void ContentCatalog_CopiesDefinitionsAndSupportsOrdinalLookup()
        {
            var cargo = new List<CargoDefinition>
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1)
            };
            var industry = new List<IndustryDefinition>
            {
                new IndustryDefinition(
                    "forest",
                    "industry.forest",
                    1,
                    new List<CargoAmountDefinition>(),
                    new List<CargoAmountDefinition> { new CargoAmountDefinition("wood", 4) })
            };

            var catalog = new ContentCatalog(cargo, industry);
            cargo.Clear();
            industry.Clear();

            Assert.That(catalog.Cargo, Has.Count.EqualTo(1));
            Assert.That(catalog.Industries, Has.Count.EqualTo(1));
            Assert.That(catalog.TryGetCargo("wood", out CargoDefinition? foundCargo), Is.True);
            Assert.That(foundCargo!.PricePerUnit, Is.EqualTo(8));
            Assert.That(catalog.TryGetCargo("Wood", out _), Is.False);
            Assert.That(catalog.TryGetIndustry("forest", out IndustryDefinition? foundIndustry), Is.True);
            Assert.That(foundIndustry!.Outputs[0].Amount, Is.EqualTo(4));

            var readOnlyCargo = (IList<CargoDefinition>)catalog.Cargo;
            Assert.Throws<System.NotSupportedException>(
                () => readOnlyCargo[0] = new CargoDefinition("metal", "cargo.metal", CargoClass.Cargo, 16, 1));
        }

        [Test]
        public void IndustryDefinition_CopiesRecipeCollectionsAndExposesReadOnlyViews()
        {
            var sourceInputs = new List<CargoAmountDefinition>
            {
                new CargoAmountDefinition("wood", 2)
            };
            var outputs = new List<CargoAmountDefinition>
            {
                new CargoAmountDefinition("planks", 1)
            };

            var definition = new IndustryDefinition("sawmill", "industry.sawmill", 2, sourceInputs, outputs);
            sourceInputs.Clear();
            outputs.Clear();

            Assert.That(definition.Inputs, Has.Count.EqualTo(1));
            Assert.That(definition.Outputs, Has.Count.EqualTo(1));
            Assert.That(definition.Inputs[0].CargoId, Is.EqualTo("wood"));
            Assert.That(definition.Outputs[0].CargoId, Is.EqualTo("planks"));
            var readOnlyInputs = (IList<CargoAmountDefinition>)definition.Inputs;
            Assert.Throws<System.NotSupportedException>(
                () => readOnlyInputs[0] = new CargoAmountDefinition("metal", 1));
        }

        [Test]
        public void ContentCatalog_RejectsDuplicateDefinitionIds()
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1),
                new CargoDefinition("wood", "cargo.wood_duplicate", CargoClass.Cargo, 9, 1)
            };

            Assert.Throws<System.ArgumentException>(
                () => new ContentCatalog(cargo, System.Array.Empty<IndustryDefinition>()));
        }

        [Test]
        public void ContentCatalog_ExposesStartingVehiclesAndRailWagon()
        {
            var catalog = new ContentCatalog(
                System.Array.Empty<CargoDefinition>(),
                System.Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());

            Assert.That(catalog.StartingRoadVehicle.Id, Is.EqualTo("truck_mk1"));
            Assert.That(catalog.StartingRoadVehicle.Mode, Is.EqualTo(VehicleMode.Road));
            Assert.That(catalog.StartingRailLocomotive.Id, Is.EqualTo("loco_mk1"));
            Assert.That(catalog.StartingRailLocomotive.MaxWagons, Is.EqualTo(3));
            Assert.That(catalog.RailWagon.Mode, Is.EqualTo(VehicleMode.RailWagon));
            Assert.That(catalog.TryGetVehicle("truck_mk1", out VehicleDefinition? truck), Is.True);
            Assert.That(truck!.SpeedThousandths, Is.EqualTo(250));
            Assert.That(
                catalog.MonthlyUpkeepForRail("loco_mk1", 2),
                Is.EqualTo(400L + (100L * 2L)));
        }
    }
}
