#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TransportTycoon.Presentation.Audio;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.Presentation.Tests.Audio
{
    public sealed class SoundSettingsStoreTests
    {
        private FakePreferences _preferences = null!;
        private SoundSettingsStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _preferences = new FakePreferences();
            _store = new SoundSettingsStore(_preferences);
        }

        [Test]
        public void Load_NothingStored_ReturnsDefaults()
        {
            Assert.That(_store.Load(), Is.EqualTo(SoundSettings.Default));
        }

        [Test]
        public void Load_AfterChange_ReturnsTheChangedSettings()
        {
            var settings = new SoundSettings(0.25f, 0.75f, isMuted: true);

            _store.Change(settings, 1f);

            Assert.That(_store.Load(), Is.EqualTo(settings));
            Assert.That(_preferences.Ints[SoundSettingsStore.MutedKey], Is.EqualTo(1));
        }

        [Test]
        public void Load_OutOfRangeStoredVolumes_AreClamped()
        {
            _preferences.Floats[SoundSettingsStore.MusicVolumeKey] = 3f;
            _preferences.Floats[SoundSettingsStore.EffectsVolumeKey] = -1f;

            SoundSettings loaded = _store.Load();

            Assert.That(loaded.MusicVolume, Is.EqualTo(1f));
            Assert.That(loaded.EffectsVolume, Is.EqualTo(0f));
        }

        [Test]
        public void WriteIfDue_WaitsUntilChangesStopForTheDelay()
        {
            _store.Change(SoundSettings.Default.WithMusicVolume(0.1f), 1f);
            _store.Change(SoundSettings.Default.WithMusicVolume(0.2f), 1.3f);

            _store.WriteIfDue(1.3f + SoundSettingsStore.WriteDelaySeconds - 0.01f);
            Assert.That(_preferences.SaveCount, Is.Zero);
            Assert.That(_store.HasPendingWrite, Is.True);

            _store.WriteIfDue(1.3f + SoundSettingsStore.WriteDelaySeconds);
            Assert.That(_preferences.SaveCount, Is.EqualTo(1));
            Assert.That(_store.HasPendingWrite, Is.False);

            _store.WriteIfDue(10f);
            Assert.That(_preferences.SaveCount, Is.EqualTo(1));
        }

        [Test]
        public void Flush_WritesOnlyAPendingChange()
        {
            _store.Flush();
            Assert.That(_preferences.SaveCount, Is.Zero);

            _store.Change(SoundSettings.Default.WithMuted(true), 1f);
            _store.Flush();
            Assert.That(_preferences.SaveCount, Is.EqualTo(1));
        }

        private sealed class FakePreferences : IPlayerPreferences
        {
            public Dictionary<string, float> Floats { get; } = new Dictionary<string, float>();

            public Dictionary<string, int> Ints { get; } = new Dictionary<string, int>();

            public int SaveCount { get; private set; }

            public float GetFloat(string key, float fallback)
            {
                return Floats.TryGetValue(key, out float value) ? value : fallback;
            }

            public int GetInt(string key, int fallback)
            {
                return Ints.TryGetValue(key, out int value) ? value : fallback;
            }

            public void SetFloat(string key, float value)
            {
                Floats[key] = value;
            }

            public void SetInt(string key, int value)
            {
                Ints[key] = value;
            }

            public void Save()
            {
                SaveCount++;
            }
        }
    }
}
