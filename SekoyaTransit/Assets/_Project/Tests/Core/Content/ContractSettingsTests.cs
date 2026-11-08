#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;
using SekoyaTransit.Core.World;

namespace SekoyaTransit.Core.Tests.Content
{
    [TestFixture]
    public sealed class ContractSettingsTests
    {
        private static readonly ContractLimits _small = new ContractLimits(6, 3, 1);
        private static readonly int[] _noMonths = { 1, 1, 1 };
        private static readonly ContractLimits _medium = new ContractLimits(8, 4, 2);
        private static readonly ContractLimits _large = new ContractLimits(10, 5, 3);

        [Test]
        public void Placeholder_HasTheAgreedValues()
        {
            ContractSettings settings = ContractSettings.CreatePlaceholder();

            Assert.That(settings.GetLimits(MapSize.Small).MaxContracts, Is.EqualTo(6));
            Assert.That(settings.GetLimits(MapSize.Small).MaxAccepted, Is.EqualTo(3));
            Assert.That(settings.GetLimits(MapSize.Medium).MaxContracts, Is.EqualTo(8));
            Assert.That(settings.GetLimits(MapSize.Medium).MaxAccepted, Is.EqualTo(4));
            Assert.That(settings.GetLimits(MapSize.Large).MaxContracts, Is.EqualTo(10));
            Assert.That(settings.GetLimits(MapSize.Large).MaxAccepted, Is.EqualTo(5));
            Assert.That(settings.GetLimits(MapSize.Small).ProposalsPerMonth, Is.EqualTo(1));
            Assert.That(settings.GetLimits(MapSize.Medium).ProposalsPerMonth, Is.EqualTo(2));
            Assert.That(settings.GetLimits(MapSize.Large).ProposalsPerMonth, Is.EqualTo(3));
            Assert.That(settings.InitialProposals, Is.EqualTo(3));
            Assert.That(settings.MinAmount, Is.EqualTo(50));
            Assert.That(settings.MaxAmount, Is.EqualTo(200));
            Assert.That(settings.GetDurationMonths(1), Is.EqualTo(2));
            Assert.That(settings.GetDurationMonths(2), Is.EqualTo(4));
            Assert.That(settings.GetDurationMonths(3), Is.EqualTo(6));
            Assert.That(settings.InitialDurationMonths, Is.EqualTo(12));
            Assert.That(settings.RewardMultiplier, Is.EqualTo(1.0m));
            Assert.That(settings.TownPopulationBonus, Is.EqualTo(100));
        }

        [Test]
        public void Constructor_ZeroLimits_AreAllowed()
        {
            var none = new ContractLimits(0, 0, 1);

            Assert.DoesNotThrow(() => new ContractSettings(none, none, none, 0, 1, 1, _noMonths, 1, 0m, 0));
        }

        [TestCase(-1, 0, 1)]
        [TestCase(3, -1, 1)]
        [TestCase(3, 4, 1)]
        [TestCase(3, 1, 0)]
        [TestCase(3, 1, -1)]
        public void Limits_BadValues_Throw(int maxContracts, int maxAccepted, int proposalsPerMonth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContractLimits(maxContracts, maxAccepted, proposalsPerMonth));
        }

        [TestCase(-1, 50, 200, 12, 12, 1.0, 100)]
        [TestCase(3, 0, 200, 12, 12, 1.0, 100)]
        [TestCase(3, 201, 200, 12, 12, 1.0, 100)]
        [TestCase(3, 50, 200, 0, 12, 1.0, 100)]
        [TestCase(3, 50, 200, 12, 0, 1.0, 100)]
        [TestCase(3, 50, 200, 12, 12, -0.5, 100)]
        [TestCase(3, 50, 200, 12, 12, 1.0, -1)]
        public void Constructor_BadValues_Throws(
            int initialProposals,
            int minAmount,
            int maxAmount,
            int level1Months,
            int initialMonths,
            double rewardMultiplier,
            int townBonus)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ContractSettings(
                    _small,
                    _medium,
                    _large,
                    initialProposals,
                    minAmount,
                    maxAmount,
                    new[] { level1Months, 2, 3 },
                    initialMonths,
                    (decimal)rewardMultiplier,
                    townBonus));
        }

        [TestCase(new[] { 1, 2 })]
        [TestCase(new[] { 1, 2, 3, 4 })]
        public void Constructor_WrongDurationCount_Throws(int[] durations)
        {
            Assert.Throws<ArgumentException>(
                () => new ContractSettings(_small, _medium, _large, 3, 50, 200, durations, 12, 1.0m, 100));
        }

        [TestCase(0, 2)]
        [TestCase(1, 2)]
        [TestCase(3, 6)]
        [TestCase(4, 6)]
        public void GetDurationMonths_ClampsToTheLevelRange(int level, int expectedMonths)
        {
            ContractSettings settings = ContractSettings.CreatePlaceholder();

            Assert.That(settings.GetDurationMonths(level), Is.EqualTo(expectedMonths));
        }

        [Test]
        public void Catalog_WithoutContracts_UsesPlaceholder()
        {
            var catalog = new ContentCatalog(
                new[] { new CargoDefinition("wood", "cargo.wood", CargoClass.Cargo, 8, 1) },
                Array.Empty<IndustryDefinition>());

            Assert.That(catalog.Contracts.GetLimits(MapSize.Large).MaxContracts, Is.EqualTo(10));
        }
    }
}
