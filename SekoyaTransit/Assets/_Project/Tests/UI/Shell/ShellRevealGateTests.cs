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
            var gate = new ShellRevealGate(startSeconds: 0f, timeoutSeconds: 10f);

            Assert.That(gate.IsLoaded(pendingRequests: 0), Is.False);
        }

        [Test]
        public void IsLoaded_AllRequestedWithPending_IsFalse()
        {
            var gate = new ShellRevealGate(startSeconds: 0f, timeoutSeconds: 10f);
            gate.MarkAllRequested();

            Assert.That(gate.IsLoaded(pendingRequests: 1), Is.False);
        }

        [Test]
        public void IsLoaded_AllRequestedAndNonePending_IsTrue()
        {
            var gate = new ShellRevealGate(startSeconds: 0f, timeoutSeconds: 10f);
            gate.MarkAllRequested();

            Assert.That(gate.IsLoaded(pendingRequests: 0), Is.True);
        }

        [Test]
        public void HasTimedOut_BeforeTheDeadline_IsFalse()
        {
            var gate = new ShellRevealGate(startSeconds: 5f, timeoutSeconds: 10f);

            Assert.That(gate.HasTimedOut(nowSeconds: 14.9f), Is.False);
        }

        [Test]
        public void HasTimedOut_AtTheDeadlineEvenIfNothingWasRequested_IsTrue()
        {
            var gate = new ShellRevealGate(startSeconds: 5f, timeoutSeconds: 10f);

            Assert.That(gate.HasTimedOut(nowSeconds: 15f), Is.True);
        }

        [Test]
        public void Constructor_NonPositiveTimeout_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShellRevealGate(startSeconds: 0f, timeoutSeconds: 0f));
        }
    }
}
