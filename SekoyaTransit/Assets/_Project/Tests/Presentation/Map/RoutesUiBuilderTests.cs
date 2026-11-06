#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation.Commands;
using SekoyaTransit.Presentation.Map;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.Presentation.Tests.Map
{
    [TestFixture]
    public sealed class RoutesUiBuilderTests
    {
        [Test]
        public void CreatePurchaseOption_Train_PricesMatchBuyCommandForEveryWagonCount()
        {
            ContentCatalog catalog = CreateCatalog();
            VehicleDefinition locomotive = catalog.StartingRailLocomotive;

            VehiclePurchaseOption option = RoutesUiBuilder.CreatePurchaseOption(catalog, locomotive, long.MaxValue / 2L);

            Assert.That(option.IsTrain, Is.True);
            Assert.That(option.MinWagons, Is.EqualTo(1));
            Assert.That(option.MaxWagons, Is.EqualTo(locomotive.MaxWagons));
            Assert.That(option.MaxAffordableWagons, Is.EqualTo(locomotive.MaxWagons));
            for (int wagons = 1; wagons <= locomotive.MaxWagons; wagons++)
            {
                Assert.That(
                    option.GetPrice(wagons),
                    Is.EqualTo(BuyVehicleForRouteCommand.GetPrice(catalog, locomotive, wagons)),
                    $"{wagons} wagons");
                Assert.That(option.CanAfford(wagons), Is.True, $"{wagons} wagons");
            }
        }

        [Test]
        public void CreatePurchaseOption_TrainWithCashForTwoWagons_AffordsOnlyUpToTwo()
        {
            ContentCatalog catalog = CreateCatalog();
            VehicleDefinition locomotive = catalog.StartingRailLocomotive;
            Assume.That(locomotive.MaxWagons, Is.GreaterThan(2));
            long cash = BuyVehicleForRouteCommand.GetPrice(catalog, locomotive, 3) - 1L;

            VehiclePurchaseOption option = RoutesUiBuilder.CreatePurchaseOption(catalog, locomotive, cash);

            Assert.That(option.MaxAffordableWagons, Is.EqualTo(2));
            Assert.That(option.CanAfford(1), Is.True);
            Assert.That(option.CanAfford(2), Is.True);
            Assert.That(option.CanAfford(3), Is.False);
        }

        [Test]
        public void CreatePurchaseOption_TrainWithoutCashForOneWagon_AffordsNone()
        {
            ContentCatalog catalog = CreateCatalog();
            VehicleDefinition locomotive = catalog.StartingRailLocomotive;
            long cash = BuyVehicleForRouteCommand.GetPrice(catalog, locomotive, 1) - 1L;

            VehiclePurchaseOption option = RoutesUiBuilder.CreatePurchaseOption(catalog, locomotive, cash);

            Assert.That(option.MaxAffordableWagons, Is.EqualTo(0));
            Assert.That(option.CanAfford(1), Is.False);
        }

        [Test]
        public void CreatePurchaseOption_Road_HasNoWagonsAndTruckPrice()
        {
            ContentCatalog catalog = CreateCatalog();
            VehicleDefinition truck = catalog.StartingRoadVehicle;

            VehiclePurchaseOption affordable = RoutesUiBuilder.CreatePurchaseOption(catalog, truck, truck.Purchase);
            VehiclePurchaseOption tooExpensive = RoutesUiBuilder.CreatePurchaseOption(catalog, truck, truck.Purchase - 1L);

            Assert.That(affordable.IsTrain, Is.False);
            Assert.That(affordable.MinWagons, Is.EqualTo(0));
            Assert.That(affordable.MaxWagons, Is.EqualTo(0));
            Assert.That(affordable.GetPrice(0), Is.EqualTo(truck.Purchase));
            Assert.That(affordable.CanAfford(0), Is.True);
            Assert.That(tooExpensive.CanAfford(0), Is.False);
        }

        [Test]
        public void ClampWagons_TrainOption_KeepsCountBetweenOneAndMax()
        {
            VehiclePurchaseOption option = VehiclePurchaseOption.Train("loco", "loco.name", 1000L, 100L, 4, 4);

            Assert.That(option.ClampWagons(0), Is.EqualTo(1));
            Assert.That(option.ClampWagons(3), Is.EqualTo(3));
            Assert.That(option.ClampWagons(5), Is.EqualTo(4));
            Assert.That(() => option.GetPrice(0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => option.GetPrice(5), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(option.GetPrice(3), Is.EqualTo(1300L));
        }

        private static ContentCatalog CreateCatalog()
        {
            return new ContentCatalog(
                Array.Empty<CargoDefinition>(),
                Array.Empty<IndustryDefinition>(),
                vehicles: ContentCatalog.CreatePlaceholderVehicles());
        }
    }
}
