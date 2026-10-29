#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SekoyaTransit.Core.Simulation;

namespace SekoyaTransit.Core.Tests.Simulation
{
    [TestFixture]
    public sealed class EventBusTests
    {
        private static readonly string[] _expectedPublishedOrder = { "first:1", "second:2", "first:3" };
        private static readonly int[] _expectedSubscriberOrder = { 1, 2 };
        private static readonly string[] _expectedSubscriptionSnapshot = { "first", "second", "first", "third" };
        private static readonly int[] _expectedReceivedValues = { 1, 2 };
        private static readonly int[] _expectedFirstDispatchValues = { 1 };
        private static readonly int[] _expectedBothDispatchValues = { 1, 2 };

        [Test]
        public void Publish_QueuesEventsUntilDispatchAndPreservesGlobalOrder()
        {
            var eventBus = new EventBus();
            var received = new List<string>();
            eventBus.Subscribe<FirstEvent>(simulationEvent => received.Add($"first:{simulationEvent.Value}"));
            eventBus.Subscribe<SecondEvent>(simulationEvent => received.Add($"second:{simulationEvent.Value}"));

            eventBus.Publish(new FirstEvent(1));
            eventBus.Publish(new SecondEvent(2));
            eventBus.Publish(new FirstEvent(3));

            Assert.That(received, Is.Empty);
            EventDispatchResult result = eventBus.DispatchPending();

            CollectionAssert.AreEqual(_expectedPublishedOrder, received);
            Assert.That(result.EventsProcessed, Is.EqualTo(3));
            Assert.That(result.Failures, Is.Empty);
        }

        [Test]
        public void DispatchPending_InvokesSubscribersInRegistrationOrder()
        {
            var eventBus = new EventBus();
            var received = new List<int>();
            eventBus.Subscribe<FirstEvent>(_ => received.Add(1));
            eventBus.Subscribe<FirstEvent>(_ => received.Add(2));

            eventBus.Publish(new FirstEvent(10));
            eventBus.DispatchPending();

            CollectionAssert.AreEqual(_expectedSubscriberOrder, received);
        }

        [Test]
        public void Subscription_DisposeStopsReceivingSubsequentEvents()
        {
            var eventBus = new EventBus();
            int received = 0;
            IDisposable subscription = eventBus.Subscribe<FirstEvent>(_ => received++);

            eventBus.Publish(new FirstEvent(1));
            subscription.Dispose();
            subscription.Dispose();
            eventBus.DispatchPending();

            eventBus.Publish(new FirstEvent(2));
            eventBus.DispatchPending();

            Assert.That(received, Is.Zero);
        }

        [Test]
        public void DispatchPending_UsesSubscriberSnapshotForCurrentEvent()
        {
            var eventBus = new EventBus();
            var received = new List<string>();
            IDisposable? secondSubscription = null;
            bool subscribedThird = false;

            eventBus.Subscribe<FirstEvent>(_ =>
            {
                received.Add("first");
                secondSubscription!.Dispose();
                if (!subscribedThird)
                {
                    subscribedThird = true;
                    eventBus.Subscribe<FirstEvent>(_ => received.Add("third"));
                }
            });
            secondSubscription = eventBus.Subscribe<FirstEvent>(_ => received.Add("second"));

            eventBus.Publish(new FirstEvent(1));
            eventBus.Publish(new FirstEvent(2));
            eventBus.DispatchPending();

            CollectionAssert.AreEqual(
                _expectedSubscriptionSnapshot,
                received);
        }

        [Test]
        public void DispatchPending_DefersEventsPublishedByHandlersUntilNextCall()
        {
            var eventBus = new EventBus();
            var received = new List<int>();
            eventBus.Subscribe<FirstEvent>(simulationEvent =>
            {
                received.Add(simulationEvent.Value);
                if (simulationEvent.Value == 1)
                {
                    eventBus.Publish(new FirstEvent(2));
                }
            });

            eventBus.Publish(new FirstEvent(1));
            EventDispatchResult firstResult = eventBus.DispatchPending();

            Assert.That(firstResult.EventsProcessed, Is.EqualTo(1));
            CollectionAssert.AreEqual(_expectedFirstDispatchValues, received);

            EventDispatchResult secondResult = eventBus.DispatchPending();

            Assert.That(secondResult.EventsProcessed, Is.EqualTo(1));
            CollectionAssert.AreEqual(_expectedBothDispatchValues, received);
        }

        [Test]
        public void DispatchPending_ReportsObserverFailuresAndContinuesDelivery()
        {
            var eventBus = new EventBus();
            var received = new List<int>();
            var expectedException = new InvalidOperationException("observer failure");
            eventBus.Subscribe<FirstEvent>(_ => throw expectedException);
            eventBus.Subscribe<FirstEvent>(simulationEvent => received.Add(simulationEvent.Value));

            eventBus.Publish(new FirstEvent(1));
            eventBus.Publish(new FirstEvent(2));
            EventDispatchResult result = eventBus.DispatchPending();

            Assert.That(result.EventsProcessed, Is.EqualTo(2));
            Assert.That(result.Failures, Has.Count.EqualTo(2));
            Assert.That(result.Failures[0].EventType, Is.EqualTo(typeof(FirstEvent)));
            Assert.That(result.Failures[0].Exception, Is.SameAs(expectedException));
            Assert.That(result.Failures[0].SubscriptionId, Is.EqualTo(result.Failures[1].SubscriptionId));
            CollectionAssert.AreEqual(_expectedReceivedValues, received);
        }

        [Test]
        public void Subscribe_RejectsNullHandler()
        {
            var eventBus = new EventBus();

            Assert.That(
                () => eventBus.Subscribe<FirstEvent>(null!),
                Throws.TypeOf<ArgumentNullException>());
        }

        private readonly struct FirstEvent : ISimulationEvent
        {
            public FirstEvent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private readonly struct SecondEvent : ISimulationEvent
        {
            public SecondEvent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}
