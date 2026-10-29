#nullable enable
using System;
using System.Globalization;
using NUnit.Framework;
using SekoyaTransit.UI.Shell;

namespace SekoyaTransit.UI.Tests.Shell
{
    [TestFixture]
    public sealed class ToastQueueTests
    {
        private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("en");

        [Test]
        public void Enqueue_ShowsToastUntilItsLifetimeEnds()
        {
            var queue = new ToastQueue(lifetimeSeconds: 5f, maxVisible: 3);

            queue.Enqueue(Message("a"), nowSeconds: 10f);

            Assert.That(queue.Count, Is.EqualTo(1));
            Assert.That(queue.Expire(14.9f), Is.False);
            Assert.That(queue.Count, Is.EqualTo(1));
            Assert.That(queue.Expire(15f), Is.True);
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void Enqueue_BeyondTheVisibleLimit_DropsTheOldest()
        {
            var queue = new ToastQueue(lifetimeSeconds: 5f, maxVisible: 2);

            queue.Enqueue(Message("a"), 0f);
            queue.Enqueue(Message("b"), 0f);
            queue.Enqueue(Message("c"), 0f);

            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue[0].TemplateKey, Is.EqualTo("b"));
            Assert.That(queue[1].TemplateKey, Is.EqualTo("c"));
        }

        [Test]
        public void Expire_OnlyRemovesToastsPastTheirOwnDeadline()
        {
            var queue = new ToastQueue(lifetimeSeconds: 5f, maxVisible: 4);
            queue.Enqueue(Message("old"), 0f);
            queue.Enqueue(Message("new"), 3f);

            queue.Expire(5f);

            Assert.That(queue.Count, Is.EqualTo(1));
            Assert.That(queue[0].TemplateKey, Is.EqualTo("new"));
        }

        [Test]
        public void Version_ChangesWhenToastsAreAddedOrRemoved()
        {
            var queue = new ToastQueue(lifetimeSeconds: 5f, maxVisible: 4);
            int initial = queue.Version;

            queue.Enqueue(Message("a"), 0f);
            int afterAdd = queue.Version;
            queue.Expire(1f);
            int afterNoop = queue.Version;
            queue.Expire(5f);

            Assert.That(afterAdd, Is.Not.EqualTo(initial));
            Assert.That(afterNoop, Is.EqualTo(afterAdd));
            Assert.That(queue.Version, Is.Not.EqualTo(afterAdd));
        }

        [Test]
        public void PlaceholderDefaults_ArePositive()
        {
            var queue = new ToastQueue();

            Assert.That(ToastQueue.PlaceholderLifetimeSeconds, Is.GreaterThan(0f));
            Assert.That(ToastQueue.PlaceholderMaxVisible, Is.GreaterThan(0));
            Assert.That(queue.Count, Is.Zero);
        }

        [Test]
        public void Constructor_InvalidSettings_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ToastQueue(0f, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ToastQueue(5f, 0));
        }

        [Test]
        public void Format_SubstitutesTextKeysAndAmounts()
        {
            var text = new ShellTextCache();
            text.Set("toast.test", "Deliver {1} {0} to {2} for {3}");
            text.Set("industry.sawmill", "Sawmill");
            var message = new ToastMessage(
                "toast.test",
                new[]
                {
                    ToastArgument.FromText("wood"),
                    ToastArgument.FromAmount(1200L),
                    ToastArgument.FromKey("industry.sawmill"),
                    ToastArgument.FromAmount(45000L)
                });

            Assert.That(message.Format(text, _culture), Is.EqualTo("Deliver 1,200 wood to Sawmill for 45,000"));
        }

        [Test]
        public void Format_MissingTemplate_FallsBackToTheKey()
        {
            var message = new ToastMessage("toast.missing", Array.Empty<ToastArgument>());

            Assert.That(message.Format(new ShellTextCache(), _culture), Is.EqualTo("toast.missing"));
        }

        private static ToastMessage Message(string key)
        {
            return new ToastMessage(key, Array.Empty<ToastArgument>());
        }
    }
}
