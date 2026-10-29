#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;

namespace SekoyaTransit.Core.Tests.Content
{
    [TestFixture]
    public sealed class ContractSettingsTests
    {
        [Test]
        public void Placeholder_HasTheAgreedValues()
        {
            ContractSettings settings = ContractSettings.CreatePlaceholder();

            Assert.That(settings.MaxOpenContracts, Is.EqualTo(3));
            Assert.That(settings.MinAmount, Is.EqualTo(50));
            Assert.That(settings.MaxAmount, Is.EqualTo(200));
            Assert.That(settings.DurationMonths, Is.EqualTo(12));
            Assert.That(settings.RewardMultiplier, Is.EqualTo(1.0m));
            Assert.That(settings.TownPopulationBonus, Is.EqualTo(100));
        }

        [Test]
        public void Constructor_ZeroOpenContracts_IsAllowed()
        {
            Assert.DoesNotThrow(() => new ContractSettings(0, 1, 1, 1, 0m, 0));
        }

        [TestCase(-1, 50, 200, 12, 1.0, 100)]
        [TestCase(3, 0, 200, 12, 1.0, 100)]
        [TestCase(3, 201, 200, 12, 1.0, 100)]
        [TestCase(3, 50, 200, 0, 1.0, 100)]
        [TestCase(3, 50, 200, 12, -0.5, 100)]
        [TestCase(3, 50, 200, 12, 1.0, -1)]
        public void Constructor_BadValues_Throws(
            int maxOpen,
            int minAmount,
            int maxAmount,
            int durationMonths,
            double rewardMultiplier,
            int townBonus)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ContractSettings(
                    maxOpen,
                    minAmount,
                    maxAmount,
                    durationMonths,
                    (decimal)rewardMultiplier,
                    townBonus));
        }

        [Test]
        public void Catalog_WithoutContracts_UsesPlaceholder()
        {
            var catalog = new ContentCatalog(
                new[] { new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1) },
                Array.Empty<IndustryDefinition>());

            Assert.That(catalog.Contracts.MaxOpenContracts, Is.EqualTo(3));
        }
    }
}
