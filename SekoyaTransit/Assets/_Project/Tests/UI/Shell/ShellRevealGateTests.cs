#nullable enable
using System;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    public sealed class ShellRevealGateTests
    {
        [Test]
        public void IsLoaded_NoPendingBeforeAllRequested_IsFalse()
        {
            var gate = new ShellRevealGate(timeoutSeconds: 10f);

            Assert.That(gate.IsLoaded(pendingRequests: 0), Is.False);
        }

        [Test]
        public void IsLoaded_AllRequestedWithPending_IsFalse()
        {
            var gate = new ShellRevealGate(timeoutSeconds: 10f);
            gate.MarkAllRequested();

            Assert.That(gate.IsLoaded(pendingRequests: 1), Is.False);
        }

        [Test]
        public void IsLoaded_AllRequestedAndNonePending_IsTrue()
        {
            var gate = new ShellRevealGate(timeoutSeconds: 10f);
            gate.MarkAllRequested();

            Assert.That(gate.IsLoaded(pendingRequests: 0), Is.True);
        }

        [Test]
        public void HasTimedOut_BeforeAnyFrame_IsFalse()
        {
            var gate = new ShellRevealGate(timeoutSeconds: 10f);

            Assert.That(gate.HasTimedOut, Is.False);
        }

        [Test]
        public void HasTimedOut_BlockedFirstFrameLongerThanTheTimeout_IsFalse()
        {
            var gate = new ShellRevealGate(timeoutSeconds: 10f);

            gate.Tick(frameSeconds: 16f);

            Assert.That(gate.HasTimedOut, Is.False);
        }

        [Test]
        public void HasTimedOut_FramesJustShortOfTheTimeout_IsFalse()
        {
            var gate = new ShellRevealGate(timeoutSeconds: 1f);

            gate.Tick(0.25f);
            gate.Tick(0.25f);
            gate.Tick(0.25f);

            Assert.That(gate.HasTimedOut, Is.False);
        }

        [Test]
        public void HasTimedOut_FramesReachingTheTimeoutEvenIfNothingWasRequested_IsTrue()
        {
            var gate = new ShellRevealGate(timeoutSeconds: 1f);

            gate.Tick(0.25f);
            gate.Tick(0.25f);
            gate.Tick(0.25f);
            gate.Tick(0.25f);

            Assert.That(gate.HasTimedOut, Is.True);
        }

        [Test]
        public void HasTimedOut_ManySlowFrames_StillExpires()
        {
            var gate = new ShellRevealGate(timeoutSeconds: 1f);

            for (int frame = 0; frame < 4; frame++)
            {
                gate.Tick(frameSeconds: 5f);
            }

            Assert.That(gate.HasTimedOut, Is.True);
        }

        [Test]
        public void Tick_NegativeFrame_IsIgnored()
        {
            var gate = new ShellRevealGate(timeoutSeconds: 0.25f);

            gate.Tick(frameSeconds: -1f);

            Assert.That(gate.HasTimedOut, Is.False);
        }

        [Test]
        public void Constructor_NonPositiveTimeout_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShellRevealGate(timeoutSeconds: 0f));
        }
    }
}
