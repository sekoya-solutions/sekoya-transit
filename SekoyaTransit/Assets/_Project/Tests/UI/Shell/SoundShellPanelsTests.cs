#nullable enable
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class SoundShellPanelsTests
    {
        private const string UxmlPath = "Assets/_Project/UI/Shell/MapPlayShell.uxml";

        private VisualElement _root = null!;
        private SoundShellPanels _panels = null!;
        private readonly List<SoundSettings> _changes = new List<SoundSettings>();
        private int _beforeOpenCount;

        [SetUp]
        public void SetUp()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(tree, Is.Not.Null, UxmlPath);
            _root = tree.CloneTree();
            _changes.Clear();
            _beforeOpenCount = 0;
            _panels = new SoundShellPanels(_root, () => CultureInfo.InvariantCulture, () => _beforeOpenCount++);
            _panels.SettingsChanged += settings => _changes.Add(settings);
        }

        [TearDown]
        public void TearDown()
        {
            _panels.Dispose();
        }

        [Test]
        public void Constructor_PanelIsClosed()
        {
            Assert.That(_panels.IsOpen, Is.False);
            Assert.That(_root.Q("sound-panel").ClassListContains("sound-panel--hidden"), Is.True);
        }

        [Test]
        public void Toggle_OpensAfterClosingOtherPanelsAndClosesAgain()
        {
            _panels.Toggle();

            Assert.That(_panels.IsOpen, Is.True);
            Assert.That(_beforeOpenCount, Is.EqualTo(1));
            Assert.That(_root.Q("sound-panel").ClassListContains("sound-panel--hidden"), Is.False);

            _panels.Toggle();

            Assert.That(_panels.IsOpen, Is.False);
            Assert.That(_beforeOpenCount, Is.EqualTo(1));
        }

        [Test]
        public void SetSettings_ShowsValuesWithoutRaisingAChange()
        {
            var stored = new SoundSettings(0.2f, 0.9f, isMuted: true);

            _panels.SetSettings(stored);

            Assert.That(_root.Q<Slider>("sound-music").value, Is.EqualTo(0.2f));
            Assert.That(_root.Q<Slider>("sound-effects").value, Is.EqualTo(0.9f));
            Assert.That(_root.Q<Toggle>("sound-mute").value, Is.True);
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void PlayerInput_RaisesEachChangeWithTheOtherValuesKept()
        {
            _panels.SetSettings(new SoundSettings(0.5f, 0.5f, isMuted: false));

            _panels.Panel.SetSettings(_panels.Panel.Settings.WithMusicVolume(0.3f), notify: true);
            _panels.Panel.SetSettings(_panels.Panel.Settings.WithEffectsVolume(0.7f), notify: true);
            _panels.Panel.SetSettings(_panels.Panel.Settings.WithMuted(true), notify: true);

            Assert.That(
                _changes,
                Is.EqualTo(
                    new[]
                    {
                        new SoundSettings(0.3f, 0.5f, false),
                        new SoundSettings(0.3f, 0.7f, false),
                        new SoundSettings(0.3f, 0.7f, true),
                    }));
            Assert.That(_root.Q<Slider>("sound-music").value, Is.EqualTo(0.3f));
            Assert.That(_root.Q<Toggle>("sound-mute").value, Is.True);
        }

        [Test]
        public void PlayerInput_SameValue_RaisesNothing()
        {
            _panels.Panel.SetSettings(_panels.Panel.Settings, notify: true);

            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void Localization_EverySoundKey_HasEnglishText()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(GameUiKeys.Table);
            Assert.That(collection, Is.Not.Null);
            var table = (StringTable)collection.GetTable(new LocaleIdentifier("en"));
            foreach (string key in SoundUiKeys.StaticKeys)
            {
                Assert.That(table.GetEntry(key)?.Value, Is.Not.Null.And.Not.Empty, key);
            }
        }

        [Test]
        public void IsButtonOrInsideButton_FindsEnabledButtonsOnly()
        {
            var root = new VisualElement();
            var button = new Button();
            var icon = new VisualElement();
            var label = new Label();
            button.Add(icon);
            root.Add(button);
            root.Add(label);

            Assert.That(MapPlayShell.IsButtonOrInsideButton(button, root), Is.True);
            Assert.That(MapPlayShell.IsButtonOrInsideButton(icon, root), Is.True);
            Assert.That(MapPlayShell.IsButtonOrInsideButton(label, root), Is.False);
            Assert.That(MapPlayShell.IsButtonOrInsideButton(root, root), Is.False);
            Assert.That(MapPlayShell.IsButtonOrInsideButton(null, root), Is.False);

            button.SetEnabled(false);
            Assert.That(MapPlayShell.IsButtonOrInsideButton(icon, root), Is.False);
        }
    }
}
