#nullable enable
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.Balance;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Balance
{
    [TestFixture]
    public sealed class VehicleBenchTests
    {
        private const int _firstDistance = 8;
        private const int _lastDistance = 14;
        private const int _years = 5;
        private const int _pricePerUnit = 5;
        private const double _ticksPerYear = 36000d;
        private static readonly DeliveryPaySettings _pay = new DeliveryPaySettings(700, 300, 4);

        [Test]
        public void Run_StartingTruck_MatchesTripTimeAndRealPay()
        {
            ContentCatalog catalog = CreateCatalog();
            VehicleDefinition truck = catalog.StartingRoadVehicle;

            IReadOnlyList<VehicleBenchRow> rows = VehicleBench.Run(catalog, new[] { truck }, _firstDistance, _lastDistance, _years);

            Assert.That(rows, Has.Count.EqualTo(_lastDistance - _firstDistance + 1));
            foreach (VehicleBenchRow row in rows)
            {
                double expectedTicks = (2000d * row.Distance / truck.SpeedThousandths) + (2d * truck.CargoCapacity);
                double payPerTrip = _pricePerUnit * truck.CargoCapacity * (0.07d + (0.03d * row.Distance));
                double expectedIncome = payPerTrip * _ticksPerYear / row.TicksPerTrip;
                Assert.That(row.TicksPerTrip, Is.EqualTo(expectedTicks).Within(1d), row.Distance.ToString(CultureInfo.InvariantCulture));
                Assert.That(row.IncomePerYear, Is.EqualTo(expectedIncome).Within(1d), row.Distance.ToString(CultureInfo.InvariantCulture));
            }
        }

        [Test]
        public void Run_MultipleDistances_ReturnsThemInOrder()
        {
            ContentCatalog catalog = CreateCatalog();

            IReadOnlyList<VehicleBenchRow> rows = VehicleBench.Run(catalog, new[] { catalog.StartingRoadVehicle }, _firstDistance, _lastDistance, _years);

            for (int index = 1; index < rows.Count; index++)
            {
                Assert.That(rows[index].Distance, Is.EqualTo(rows[index - 1].Distance + 1));
            }
        }

        [Test]
        public void Models_LeaveOutVehiclesWithoutCapacity()
        {
            ContentCatalog catalog = CreateCatalog();

            IReadOnlyList<VehicleDefinition> models = VehicleBench.Models(catalog);

            Assert.That(models, Is.Not.Empty);
            foreach (VehicleDefinition model in models)
            {
                Assert.That(model.Mode == VehicleMode.Road ? model.CargoCapacity : model.MaxWagons * model.CapacityPerWagon, Is.GreaterThan(0), model.Id);
            }
        }

        [Test]
        public void WriteReport_CountsTheStepsThatDoNotRise()
        {
            var rows = new[]
            {
                new VehicleBenchRow("truck", 4, 52d, 10, 100d),
                new VehicleBenchRow("truck", 5, 60d, 10, 110d),
                new VehicleBenchRow("truck", 6, 68d, 10, 105d),
            };

            string report = VehicleBench.WriteReport(rows, 4, 6, 5);

            StringAssert.Contains("| truck | 4 | 100.00 | 6 | 105.00 | 5.00 | 2 | 1 |", report);
        }

        private static ContentCatalog CreateCatalog()
        {
            var cargo = new[]
            {
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, _pricePerUnit, 1),
                new CargoDefinition("planks", "cargo.planks", CargoClass.Cargo, 10, 2),
            };
            var industries = new[]
            {
                new IndustryDefinition("forest", "industry.forest", 1, System.Array.Empty<CargoAmountDefinition>(), new[] { new CargoAmountDefinition("wood", 4) }),
                new IndustryDefinition("sawmill", "industry.sawmill", 2, new[] { new CargoAmountDefinition("wood", 2) }, new[] { new CargoAmountDefinition("planks", 1) }),
            };
            var townNeeds = new TownNeedsSettings(
                100,
                500,
                10000,
                new[] { new TownNeedTier(0, 1, 0, 0) },
                demandPerNeedPerMonth: new TownNeedLevelAmounts(10, 10, 10),
                allNeedsMetGrowthMultiplier: 1);
            return new ContentCatalog(cargo, industries, townNeeds, new EconomySettings(100000L, _pay));
        }
    }
}
