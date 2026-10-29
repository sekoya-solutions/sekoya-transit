#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Core.Content;

namespace SekoyaTransit.Core.Tests.Content
{
    [TestFixture]
    public sealed class AchievementDefinitionTests
    {
        [TestCase(0L)]
        [TestCase(-5L)]
        public void Constructor_NonPositiveGoal_Throws(long goal)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new AchievementDefinition("a", "k", AchievementKind.CompanyCash, goal));
        }

        [Test]
        public void Constructor_UnknownKind_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new AchievementDefinition("a", "k", (AchievementKind)99, 5L));
        }

        [Test]
        public void Constructor_CargoDeliveredWithoutCargoOrDestination_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition("a", "k", AchievementKind.CargoDelivered, 5L));
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition("a", "k", AchievementKind.CargoDelivered, 5L, "bread"));
        }

        [Test]
        public void Constructor_CargoOnNonDeliveryKind_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => new AchievementDefinition("a", "k", AchievementKind.VehicleCount, 5L, "bread"));
        }

        [Test]
        public void Catalog_DuplicateIds_Throws()
        {
            var achievements = new[]
            {
                new AchievementDefinition("a", "k", AchievementKind.CompanyCash, 5L),
                new AchievementDefinition("a", "k2", AchievementKind.VehicleCount, 3L)
            };

            Assert.Throws<ArgumentException>(
                () => new ContentCatalog(
                    Array.Empty<CargoDefinition>(),
                    Array.Empty<IndustryDefinition>(),
                    achievements: achievements));
        }

        [Test]
        public void Catalog_UnknownCargoId_Throws()
        {
            var achievements = new[]
            {
                new AchievementDefinition(
                    "a",
                    "k",
                    AchievementKind.CargoDelivered,
                    5L,
                    "missing",
                    DeliveryDestinationKind.Any)
            };

            Assert.Throws<ArgumentException>(
                () => new ContentCatalog(
                    Array.Empty<CargoDefinition>(),
                    Array.Empty<IndustryDefinition>(),
                    achievements: achievements));
        }

        [Test]
        public void Catalog_WithoutAchievements_IsEmpty()
        {
            var catalog = new ContentCatalog(Array.Empty<CargoDefinition>(), Array.Empty<IndustryDefinition>());

            Assert.That(catalog.Achievements, Is.Empty);
        }
    }
}
