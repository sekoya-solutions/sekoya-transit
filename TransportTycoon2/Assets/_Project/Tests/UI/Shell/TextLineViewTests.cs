#nullable enable
using NUnit.Framework;
using TransportTycoon.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Tests.Shell
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
        public void SetLine_TextAndIcon_BuildsALabelAndAnIconInOrder()
        {
            var view = new TextLineView(new FixedIconSource(_image));

            view.SetLine(TextLine.Concat(TextLine.Plain("6 "), TextLine.Cargo("wood", "wood")));

            Assert.That(view.childCount, Is.EqualTo(3));
            Assert.That(((Label)view[0]).text, Is.EqualTo("6 "));
            Assert.That(view[1].ClassListContains(TextLineView.IconUssClass), Is.True);
            Assert.That(view[1].style.backgroundImage.value.vectorImage, Is.SameAs(_image));
            Assert.That(((Label)view[2]).text, Is.EqualTo("wood"));
        }

        [Test]
        public void SetLine_NoIconSource_ShowsTheTextOnly()
        {
            var view = new TextLineView(null);

            view.SetLine(TextLine.Cargo("wood", "wood"));

            Assert.That(view.childCount, Is.EqualTo(1));
            Assert.That(((Label)view[0]).text, Is.EqualTo("wood"));
        }

        [Test]
        public void SetLine_EqualLine_KeepsTheExistingChildren()
        {
            var view = new TextLineView(new FixedIconSource(_image));
            view.SetLine(TextLine.Cargo("wood", "wood"));
            VisualElement firstChild = view[0];

            view.SetLine(TextLine.Cargo("wood", "wood"));

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
