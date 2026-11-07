#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;
using SekoyaTransit.Presentation.Map;

namespace SekoyaTransit.Presentation.Tests.Map
{
    [TestFixture]
    public sealed class GameDateTextTests
    {
        [TestCase("fr", "04/03/1902")]
        [TestCase("fr-FR", "04/03/1902")]
        [TestCase("fr-CA", "04/03/1902")]
        [TestCase("en", "1902-03-04")]
        [TestCase("en-US", "1902-03-04")]
        [TestCase("de", "1902-03-04")]
        public void Format_ByLanguage_FrenchIsDayMonthYearOthersAreYearMonthDay(string cultureName, string expected)
        {
            // Issue #338: chosen by language code, not by the culture's own short date pattern (en-US would be
            // M/d/yyyy, fr-CA would be yyyy-MM-dd).
            string text = GameDateText.Format(new GameDate(1902, 3, 4), CultureInfo.GetCultureInfo(cultureName));

            Assert.That(text, Is.EqualTo(expected));
        }

        [Test]
        public void Format_Invariant_IsYearMonthDay()
        {
            Assert.That(
                GameDateText.Format(new GameDate(1900, 12, 31), CultureInfo.InvariantCulture),
                Is.EqualTo("1900-12-31"));
        }

        [Test]
        public void Format_NullCulture_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GameDateText.Format(new GameDate(1900, 1, 1), null!));
        }
    }
}
