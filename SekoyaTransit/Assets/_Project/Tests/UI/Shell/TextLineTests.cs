#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class TextLineTests
    {
        [Test]
        public void Format_CargoArgument_PutsTheIconBeforeTheNameInFormatOrder()
        {
            TextLine line = TextLine.Format(
                "{1} to town #{0}",
                TextLine.Plain("2"),
                TextLine.Icon("wood"));

            Assert.That(line.Segments.Count, Is.EqualTo(2));
            Assert.That(line.Segments[0], Is.EqualTo(TextLineSegment.CargoIcon("wood")));
            Assert.That(line.Segments[1], Is.EqualTo(TextLineSegment.Text(" to town #2")));
            Assert.That(line.ToPlainText(), Is.EqualTo(" to town #2"));
        }

        [Test]
        public void Format_ReorderedTranslation_KeepsTheTranslatedOrder()
        {
            TextLine line = TextLine.Format(
                "Ville #{1} : {0}",
                TextLine.Icon("bread"),
                TextLine.Plain("4"));

            Assert.That(line.ToPlainText(), Is.EqualTo("Ville #4 : "));
            Assert.That(line.Segments[1], Is.EqualTo(TextLineSegment.CargoIcon("bread")));
        }

        [Test]
        public void Format_EscapedBraces_AreLiteral()
        {
            Assert.That(TextLine.Format("{{{0}}}", TextLine.Plain("x")).ToPlainText(), Is.EqualTo("{x}"));
        }

        [TestCase("{0")]
        [TestCase("}")]
        [TestCase("{}")]
        [TestCase("{1}")]
        [TestCase("{0:N0}")]
        [TestCase("{0,5}")]
        public void Format_MalformedOrMissingArgument_Throws(string format)
        {
            Assert.Throws<FormatException>(() => TextLine.Format(format, TextLine.Plain("x")));
        }

        [Test]
        public void ConcatAndJoin_MergeAdjacentTextAndDropEmptyText()
        {
            TextLine joined = TextLine.Join(
                ", ",
                new[] { TextLine.Plain("a"), TextLine.Empty, TextLine.Icon("wool") });

            Assert.That(joined.Segments.Count, Is.EqualTo(2));
            Assert.That(joined.Segments[0], Is.EqualTo(TextLineSegment.Text("a, , ")));
            Assert.That(joined.Segments[1], Is.EqualTo(TextLineSegment.CargoIcon("wool")));
            Assert.That(TextLine.Concat(TextLine.Plain(""), TextLine.Empty).IsEmpty, Is.True);
        }

        [Test]
        public void CashAndPopulationIcons_AreSegmentsThatShowNoPlainText()
        {
            TextLine line = TextLine.Concat(
                TextLine.Plain("100"),
                TextLine.CashIcon(),
                TextLine.Plain(", 5"),
                TextLine.PopulationIcon());

            Assert.That(line.Segments.Count, Is.EqualTo(4));
            Assert.That(line.Segments[1], Is.EqualTo(TextLineSegment.Cash));
            Assert.That(line.Segments[3], Is.EqualTo(TextLineSegment.Population));
            Assert.That(line.ToPlainText(), Is.EqualTo("100, 5"));
            Assert.That(TextLineSegment.Cash, Is.Not.EqualTo(TextLineSegment.Population));
        }

        [Test]
        public void Equals_SameSegments_AreEqualWithTheSameHash()
        {
            TextLine first = TextLine.Concat(TextLine.Plain("3 "), TextLine.Icon("iron"));
            TextLine second = TextLine.Format("{0} {1}", TextLine.Plain("3"), TextLine.Icon("iron"));

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(TextLine.Plain("3 iron")));
        }

        [Test]
        public void CargoIcon_BlankId_Throws()
        {
            Assert.Throws<ArgumentException>(() => TextLineSegment.CargoIcon(" "));
        }
    }
}
