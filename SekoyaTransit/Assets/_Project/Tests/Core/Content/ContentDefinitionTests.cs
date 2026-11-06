#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using SekoyaTransit.Core.Content;

namespace SekoyaTransit.Core.Tests.Content
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

        [Test]
        public void GetMonthlyUpkeep_Truck_IsRatePerUnitTimesCargoCapacity()
        {
            VehicleDefinition truck = CreateTruck(upkeepPerUnit: 9L, cargoCapacity: 14);

            Assert.That(truck.GetMonthlyUpkeep(0), Is.EqualTo(126L));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => truck.GetMonthlyUpkeep(1));
        }

        [TestCase(0, 200L)]
        [TestCase(1, 320L)]
        [TestCase(4, 680L)]
        [TestCase(6, 920L)]
        public void GetMonthlyUpkeep_Locomotive_IsFixedFeePlusRateTimesWagonCapacity(int wagons, long expected)
        {
            // Wagon counts above the maximum keep a defined upkeep, for a save written under a larger maximum.
            VehicleDefinition locomotive = CreateLocomotive(upkeepPerUnit: 10L, fixedUpkeep: 200L);

            Assert.That(locomotive.GetMonthlyUpkeep(wagons), Is.EqualTo(expected));
        }

        [Test]
        public void GetMonthlyUpkeep_NegativeWagonsOrRailWagonEntry_Throws()
        {
            VehicleDefinition locomotive = CreateLocomotive(upkeepPerUnit: 10L, fixedUpkeep: 200L);
            VehicleDefinition wagon = new VehicleDefinition(
                "rail_wagon", "vehicle.rail_wagon", VehicleMode.RailWagon, 4000L, 0L, 0L, 1900, 0L, true);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => locomotive.GetMonthlyUpkeep(-1));
            Assert.Throws<System.InvalidOperationException>(() => wagon.GetMonthlyUpkeep(0));
        }

        [Test]
        public void VehicleDefinition_RejectsNegativeOrMisplacedUpkeep()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CreateTruck(upkeepPerUnit: -1L, cargoCapacity: 10));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CreateLocomotive(upkeepPerUnit: 10L, fixedUpkeep: -1L));
            Assert.Throws<System.ArgumentException>(() => new VehicleDefinition(
                "truck", "vehicle.truck", VehicleMode.Road, 1L, 10L, 50L, 1900, 0L, true, cargoCapacity: 10, speedThousandths: 250));
            Assert.Throws<System.ArgumentException>(() => new VehicleDefinition(
                "rail_wagon", "vehicle.rail_wagon", VehicleMode.RailWagon, 4000L, 10L, 0L, 1900, 0L, true));
            Assert.Throws<System.ArgumentException>(() => new VehicleDefinition(
                "rail_wagon", "vehicle.rail_wagon", VehicleMode.RailWagon, 4000L, 0L, 100L, 1900, 0L, true));
        }

        private static VehicleDefinition CreateTruck(long upkeepPerUnit, int cargoCapacity)
        {
            return new VehicleDefinition(
                "truck", "vehicle.truck", VehicleMode.Road, 1L, upkeepPerUnit, 0L, 1900, 0L, true,
                cargoCapacity: cargoCapacity, speedThousandths: 250);
        }

        private static VehicleDefinition CreateLocomotive(long upkeepPerUnit, long fixedUpkeep)
        {
            return new VehicleDefinition(
                "loco", "vehicle.loco", VehicleMode.Rail, 1L, upkeepPerUnit, fixedUpkeep, 1900, 0L, true,
                maxWagons: 4, capacityPerWagon: 12, speedThousandths: 300);
        }
    }
}
