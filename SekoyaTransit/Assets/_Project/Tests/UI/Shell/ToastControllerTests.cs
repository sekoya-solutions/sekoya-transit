#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class ToastControllerTests
    {
        private VectorImage? _image;
        private PanelSettings? _panelSettings;
        private GameObject? _document;

        [SetUp]
        public void SetUp()
        {
            _image = ScriptableObject.CreateInstance<VectorImage>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_document);
            Object.DestroyImmediate(_panelSettings);
            Object.DestroyImmediate(_image);
        }

        [Test]
        public void Refresh_ContractToast_ShowsTheCargoIconWithItsNameInTheTooltipAndNotInTheText()
        {
            VisualElement stack = PaintContractToast();

            VisualElement toast = stack[0];
            var icons = new List<VisualElement>();
            toast.Query(className: TextLineView.IconUssClass).ToList(icons);
            Assert.That(icons.Count, Is.EqualTo(1));
            Assert.That(icons[0].tooltip, Is.EqualTo("Iron ore"));

            var words = new List<string>();
            toast.Query<Label>().ForEach(label => words.Add(label.text));
            Assert.That(words, Is.EqualTo(new[] { "New", "contract:", "deliver", "175", "to", "Hanoi" }));
        }

        [Test]
        public void Refresh_ContractToast_OnlyTheCargoIconTakesThePointer()
        {
            VisualElement stack = PaintContractToast();

            var pickable = new List<VisualElement>();
            stack.Query<VisualElement>()
                .Where(element => element.pickingMode == PickingMode.Position)
                .ToList(pickable);

            Assert.That(pickable.Count, Is.EqualTo(1));
            Assert.That(pickable[0].ClassListContains(TextLineView.IconUssClass), Is.True);
        }

        [Test]
        public void Tooltip_OwnerLeavesThePanelUnderAMotionlessPointer_Hides()
        {
            VisualElement root = CreatePanelRoot();
            var owner = new VisualElement { tooltip = "Iron ore" };
            root.Add(owner);
            using var tooltip = new ShellTooltip(root);
            Label label = root.Q<Label>(className: ShellTooltip.UssClass);
            using (PointerOverEvent over = PointerOverEvent.GetPooled())
            {
                over.target = owner;
                owner.SendEvent(over);
            }

            Assert.That(label.ClassListContains(ShellTooltip.HiddenUssClass), Is.False);

            owner.RemoveFromHierarchy();

            Assert.That(label.ClassListContains(ShellTooltip.HiddenUssClass), Is.True);
        }

        private VisualElement PaintContractToast()
        {
            var stack = new VisualElement();
            var text = new ShellTextCache();
            text.Set(GoalsUiKeys.ToastContractOffered, "New contract: deliver {1} {0} to {2}");
            var controller = new ToastController(
                stack,
                new ToastQueue(),
                text,
                () => CultureInfo.InvariantCulture,
                new FixedIconSource(_image));
            controller.Show(
                new ToastMessage(
                    GoalsUiKeys.ToastContractOffered,
                    new[]
                    {
                        ToastArgument.FromCargo("iron_ore"),
                        ToastArgument.FromAmount(175L),
                        ToastArgument.FromText("Hanoi")
                    }),
                0f);
            controller.Refresh(0f);
            Assert.That(stack.childCount, Is.EqualTo(1));
            return stack;
        }

        private VisualElement CreatePanelRoot()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _document = new GameObject("TooltipTestDocument");
            var document = _document.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            if (document.rootVisualElement?.panel == null)
            {
                throw new InvalidOperationException("The test document has no panel.");
            }

            return document.rootVisualElement;
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
