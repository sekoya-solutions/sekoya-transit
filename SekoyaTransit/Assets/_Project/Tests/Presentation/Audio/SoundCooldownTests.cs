#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.Presentation.Audio;

namespace SekoyaTransit.Presentation.Tests.Audio
{
    public sealed class SoundCooldownTests
    {
        [Test]
        public void TryStart_FirstTrigger_Starts()
        {
            var cooldown = new SoundCooldown(0.5f);

            Assert.That(cooldown.TryStart(10f), Is.True);
        }

        [Test]
        public void TryStart_WithinGap_IsSilentAndDoesNotExtendTheGap()
        {
            var cooldown = new SoundCooldown(0.5f);
            Assert.That(cooldown.TryStart(10f), Is.True);

            Assert.That(cooldown.TryStart(10.2f), Is.False);
            Assert.That(cooldown.TryStart(10.4f), Is.False);
            Assert.That(cooldown.TryStart(10.5f), Is.True);
        }

        [Test]
        public void TryStart_ZeroGap_StartsEveryTrigger()
        {
            var cooldown = new SoundCooldown(0f);

            Assert.That(cooldown.TryStart(1f), Is.True);
            Assert.That(cooldown.TryStart(1f), Is.True);
        }

        [Test]
        public void Constructor_NegativeOrNaNGap_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SoundCooldown(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new SoundCooldown(float.NaN));
        }
    }
}
