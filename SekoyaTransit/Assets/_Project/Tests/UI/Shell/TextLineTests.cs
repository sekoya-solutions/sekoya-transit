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
                TextLine.Cargo("wood", "wood"));

            Assert.That(line.Segments.Count, Is.EqualTo(2));
            Assert.That(line.Segments[0], Is.EqualTo(TextLineSegment.CargoIcon("wood")));
            Assert.That(line.Segments[1], Is.EqualTo(TextLineSegment.Text("wood to town #2")));
            Assert.That(line.ToPlainText(), Is.EqualTo("wood to town #2"));
        }

        [Test]
        public void Format_ReorderedTranslation_KeepsTheTranslatedOrder()
        {
            TextLine line = TextLine.Format(
                "Ville #{1} : {0}",
                TextLine.Cargo("bread", "pain"),
                TextLine.Plain("4"));

            Assert.That(line.ToPlainText(), Is.EqualTo("Ville #4 : pain"));
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
                new[] { TextLine.Plain("a"), TextLine.Empty, TextLine.Cargo("wool", "wool") });

            Assert.That(joined.Segments.Count, Is.EqualTo(3));
            Assert.That(joined.Segments[0], Is.EqualTo(TextLineSegment.Text("a, , ")));
            Assert.That(joined.Segments[1], Is.EqualTo(TextLineSegment.CargoIcon("wool")));
            Assert.That(TextLine.Concat(TextLine.Plain(""), TextLine.Empty).IsEmpty, Is.True);
        }

        [Test]
        public void Equals_SameSegments_AreEqualWithTheSameHash()
        {
            TextLine first = TextLine.Concat(TextLine.Plain("3 "), TextLine.Cargo("iron", "iron"));
            TextLine second = TextLine.Format("{0} {1}", TextLine.Plain("3"), TextLine.Cargo("iron", "iron"));

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
