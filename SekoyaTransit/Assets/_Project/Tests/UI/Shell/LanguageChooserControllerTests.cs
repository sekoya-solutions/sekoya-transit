#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class LanguageChooserControllerTests
    {
        private readonly List<string> _selected = new List<string>();
        private Button _english = null!;
        private Button _french = null!;
        private ShellTextCache _text = null!;
        private MemoryLanguagePreference _preference = null!;
        private bool _selectSucceeds;
        private LanguageChooserController _chooser = null!;

        [SetUp]
        public void SetUp()
        {
            _selected.Clear();
            _english = new Button();
            _french = new Button();
            _text = new ShellTextCache();
            _preference = new MemoryLanguagePreference();
            _selectSucceeds = true;
            _chooser = new LanguageChooserController(
                new[] { _english, _french },
                _text,
                _preference,
                code =>
                {
                    _selected.Add(code);
                    return _selectSucceeds;
                });
        }

        [TearDown]
        public void TearDown()
        {
            _chooser.Dispose();
        }

        [Test]
        public void OnLanguagePressed_CurrentUnknown_IsIgnored()
        {
            _chooser.OnLanguagePressed(ShellLanguages.French);

            Assert.That(_selected, Is.Empty);
            Assert.That(_preference.StoreCount, Is.Zero);
        }

        [Test]
        public void OnLanguagePressed_CurrentLanguage_DoesNothing()
        {
            _chooser.SetCurrent(ShellLanguages.English);

            _chooser.OnLanguagePressed(ShellLanguages.English);

            Assert.That(_selected, Is.Empty);
            Assert.That(_preference.StoreCount, Is.Zero);
        }

        [Test]
        public void OnLanguagePressed_OtherLanguage_SelectsThenStoresIt()
        {
            _chooser.SetCurrent(ShellLanguages.English);

            _chooser.OnLanguagePressed(ShellLanguages.French);

            CollectionAssert.AreEqual(new[] { ShellLanguages.French }, _selected);
            Assert.That(_preference.Load(), Is.EqualTo(ShellLanguages.French));
        }

        [Test]
        public void OnLanguagePressed_SelectFails_DoesNotStore()
        {
            _chooser.SetCurrent(ShellLanguages.English);
            _selectSucceeds = false;

            _chooser.OnLanguagePressed(ShellLanguages.French);

            Assert.That(_preference.StoreCount, Is.Zero);
            Assert.That(_preference.Load(), Is.Null);
        }

        [Test]
        public void OnLanguagePressed_HighlightFollowsTheSelectedLocaleNotThePress()
        {
            _chooser.SetCurrent(ShellLanguages.English);

            _chooser.OnLanguagePressed(ShellLanguages.French);

            // The locale change has not been reported yet, so English stays highlighted.
            Assert.That(_english.ClassListContains(LanguageChooserController.SelectedClass), Is.True);
            Assert.That(_french.ClassListContains(LanguageChooserController.SelectedClass), Is.False);

            _chooser.SetCurrent(ShellLanguages.French);

            Assert.That(_english.ClassListContains(LanguageChooserController.SelectedClass), Is.False);
            Assert.That(_french.ClassListContains(LanguageChooserController.SelectedClass), Is.True);
        }

        [Test]
        public void Refresh_ShowsTheLanguageNamesFromTheTexts()
        {
            _text.Set(ShellLanguages.EnglishNameKey, "English");
            _text.Set(ShellLanguages.FrenchNameKey, "Français");

            _chooser.Refresh(force: false);

            Assert.That(_english.text, Is.EqualTo("English"));
            Assert.That(_french.text, Is.EqualTo("Français"));
        }
    }
}
