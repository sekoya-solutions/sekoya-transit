#nullable enable
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using TransportTycoon.UI.Shell;
using UnityEditor;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class SaveLoadShellPanelsTests
    {
        private const string UxmlPath = "Assets/_Project/UI/Shell/MapPlayShell.uxml";

        private VisualElement _root = null!;
        private SaveLoadShellPanels _panels = null!;
        private readonly List<string> _saved = new List<string>();
        private readonly List<string> _loaded = new List<string>();
        private int _continueCount;
        private int _refreshCount;
        private int _beforeOpenCount;

        [SetUp]
        public void SetUp()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(tree, Is.Not.Null, UxmlPath);
            _root = tree.CloneTree();
            _saved.Clear();
            _loaded.Clear();
            _continueCount = 0;
            _refreshCount = 0;
            _beforeOpenCount = 0;
            _panels = new SaveLoadShellPanels(_root, () => CultureInfo.InvariantCulture, () => _beforeOpenCount++);
            _panels.SaveRequested += slot => _saved.Add(slot);
            _panels.LoadRequested += slot => _loaded.Add(slot);
            _panels.ContinueRequested += () => _continueCount++;
            _panels.SlotsRefreshRequested += () => _refreshCount++;
        }

        [TearDown]
        public void TearDown()
        {
            _panels.Dispose();
        }

        private static SaveSlotEntry Filled(string id)
        {
            return SaveSlotEntry.Filled(id, "1905-03-01", 42UL, 128);
        }

        [Test]
        public void Constructor_PanelIsClosedAndContinueAndLoadAreDisabledUntilASaveIsKnown()
        {
            _panels.Update();

            Assert.That(_panels.IsOpen, Is.False);
            Assert.That(_root.Q<Button>("new-game-continue").enabledSelf, Is.False);
            Assert.That(_root.Q<Button>("new-game-load").enabledSelf, Is.False);
        }

        [Test]
        public void SetSlots_WithASave_EnablesContinueAndLoad()
        {
            _panels.SetSlots(new[] { Filled("2") });
            _panels.Update();

            Assert.That(_root.Q<Button>("new-game-continue").enabledSelf, Is.True);
            Assert.That(_root.Q<Button>("new-game-load").enabledSelf, Is.True);
        }

        [Test]
        public void ContinueButton_RaisesContinueRequested()
        {
            _panels.SetSlots(new[] { Filled("2") });
            _panels.Update();

            _panels.OnContinuePressed();

            Assert.That(_continueCount, Is.EqualTo(1));
        }

        [Test]
        public void LoadGameButton_OpensThePanelAndAsksForFreshSlots()
        {
            _panels.OnLoadGamePressed();

            Assert.That(_panels.IsOpen, Is.True);
            Assert.That(_refreshCount, Is.EqualTo(1));
            Assert.That(_beforeOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Panel_ListsTheAutosaveAndFiveManualSlots()
        {
            _panels.SetSlots(new[] { Filled("2") });
            _panels.SetOpen(true);

            Assert.That(_root.Query<VisualElement>(className: "save-load-row").ToList().Count, Is.EqualTo(6));
        }

        [Test]
        public void Panel_WithoutARunningGame_OffersNoSaveButtons()
        {
            _panels.SetSlots(new[] { Filled("2") });
            _panels.SetOpen(true);

            Assert.That(ButtonsWithText(SaveLoadUiKeys.Save).Count, Is.Zero);
        }

        [Test]
        public void Panel_WithARunningGame_OffersSaveOnTheFiveManualSlotsOnly()
        {
            _panels.SetGameRunning(true);
            _panels.SetSlots(new[] { Filled("2") });
            _panels.SetOpen(true);

            Assert.That(ButtonsWithText(SaveLoadUiKeys.Save).Count, Is.EqualTo(5));
        }

        [Test]
        public void SaveButton_RaisesSaveRequestedWithTheSlotNumber()
        {
            _panels.SetGameRunning(true);
            _panels.SetSlots(new[] { Filled("2") });
            _panels.SetOpen(true);

            _panels.Panel.OnSavePressed("3");

            Assert.That(_saved, Is.EqualTo(new[] { "3" }));
        }

        [Test]
        public void OnSavePressed_Autosave_IsIgnored()
        {
            _panels.SetGameRunning(true);
            _panels.SetSlots(new[] { Filled(SaveSlotEntry.AutosaveId) });
            _panels.SetOpen(true);

            _panels.Panel.OnSavePressed(SaveSlotEntry.AutosaveId);

            Assert.That(_saved, Is.Empty);
        }

        [Test]
        public void LoadWithoutARunningGame_LoadsAtOnceAndClosesThePanel()
        {
            _panels.SetSlots(new[] { Filled("2") });
            _panels.SetOpen(true);

            _panels.Panel.OnLoadPressed("2");

            Assert.That(_loaded, Is.EqualTo(new[] { "2" }));
            Assert.That(_panels.IsOpen, Is.False);
        }

        [Test]
        public void LoadWithARunningGame_WaitsForConfirmation()
        {
            _panels.SetGameRunning(true);
            _panels.SetSlots(new[] { Filled("2") });
            _panels.SetOpen(true);

            _panels.Panel.OnLoadPressed("2");

            Assert.That(_loaded, Is.Empty);
            Assert.That(_panels.Model.PendingLoadId, Is.EqualTo("2"));
            Assert.That(_panels.IsOpen, Is.True);
        }

        [Test]
        public void ConfirmingTheLoad_LoadsTheSlotAndClosesThePanel()
        {
            _panels.SetGameRunning(true);
            _panels.SetSlots(new[] { Filled("2") });
            _panels.SetOpen(true);
            _panels.Panel.OnLoadPressed("2");

            _panels.Panel.OnConfirmYes();

            Assert.That(_loaded, Is.EqualTo(new[] { "2" }));
            Assert.That(_panels.IsOpen, Is.False);
        }

        [Test]
        public void Panel_ShowsTheConfirmationOnlyWhileALoadIsPending()
        {
            _panels.SetGameRunning(true);
            _panels.SetSlots(new[] { Filled("2") });
            _panels.SetOpen(true);
            VisualElement confirm = _root.Q<VisualElement>("save-load-confirm");
            Assert.That(confirm.ClassListContains("save-load-confirm--hidden"), Is.True);

            _panels.Panel.OnLoadPressed("2");
            _panels.Update();

            Assert.That(confirm.ClassListContains("save-load-confirm--hidden"), Is.False);
        }

        [Test]
        public void SetStatusWithOpenPanel_OpensThePanelSoAFailureIsNotMissed()
        {
            _panels.SetStatus(SaveLoadStatus.LoadFailed, openPanel: true);

            Assert.That(_panels.IsOpen, Is.True);
            Assert.That(_panels.Model.Status, Is.EqualTo(SaveLoadStatus.LoadFailed));
        }

        [Test]
        public void ClosingThePanel_ClearsASavedStatusButKeepsAFailure()
        {
            _panels.SetOpen(true);
            _panels.SetStatus(SaveLoadStatus.Saved);
            _panels.SetOpen(false);
            Assert.That(_panels.Model.Status, Is.EqualTo(SaveLoadStatus.None));

            _panels.SetOpen(true);
            _panels.SetStatus(SaveLoadStatus.SaveFailed);
            _panels.SetOpen(false);

            Assert.That(_panels.Model.Status, Is.EqualTo(SaveLoadStatus.SaveFailed));
        }

        private List<Button> ButtonsWithText(string text)
        {
            var matches = new List<Button>();
            foreach (Button button in _root.Q<VisualElement>("save-load-list").Query<Button>().ToList())
            {
                if (button.text == text)
                {
                    matches.Add(button);
                }
            }

            return matches;
        }
    }
}
