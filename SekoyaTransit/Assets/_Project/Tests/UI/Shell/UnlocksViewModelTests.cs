#nullable enable
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class UnlocksViewModelTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void BuildColumns_GroupsByModeInOrderAndAddsTeaserColumns()
        {
            var items = new[]
            {
                Truck("truck_mk1", 1900, VehicleUnlockRowStatus.Unlocked),
                Loco("loco_mk2", 1907, VehicleUnlockRowStatus.Future),
                Truck("truck_mk2", 1904, VehicleUnlockRowStatus.Unlockable),
            };

            var columns = UnlocksViewModel.BuildColumns(items, CreateText(), _culture);

            Assert.That(columns.Count, Is.EqualTo(4));
            Assert.That(columns[0].Title, Is.EqualTo("Trucks"));
            Assert.That(columns[0].Cards[0].TypeId, Is.EqualTo("truck_mk1"));
            Assert.That(columns[0].Cards[1].TypeId, Is.EqualTo("truck_mk2"));
            Assert.That(columns[1].Title, Is.EqualTo("Trains"));
            Assert.That(columns[1].Cards.Count, Is.EqualTo(1));
            Assert.That(columns[2].Title, Is.EqualTo("Boats"));
            Assert.That(columns[3].Title, Is.EqualTo("Planes"));
        }

        [Test]
        public void BuildColumns_BoatsAndPlanes_ShowOneGreyedComingSoonCard()
        {
            var columns = UnlocksViewModel.BuildColumns(new VehicleUnlockListItem[0], CreateText(), _culture);

            for (int index = 2; index < 4; index++)
            {
                Assert.That(columns[index].Cards.Count, Is.EqualTo(1));
                UnlockCardRow card = columns[index].Cards[0];
                Assert.That(card.Kind, Is.EqualTo(UnlockCardKind.ComingSoon));
                Assert.That(card.Header, Is.EqualTo("Coming soon"));
                Assert.That(card.IsGreyedOut, Is.True);
                Assert.That(card.StatLines, Is.Empty);
                Assert.That(card.TypeId, Is.Null);
            }
        }

        [Test]
        public void BuildCard_Owned_ShowsUnlockedYearAndOwnedFooter()
        {
            UnlockCardRow card = UnlocksViewModel.BuildCard(
                Truck("truck_mk1", 1900, VehicleUnlockRowStatus.Unlocked),
                CreateText(),
                _culture);

            Assert.That(card.Header, Is.EqualTo("Truck Mk1 - Unlocked 1900"));
            Assert.That(card.Kind, Is.EqualTo(UnlockCardKind.Owned));
            Assert.That(card.Footer, Is.EqualTo("Already owned"));
            Assert.That(card.IsGreyedOut, Is.False);
            Assert.That(card.TypeId, Is.EqualTo("truck_mk1"));
        }

        [Test]
        public void BuildCard_Buyable_ShowsBuyTextAndAffordability()
        {
            UnlockCardRow affordable = UnlocksViewModel.BuildCard(
                Truck("truck_mk2", 1904, VehicleUnlockRowStatus.Unlockable, afford: true),
                CreateText(),
                _culture);
            UnlockCardRow tooExpensive = UnlocksViewModel.BuildCard(
                Truck("truck_mk2", 1904, VehicleUnlockRowStatus.Unlockable, afford: false),
                CreateText(),
                _culture);

            Assert.That(affordable.Kind, Is.EqualTo(UnlockCardKind.Buyable));
            Assert.That(affordable.Footer, Is.EqualTo("Buy for 250,000"));
            Assert.That(affordable.CanBuy, Is.True);
            Assert.That(tooExpensive.CanBuy, Is.False);
            Assert.That(affordable.IsGreyedOut, Is.False);
        }

        [Test]
        public void BuildCard_Future_IsGreyedOutWithNotYetAvailableFooter()
        {
            UnlockCardRow card = UnlocksViewModel.BuildCard(
                Loco("loco_mk2", 1907, VehicleUnlockRowStatus.Future),
                CreateText(),
                _culture);

            Assert.That(card.Header, Is.EqualTo("Loco Mk2 - Unlocks 1907"));
            Assert.That(card.Kind, Is.EqualTo(UnlockCardKind.Future));
            Assert.That(card.Footer, Is.EqualTo("Not yet available"));
            Assert.That(card.IsGreyedOut, Is.True);
            Assert.That(card.TypeId, Is.EqualTo("loco_mk2"));
        }

        [Test]
        public void FormatStats_Truck_ListsCapacitySpeedPriceUpkeep()
        {
            var lines = UnlocksViewModel.FormatStats(
                Truck("truck_mk1", 1900, VehicleUnlockRowStatus.Unlocked),
                CreateText(),
                _culture);

            Assert.That(
                lines,
                Is.EqualTo(new[] { "Cargo capacity: 10", "Speed: 5.0 tiles/s", "Price: 10,000", "Upkeep: 400/month" }));
        }

        [Test]
        public void FormatStats_Train_ListsWagonCapacityAndMaxWagons()
        {
            var lines = UnlocksViewModel.FormatStats(
                Loco("loco_mk2", 1907, VehicleUnlockRowStatus.Future),
                CreateText(),
                _culture);

            Assert.That(
                lines,
                Is.EqualTo(
                    new[]
                    {
                        "Wagon capacity: 14",
                        "Speed: 6.8 tiles/s",
                        "Max wagons: 5",
                        "Price: 30,000",
                        "Upkeep: 550/month",
                    }));
        }

        [TestCase(250, 0.05, 50)]
        [TestCase(270, 0.05, 54)]
        [TestCase(420, 0.05, 84)]
        [TestCase(250, 0.1, 25)]
        public void SpeedTenthsPerSecond_ConvertsThousandthsPerTick(int thousandths, double tickSeconds, int expected)
        {
            Assert.That(UnlocksViewModel.SpeedTenthsPerSecond(thousandths, tickSeconds), Is.EqualTo(expected));
        }

        [Test]
        public void SpeedTenthsPerSecond_NonPositiveTick_Throws()
        {
            Assert.That(
                () => UnlocksViewModel.SpeedTenthsPerSecond(250, 0d),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void ComputeFingerprint_ChangesWithStatusAffordabilityAndStats()
        {
            int baseline = UnlocksViewModel.ComputeFingerprint(
                new[] { Truck("truck_mk2", 1904, VehicleUnlockRowStatus.Unlockable, afford: true) });

            Assert.That(
                UnlocksViewModel.ComputeFingerprint(
                    new[] { Truck("truck_mk2", 1904, VehicleUnlockRowStatus.Unlockable, afford: true) }),
                Is.EqualTo(baseline));
            Assert.That(
                UnlocksViewModel.ComputeFingerprint(
                    new[] { Truck("truck_mk2", 1904, VehicleUnlockRowStatus.Unlockable, afford: false) }),
                Is.Not.EqualTo(baseline));
            Assert.That(
                UnlocksViewModel.ComputeFingerprint(
                    new[] { Truck("truck_mk2", 1904, VehicleUnlockRowStatus.Unlocked, afford: true) }),
                Is.Not.EqualTo(baseline));
        }

        private static VehicleUnlockListItem Truck(
            string id,
            int year,
            VehicleUnlockRowStatus status,
            bool afford = true)
        {
            return new VehicleUnlockListItem(
                id,
                "vehicle." + id,
                VehicleListKind.Road,
                year,
                250000L,
                status,
                afford,
                new VehicleUnlockStats(10, 0, 50, 10000L, 400L));
        }

        private static VehicleUnlockListItem Loco(string id, int year, VehicleUnlockRowStatus status)
        {
            return new VehicleUnlockListItem(
                id,
                "vehicle." + id,
                VehicleListKind.Rail,
                year,
                600000L,
                status,
                false,
                new VehicleUnlockStats(14, 5, 68, 30000L, 550L));
        }

        private static ShellTextCache CreateText()
        {
            var text = new ShellTextCache();
            text.Set(GameUiKeys.UnlocksColumnTrucks, "Trucks");
            text.Set(GameUiKeys.UnlocksColumnTrains, "Trains");
            text.Set(GameUiKeys.UnlocksColumnBoats, "Boats");
            text.Set(GameUiKeys.UnlocksColumnPlanes, "Planes");
            text.Set(GameUiKeys.UnlocksCardUnlocked, "{0} - Unlocked {1}");
            text.Set(GameUiKeys.UnlocksCardUnlocks, "{0} - Unlocks {1}");
            text.Set(GameUiKeys.UnlocksStatCapacity, "Cargo capacity: {0}");
            text.Set(GameUiKeys.UnlocksStatWagonCapacity, "Wagon capacity: {0}");
            text.Set(GameUiKeys.UnlocksStatSpeed, "Speed: {0} tiles/s");
            text.Set(GameUiKeys.UnlocksStatMaxWagons, "Max wagons: {0}");
            text.Set(GameUiKeys.UnlocksStatPrice, "Price: {0}");
            text.Set(GameUiKeys.UnlocksStatUpkeep, "Upkeep: {0}/month");
            text.Set(GameUiKeys.UnlocksStatusOwned, "Already owned");
            text.Set(GameUiKeys.UnlocksStatusFuture, "Not yet available");
            text.Set(GameUiKeys.UnlocksComingSoon, "Coming soon");
            text.Set(GameUiKeys.UnlocksAction, "Buy for {0}");
            text.Set("vehicle.truck_mk1", "Truck Mk1");
            text.Set("vehicle.truck_mk2", "Truck Mk2");
            text.Set("vehicle.loco_mk2", "Loco Mk2");
            return text;
        }
    }
}
