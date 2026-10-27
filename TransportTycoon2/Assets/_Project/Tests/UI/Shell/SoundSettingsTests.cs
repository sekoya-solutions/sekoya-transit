#nullable enable
using NUnit.Framework;
using TransportTycoon.UI.Shell;

namespace TransportTycoon.UI.Tests.Shell
{
    public sealed class SoundSettingsTests
    {
        [Test]
        public void Default_IsAudibleWithDefaultVolumes()
        {
            SoundSettings settings = SoundSettings.Default;

            Assert.That(settings.MusicVolume, Is.EqualTo(SoundSettings.DefaultMusicVolume));
            Assert.That(settings.EffectsVolume, Is.EqualTo(SoundSettings.DefaultEffectsVolume));
            Assert.That(settings.IsMuted, Is.False);
        }

        [TestCase(-0.5f, 0f)]
        [TestCase(0.3f, 0.3f)]
        [TestCase(1.5f, 1f)]
        [TestCase(float.NaN, 0f)]
        public void Constructor_Volume_IsClampedToZeroToOne(float volume, float expected)
        {
            var settings = new SoundSettings(volume, volume, isMuted: false);

            Assert.That(settings.MusicVolume, Is.EqualTo(expected));
            Assert.That(settings.EffectsVolume, Is.EqualTo(expected));
        }

        [Test]
        public void EffectiveVolumes_Muted_AreZeroButChosenVolumesAreKept()
        {
            SoundSettings muted = new SoundSettings(0.4f, 0.6f, isMuted: false).WithMuted(true);

            Assert.That(muted.EffectiveMusicVolume, Is.Zero);
            Assert.That(muted.EffectiveEffectsVolume, Is.Zero);
            Assert.That(muted.WithMuted(false).EffectiveMusicVolume, Is.EqualTo(0.4f));
            Assert.That(muted.WithMuted(false).EffectiveEffectsVolume, Is.EqualTo(0.6f));
        }

        [Test]
        public void With_ChangesOnlyItsOwnValue()
        {
            var settings = new SoundSettings(0.4f, 0.6f, isMuted: false);

            Assert.That(settings.WithMusicVolume(0.1f), Is.EqualTo(new SoundSettings(0.1f, 0.6f, false)));
            Assert.That(settings.WithEffectsVolume(0.2f), Is.EqualTo(new SoundSettings(0.4f, 0.2f, false)));
            Assert.That(settings.WithMuted(true), Is.EqualTo(new SoundSettings(0.4f, 0.6f, true)));
        }
    }
}
