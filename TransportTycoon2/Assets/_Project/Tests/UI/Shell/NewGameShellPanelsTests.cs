#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.UI.Shell;
using UnityEditor;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class NewGameShellPanelsTests
    {
        private const string UxmlPath = "Assets/_Project/UI/Shell/MapPlayShell.uxml";

        private VisualElement _root = null!;
        private NewGameShellPanels _panels = null!;
        private readonly List<NewGameSelection> _requested = new List<NewGameSelection>();
        private int _gameOverNewGameCount;
        private ulong _nextSeed;

        [SetUp]
        public void SetUp()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(tree, Is.Not.Null, UxmlPath);
            _root = tree.CloneTree();
            _root.AddToClassList("shell-root");
            _requested.Clear();
            _gameOverNewGameCount = 0;
            _nextSeed = 100UL;
            _panels = new NewGameShellPanels(_root, () => _nextSeed++);
            _panels.NewGameRequested += selection => _requested.Add(selection);
            _panels.GameOverNewGameRequested += () => _gameOverNewGameCount++;
        }

        [TearDown]
        public void TearDown()
        {
            _panels.Dispose();
        }

        [Test]
        public void Constructor_ShowsTheScreenWithARandomSeedAndSmallSelected()
        {
            Assert.That(_panels.IsNewGameScreenOpen, Is.True);
            Assert.That(_panels.Panel.ClassListContains("new-game-panel--hidden"), Is.False);
            Assert.That(_root.ClassListContains("shell-root--new-game"), Is.True);
            Assert.That(_root.Q<TextField>("new-game-seed").value, Is.EqualTo("100"));
            Assert.That(_root.Q<Button>("new-game-size-small").ClassListContains("new-game-size--selected"), Is.True);
            Assert.That(_root.Q<Button>("new-game-size-medium").ClassListContains("new-game-size--selected"), Is.False);
            Assert.That(_root.Q<Button>("new-game-size-large").ClassListContains("new-game-size--selected"), Is.False);
            Assert.That(_root.Q<Button>("new-game-start").enabledSelf, Is.True);
            Assert.That(_root.Q<Label>("new-game-seed-error").ClassListContains("new-game-error--hidden"), Is.True);
        }

        [Test]
        public void RandomPressed_PutsANewSeedInTheField()
        {
            _panels.Screen.OnRandomPressed();

            Assert.That(_root.Q<TextField>("new-game-seed").value, Is.EqualTo("101"));
            Assert.That(_panels.Screen.Model.SeedText, Is.EqualTo("101"));
        }

        [Test]
        public void InvalidSeed_ShowsTheErrorAndDisablesStart()
        {
            _panels.Screen.OnSeedTextChanged("12x");

            Assert.That(_root.Q<Label>("new-game-seed-error").ClassListContains("new-game-error--hidden"), Is.False);
            Assert.That(_root.Q<Button>("new-game-start").enabledSelf, Is.False);

            _panels.Screen.OnStartPressed();
            Assert.That(_requested, Is.Empty);
        }

        [Test]
        public void SizePressed_SelectsExactlyOneToggle()
        {
            _panels.Screen.OnSizePressed(NewGameMapSize.Large);

            Assert.That(_root.Q<Button>("new-game-size-small").ClassListContains("new-game-size--selected"), Is.False);
            Assert.That(_root.Q<Button>("new-game-size-medium").ClassListContains("new-game-size--selected"), Is.False);
            Assert.That(_root.Q<Button>("new-game-size-large").ClassListContains("new-game-size--selected"), Is.True);
        }

        [Test]
        public void StartPressed_RaisesTheChosenSeedAndSize()
        {
            _panels.Screen.OnSeedTextChanged("987654321");
            _panels.Screen.OnSizePressed(NewGameMapSize.Medium);

            _panels.Screen.OnStartPressed();

            Assert.That(_requested, Is.EqualTo(new[] { new NewGameSelection(987654321UL, NewGameMapSize.Medium) }));
        }

        [Test]
        public void CloseNewGameScreen_HidesTheScreenAndShowsTheChrome()
        {
            _panels.CloseNewGameScreen();

            Assert.That(_panels.IsNewGameScreenOpen, Is.False);
            Assert.That(_panels.Panel.ClassListContains("new-game-panel--hidden"), Is.True);
            Assert.That(_root.ClassListContains("shell-root--new-game"), Is.False);

            _panels.Screen.OnStartPressed();
            Assert.That(_requested, Is.Empty);
        }

        [Test]
        public void SetGameInfo_ShowsTheSeedAndSizeInThePanelAndOnTheGameOverScreen()
        {
            _panels.SetGameInfo(new NewGameSelection(123456UL, NewGameMapSize.Medium));

            // The text cache has no localization in EditMode, so the template key stands in for the template.
            Assert.That(_root.Q<Label>("seed-info-label").text, Is.Not.Empty);
            Assert.That(_root.Q<Label>("seed-info-label").text, Is.EqualTo(_root.Q<Label>("game-over-seed").text));
            Assert.That(_root.Q<Button>("game-over-new-game").text, Is.Not.Empty);
        }

        [Test]
        public void GameOverNewGamePressed_RaisesTheRequestOnce()
        {
            _panels.OnGameOverNewGamePressed();

            Assert.That(_gameOverNewGameCount, Is.EqualTo(1));
        }

        [Test]
        public void MarkupDefinesTheGameOverNewGameButtonInsideTheGameOverPanel()
        {
            Button button = _root.Q<Button>("game-over-new-game");

            Assert.That(button, Is.Not.Null);
            Assert.That(_root.Q<VisualElement>("game-over-panel").Contains(button), Is.True);
        }
    }
}
