#nullable enable
using NUnit.Framework;
using SekoyaTransit.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class TextLineViewTests
    {
        private VectorImage? _image;

        [SetUp]
        public void SetUp()
        {
            _image = ScriptableObject.CreateInstance<VectorImage>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_image);
        }

        [Test]
        public void SetLine_CashAndPopulationIcons_FollowTheirNumbersWithoutNeedingAnIconSource()
        {
            var view = new TextLineView(null);

            view.SetLine(TextLine.Concat(
                TextLine.Plain("6,265"),
                TextLine.CashIcon(),
                TextLine.Plain(", 100"),
                TextLine.PopulationIcon()));

            Assert.That(view.childCount, Is.EqualTo(5));
            Assert.That(((Label)view[0]).text, Is.EqualTo("6,265"));
            Assert.That(view[1].ClassListContains(TextLineView.CashIconUssClass), Is.True);
            Assert.That(view[1].ClassListContains(TextLineView.IconUssClass), Is.True);
            Assert.That(((Label)view[2]).text, Is.EqualTo(","));
            Assert.That(((Label)view[3]).text, Is.EqualTo("100"));
            Assert.That(view[4].ClassListContains(TextLineView.PopulationIconUssClass), Is.True);
        }

        [Test]
        public void SetLine_CargoIcon_ShowsTheCargoNameAsItsTooltipAndCanBeHovered()
        {
            var view = new TextLineView(new FixedIconSource(_image));

            view.SetLine(TextLine.Icon("iron_ore"));

            Assert.That(view[0].tooltip, Is.EqualTo("Iron ore"));
            Assert.That(view[0].pickingMode, Is.EqualTo(PickingMode.Position));
        }

        [Test]
        public void SetLine_CashAndPopulationIcons_ShowTheirLocalizedNamesAsTooltips()
        {
            var text = new ShellTextCache();
            text.Set(GameUiKeys.CashTooltip, "Cash");
            text.Set(GameUiKeys.PopulationTooltip, "Population");
            var view = new TextLineView(null, text);

            view.SetLine(TextLine.Concat(TextLine.CashIcon(), TextLine.PopulationIcon()));

            Assert.That(view[0].tooltip, Is.EqualTo("Cash"));
            Assert.That(view[1].tooltip, Is.EqualTo("Population"));
            Assert.That(view[0].pickingMode, Is.EqualTo(PickingMode.Position));
        }

        [Test]
        public void SetLine_TextAndIcon_KeepsTheIconWithTheWordAfterIt()
        {
            var view = new TextLineView(new FixedIconSource(_image));

            view.SetLine(TextLine.Concat(
                TextLine.Plain("6 "),
                TextLine.Icon("iron_ore"),
                TextLine.Plain("8/40")));

            Assert.That(view.childCount, Is.EqualTo(2));
            Assert.That(((Label)view[0]).text, Is.EqualTo("6"));
            VisualElement chunk = view[1];
            Assert.That(chunk.ClassListContains(TextLineView.ChunkUssClass), Is.True);
            Assert.That(chunk.childCount, Is.EqualTo(2));
            Assert.That(chunk[0].ClassListContains(TextLineView.IconUssClass), Is.True);
            Assert.That(chunk[0].style.backgroundImage.value.vectorImage, Is.SameAs(_image));
            Assert.That(((Label)chunk[1]).text, Is.EqualTo("8/40"));
        }

        [Test]
        public void SetLine_Spaces_BecomeAMarginOnThePieceBeforeThem()
        {
            var view = new TextLineView(new FixedIconSource(_image));

            view.SetLine(TextLine.Concat(TextLine.Plain("6 "), TextLine.Icon("wood"), TextLine.Plain(" x")));

            Assert.That(view[0].ClassListContains(TextLineView.SpaceAfterUssClass), Is.True);
            Assert.That(view[1].ClassListContains(TextLineView.SpaceAfterUssClass), Is.True);
            Assert.That(view[2].ClassListContains(TextLineView.SpaceAfterUssClass), Is.False);
        }

        [Test]
        public void SetLine_IconAtTheEnd_IsAddedAlone()
        {
            var view = new TextLineView(new FixedIconSource(_image));

            view.SetLine(TextLine.Concat(TextLine.Plain("to "), TextLine.Icon("wood")));

            Assert.That(view.childCount, Is.EqualTo(2));
            Assert.That(view[1].ClassListContains(TextLineView.IconUssClass), Is.True);
        }

        [Test]
        public void SetLine_NoIconSource_ShowsTheTextOnly()
        {
            var view = new TextLineView(null);

            view.SetLine(TextLine.Icon("wood"));

            Assert.That(view.childCount, Is.EqualTo(1));
            Assert.That(((Label)view[0]).text, Is.EqualTo("Wood"));
        }

        [Test]
        public void SetLine_IconThenSpace_KeepsTheIconAloneWithASpaceAfter()
        {
            var view = new TextLineView(new FixedIconSource(_image));

            view.SetLine(TextLine.Concat(TextLine.Icon("mail"), TextLine.Plain(" Mail")));

            Assert.That(view.childCount, Is.EqualTo(2));
            Assert.That(view[0].ClassListContains(TextLineView.IconUssClass), Is.True);
            Assert.That(view[0].ClassListContains(TextLineView.SpaceAfterUssClass), Is.True);
            Assert.That(((Label)view[1]).text, Is.EqualTo("Mail"));
        }

        [Test]
        public void SetLine_EqualLine_KeepsTheExistingChildren()
        {
            var view = new TextLineView(new FixedIconSource(_image));
            view.SetLine(TextLine.Icon("wood"));
            VisualElement firstChild = view[0];

            view.SetLine(TextLine.Icon("wood"));

            Assert.That(view[0], Is.SameAs(firstChild));
        }

        [Test]
        public void SetIconButtonName_ClearsTheTextAndKeepsTheNameAsTooltip()
        {
            var button = new Button { text = "Road" };

            ShellTooltip.SetIconButtonName(button, "Road (B)");

            Assert.That(button.text, Is.Empty);
            Assert.That(button.tooltip, Is.EqualTo("Road (B)"));
        }

        [Test]
        public void FindTooltipOwner_ChildOfATooltipElement_ReturnsTheNearestOwner()
        {
            var owner = new VisualElement { tooltip = "Cash" };
            var child = new VisualElement();
            owner.Add(child);

            Assert.That(ShellTooltip.FindTooltipOwner(child), Is.SameAs(owner));
            Assert.That(ShellTooltip.FindTooltipOwner(new VisualElement()), Is.Null);
        }

        private sealed class FixedIconSource : ICargoIconSource
        {
            private readonly VectorImage? _image;

            public FixedIconSource(VectorImage? image)
            {
                _image = image;
            }

            public VectorImage? GetIcon(string cargoId)
            {
                return _image;
            }
        }
    }
}
