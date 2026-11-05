#nullable enable
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Tests.Shell
{
    /// <summary>
    /// Checks the top bar layout of Issue #305: panel button order, and the score only on the game-over screen.
    /// </summary>
    public sealed class TopBarLayoutTests
    {
        private const string UxmlPath = "Assets/_Project/UI/Shell/MapPlayShell.uxml";

        [Test]
        public void PanelButtons_AreInTheRequestedOrder()
        {
            VisualElement root = CloneShell();

            string?[] names = root.Q(className: "shell-hud-panels").Children().Select(child => child.name).ToArray();

            Assert.That(
                names,
                Is.EqualTo(new[]
                {
                    "vehicles-toggle",
                    "routes-toggle",
                    "industries-toggle",
                    "towns-toggle",
                    "contracts-toggle",
                    "finance-toggle",
                    "unlocks-toggle",
                }));
        }

        [Test]
        public void Score_IsOnlyOnTheGameOverPanel()
        {
            VisualElement root = CloneShell();

            Assert.That(root.Q("score-label"), Is.Null);
            Assert.That(root.Q("hud-panel").Q("game-over-score"), Is.Null);
            Assert.That(root.Q("game-over-panel").Q<Label>("game-over-score"), Is.Not.Null);
        }

        private static VisualElement CloneShell()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(tree, Is.Not.Null, UxmlPath);
            return tree.CloneTree();
        }
    }
}
