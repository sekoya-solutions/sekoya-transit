#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Tests.Shell
{
    /// <summary>
    /// Checks the draw order of the shell root's children (Issue #286, Issue #354). UI Toolkit draws later siblings on
    /// top, so every full-screen window must come after every other HUD element (the toast stack included), and the
    /// toast stack must come after the map-level HUD elements and side panels so toasts still show above them.
    /// </summary>
    public sealed class ShellDrawOrderTests
    {
        private const string UxmlPath = "Assets/_Project/UI/Shell/MapPlayShell.uxml";
        private const string WindowClass = "shell-window";
        private const string ToastStackName = "toast-stack";

        // Modal panels that cover the whole game and may draw above the full-screen windows.
        private static readonly string[] ModalPanelNames =
        {
            "game-over-panel",
            "new-game-panel",
            "save-load-panel",
            "sound-panel",
        };

        [Test]
        public void FullScreenWindows_AreDeclaredAfterEveryOtherHudElement()
        {
            List<VisualElement> children = ShellRootChildren();
            int firstWindow = children.FindIndex(child => child.ClassListContains(WindowClass));
            Assert.That(firstWindow, Is.GreaterThanOrEqualTo(0), "The shell must define at least one full-screen window.");

            string[] drawnAboveWindows = children
                .Skip(firstWindow)
                .Where(child => !child.ClassListContains(WindowClass) && Array.IndexOf(ModalPanelNames, child.name) < 0)
                .Select(child => child.name)
                .ToArray();

            Assert.That(
                drawnAboveWindows,
                Is.Empty,
                "These elements are declared after a full-screen window, so they would draw over it.");
        }

        [Test]
        public void ToastStack_IsDeclaredAfterTheMapHudAndSidePanels()
        {
            List<VisualElement> children = ShellRootChildren();
            int toastStack = children.FindIndex(child => child.name == ToastStackName);
            Assert.That(toastStack, Is.GreaterThanOrEqualTo(0), "The shell must define the toast stack.");

            string[] drawnAboveToasts = children
                .Skip(toastStack + 1)
                .Where(child => !child.ClassListContains(WindowClass) && Array.IndexOf(ModalPanelNames, child.name) < 0)
                .Select(child => child.name)
                .ToArray();

            Assert.That(drawnAboveToasts, Is.Empty, "These HUD elements are declared after the toast stack, so they would hide toasts.");
        }

        private static List<VisualElement> ShellRootChildren()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(tree, Is.Not.Null, UxmlPath);
            VisualElement? shellRoot = tree.CloneTree().Q("shell-root");
            Assert.That(shellRoot, Is.Not.Null, "The shell must define shell-root.");
            return shellRoot!.Children().ToList();
        }
    }
}
