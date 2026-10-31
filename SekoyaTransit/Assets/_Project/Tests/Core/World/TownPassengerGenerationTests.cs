#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.World
{
    [TestFixture]
    public sealed class TownPassengerGenerationTests
    {
        private static readonly string[] _emptyCargoIds = Array.Empty<string>();
        private static readonly string[] _woodCargoIds = { "wood" };

        [Test]
        public void AdvanceAll_OnInterval_AddsPassengersScaledByPopulation()
        {
            ContentCatalog catalog = CreateCatalog();
            GeneratedWorld world = GenerateWorld(59001UL, catalog);
            Town town = world.Towns[0];
            int expectedPassengers = TownPassengerGeneration.ComputePassengersGenerated(
                town.Population,
                catalog.TownNeeds);
            int expectedMail = TownPassengerGeneration.ComputeMailGenerated(
                town.Population,
                catalog.TownNeeds);

            TownPassengerGeneration.AdvanceAll(world, catalog, new Tick(1));
            Assert.That(town.WaitingCargo.TotalAmount, Is.EqualTo(0));

            TownPassengerGeneration.AdvanceAll(
                world,
                catalog,
                new Tick(TownPassengerGeneration.GenerationIntervalTicks));

            Assert.That(
                town.WaitingCargo.GetAmount(TownPassengerGeneration.PassengersCargoId),
                Is.EqualTo(expectedPassengers));
            Assert.That(
                town.WaitingCargo.GetAmount(TownPassengerGeneration.MailCargoId),
                Is.EqualTo(expectedMail));
            Assert.That(town.MonthPassengersGenerated, Is.EqualTo(expectedPassengers));
            Assert.That(town.MonthMailGenerated, Is.EqualTo(expectedMail));
        }

        [Test]
        public void AdvanceAll_WhenFull_WastesExcess_StillCreditsGenerated()
        {
            ContentCatalog catalog = CreateCatalog(passengerWaitingCapacity: 3);
            GeneratedWorld world = GenerateWorld(59002UL, catalog);
            Town town = world.Towns[0];
            Assert.That(town.TryAddWaitingCargo("passengers", 3, 3), Is.EqualTo(3));
            int expectedPassengers = TownPassengerGeneration.ComputePassengersGenerated(
                town.Population,
                catalog.TownNeeds);
            int expectedMail = TownPassengerGeneration.ComputeMailGenerated(
                town.Population,
                catalog.TownNeeds);

            TownPassengerGeneration.AdvanceAll(
                world,
                catalog,
                new Tick(TownPassengerGeneration.GenerationIntervalTicks));

            Assert.That(town.WaitingCargo.TotalAmount, Is.EqualTo(3));
            Assert.That(town.MonthPassengersGenerated, Is.EqualTo(expectedPassengers));
            Assert.That(town.MonthMailGenerated, Is.EqualTo(expectedMail));
        }

        [Test]
        public void ComputePassengersGenerated_SmallTown_AtLeastOne()
        {
            TownNeedsSettings settings = TownNeedsSettings.CreatePlaceholder();
            Assert.That(TownPassengerGeneration.ComputePassengersGenerated(100, settings), Is.EqualTo(1));
        }

        [Test]
        public void ComputeMailGenerated_SmallTown_AtLeastOne()
        {
            TownNeedsSettings settings = TownNeedsSettings.CreatePlaceholder();
            Assert.That(TownPassengerGeneration.ComputeMailGenerated(1, settings), Is.EqualTo(1));
            Assert.That(TownPassengerGeneration.ComputeMailGenerated(100, settings), Is.EqualTo(1));
            Assert.That(TownPassengerGeneration.ComputeMailGenerated(500, settings), Is.EqualTo(2));
            Assert.That(TownPassengerGeneration.ComputeMailGenerated(0, settings), Is.EqualTo(0));
        }

        private static ContentCatalog CreateCatalog(int passengerWaitingCapacity = 50)
        {
            var cargo = new[]
            {
                new CargoDefinition("passengers", "cargo.passengers", CargoClass.Passengers, 10, 0),
                new CargoDefinition("mail", "cargo.mail", CargoClass.Passengers, 6, 0),
                new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1)
            };
            var townNeeds = new TownNeedsSettings(
                100,
                500,
                10000,
                new[] { new TownNeedTier(0, 1, 0, 0) },
                demandPerNeedPerMonth: new TownNeedLevelAmounts(10, 10, 10),
                allNeedsMetGrowthMultiplier: 1,
                passengersPerPopulationUnit: 100,
                mailPerPopulationUnit: 250,
                passengerWaitingCapacity: passengerWaitingCapacity);
            return new ContentCatalog(cargo, Array.Empty<IndustryDefinition>(), townNeeds);
        }

        private static GeneratedWorld GenerateWorld(ulong seed, ContentCatalog catalog)
        {
            var industryRules = new[]
            {
                new IndustryPlacementRule("forest", 1, _emptyCargoIds, _woodCargoIds, 1, 0)
            };
            var settings = new WorldGenerationSettings(
                new TownPlacementSettings(MapSize.Small, 1, 8),
                4,
                industryRules);
            return WorldGenerator.Generate(settings, seed, catalog.TownNeeds, catalog);
        }
    }
}
