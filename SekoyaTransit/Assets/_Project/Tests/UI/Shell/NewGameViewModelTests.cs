#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class NewGameViewModelTests
    {
        [Test]
        public void Constructor_StartsWithRandomSeedAndSmallSize()
        {
            var model = new NewGameViewModel(() => 4242UL);

            Assert.That(model.SeedText, Is.EqualTo("4242"));
            Assert.That(model.SelectedSize, Is.EqualTo(NewGameMapSize.Small));
            Assert.That(model.IsSeedValid, Is.True);
            Assert.That(model.CanStart, Is.True);
        }

        [Test]
        public void Randomize_ReplacesTheSeedWithTheNextRolledValue()
        {
            ulong next = 10UL;
            var model = new NewGameViewModel(() => next++);

            model.SetSeedText("abc");
            model.Randomize();

            Assert.That(model.SeedText, Is.EqualTo("11"));
            Assert.That(model.IsSeedValid, Is.True);
        }

        [TestCase("0", 0UL)]
        [TestCase("37001", 37001UL)]
        [TestCase("  42  ", 42UL)]
        [TestCase("007", 7UL)]
        [TestCase("18446744073709551615", ulong.MaxValue)]
        public void TryParseSeed_WholeNumbers_Parse(string text, ulong expected)
        {
            Assert.That(NewGameViewModel.TryParseSeed(text, out ulong seed), Is.True);
            Assert.That(seed, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("-1")]
        [TestCase("+5")]
        [TestCase("1.5")]
        [TestCase("1,000")]
        [TestCase("12a")]
        [TestCase("١٢٣")]
        [TestCase("18446744073709551616")]
        public void TryParseSeed_InvalidText_IsRejected(string text)
        {
            Assert.That(NewGameViewModel.TryParseSeed(text, out _), Is.False);
        }

        [Test]
        public void TryParseSeed_Null_IsRejected()
        {
            Assert.That(NewGameViewModel.TryParseSeed(null, out _), Is.False);
        }

        [Test]
        public void SetSeedText_Invalid_DisablesStartAndRejectsTheRequest()
        {
            var model = new NewGameViewModel(() => 1UL);

            model.SetSeedText("12x");

            Assert.That(model.IsSeedValid, Is.False);
            Assert.That(model.CanStart, Is.False);
            Assert.That(model.TryCreateSelection(out NewGameSelection selection), Is.False);
            Assert.That(selection, Is.EqualTo(default(NewGameSelection)));
        }

        [Test]
        public void SetSeedText_Null_CountsAsEmpty()
        {
            var model = new NewGameViewModel(() => 1UL);

            model.SetSeedText(null);

            Assert.That(model.SeedText, Is.Empty);
            Assert.That(model.CanStart, Is.False);
        }

        [Test]
        public void SelectSize_KeepsExactlyOneSelected()
        {
            var model = new NewGameViewModel(() => 1UL);

            model.SelectSize(NewGameMapSize.Large);
            Assert.That(model.SelectedSize, Is.EqualTo(NewGameMapSize.Large));

            model.SelectSize(NewGameMapSize.Medium);
            Assert.That(model.SelectedSize, Is.EqualTo(NewGameMapSize.Medium));
        }

        [Test]
        public void SelectSize_UnknownValue_Throws()
        {
            var model = new NewGameViewModel(() => 1UL);

            Assert.Throws<ArgumentOutOfRangeException>(() => model.SelectSize((NewGameMapSize)9));
        }

        [Test]
        public void TryCreateSelection_ValidSeed_ReturnsSeedAndSelectedSize()
        {
            var model = new NewGameViewModel(() => 1UL);
            model.SetSeedText("123456");
            model.SelectSize(NewGameMapSize.Medium);

            Assert.That(model.TryCreateSelection(out NewGameSelection selection), Is.True);
            Assert.That(selection, Is.EqualTo(new NewGameSelection(123456UL, NewGameMapSize.Medium)));
        }

        [TestCase(NewGameMapSize.Small, 128)]
        [TestCase(NewGameMapSize.Medium, 256)]
        [TestCase(NewGameMapSize.Large, 512)]
        public void Tiles_MatchTheMapSides(NewGameMapSize size, int expected)
        {
            Assert.That(NewGameMapSizes.Tiles(size), Is.EqualTo(expected));
        }

        [Test]
        public void FormatSeedInfo_UsesInvariantDigitsWithoutGrouping()
        {
            string text = NewGameViewModel.FormatSeedInfo("Seed {0} · {1}×{1}", 1234567UL, NewGameMapSize.Medium);

            Assert.That(text, Is.EqualTo("Seed 1234567 · 256×256"));
        }

        [Test]
        public void RandomSeed_NeverExceedsThirtyTwoBitsAndFollowsItsGenerator()
        {
            var first = new NewGameRandomSeed(new Random(5));
            var second = new NewGameRandomSeed(new Random(5));

            for (int index = 0; index < 50; index++)
            {
                ulong value = first.Next();
                Assert.That(value, Is.LessThanOrEqualTo(uint.MaxValue));
                Assert.That(second.Next(), Is.EqualTo(value));
            }
        }
    }
}
