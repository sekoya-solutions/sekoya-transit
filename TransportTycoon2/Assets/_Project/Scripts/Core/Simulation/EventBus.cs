#nullable enable
using System;
using System.Collections.Generic;

namespace TransportTycoon.Core.Simulation
{
    /// <summary>
    /// Marks a value as an event emitted by the simulation for observers.
    /// Implementations should be readonly structs containing only immutable values.
    /// </summary>
    public interface ISimulationEvent
    {
    }

    /// <summary>
    /// Queues simulation events and delivers them to typed subscribers on explicit dispatch.
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, List<ISubscription>> _subscriptions = new Dictionary<Type, List<ISubscription>>();
        private readonly Queue<ISimulationEvent> _pendingEvents = new Queue<ISimulationEvent>();
        private long _nextSubscriptionId;
        private bool _isDispatching;

        /// <summary>
        /// Registers an observer. Disposing the returned token removes that subscription.
        /// </summary>
        /// <typeparam name="TEvent">The event type to observe.</typeparam>
        /// <param name="handler">The observer to invoke when an event is dispatched.</param>
        /// <returns>A token that removes this subscription when disposed.</returns>
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
            where TEvent : struct, ISimulationEvent
        {
#pragma warning disable CA1510 // Unity's Core API cannot rely on the newer ThrowIfNull helper.
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
#pragma warning restore CA1510

            long subscriptionId = checked(_nextSubscriptionId + 1);
            _nextSubscriptionId = subscriptionId;

            var subscription = new EventSubscription<TEvent>(this, subscriptionId, handler);
            Type eventType = typeof(TEvent);
            if (!_subscriptions.TryGetValue(eventType, out List<ISubscription>? eventSubscriptions))
            {
                eventSubscriptions = new List<ISubscription>();
                _subscriptions.Add(eventType, eventSubscriptions);
            }

            eventSubscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// Adds an event to the delivery queue without invoking observers.
        /// </summary>
        /// <typeparam name="TEvent">The event type to publish.</typeparam>
        /// <param name="simulationEvent">The immutable event value.</param>
        public void Publish<TEvent>(TEvent simulationEvent)
            where TEvent : struct, ISimulationEvent
        {
            _pendingEvents.Enqueue(simulationEvent);
        }

        /// <summary>
        /// Delivers the events that were pending when this call began.
        /// Events published by handlers remain queued for the next call.
        /// Observer failures are returned in delivery order; they do not stop other observers.
        /// </summary>
        /// <returns>The number of events processed and any observer failures.</returns>
        public EventDispatchResult DispatchPending()
        {
            if (_isDispatching)
            {
                throw new InvalidOperationException("Event dispatch cannot be nested.");
            }

            _isDispatching = true;
            try
            {
                int eventsToDispatch = _pendingEvents.Count;
                if (eventsToDispatch == 0)
                {
                    // Most ticks publish nothing; a shared empty result keeps them allocation-free (Issue #70).
                    return EventDispatchResult.Empty;
                }

                var failures = new List<EventDispatchFailure>();

                for (int eventIndex = 0; eventIndex < eventsToDispatch; eventIndex++)
                {
                    ISimulationEvent simulationEvent = _pendingEvents.Dequeue();
                    Type eventType = simulationEvent.GetType();
                    if (!_subscriptions.TryGetValue(eventType, out List<ISubscription>? eventSubscriptions))
                    {
                        continue;
                    }

                    ISubscription[] subscribers = eventSubscriptions.ToArray();
                    foreach (ISubscription subscriber in subscribers)
                    {
                        try
                        {
                            subscriber.Invoke(simulationEvent);
                        }
                        catch (Exception exception)
                        {
                            failures.Add(new EventDispatchFailure(eventType, subscriber.Id, exception));
                        }
                    }
                }

                return new EventDispatchResult(eventsToDispatch, failures);
            }
            finally
            {
                _isDispatching = false;
            }
        }

        private void Unsubscribe(ISubscription subscription)
        {
            if (!_subscriptions.TryGetValue(subscription.EventType, out List<ISubscription>? eventSubscriptions))
            {
                return;
            }

            eventSubscriptions.Remove(subscription);
            if (eventSubscriptions.Count == 0)
            {
                _subscriptions.Remove(subscription.EventType);
            }
        }

        private interface ISubscription
        {
            Type EventType { get; }

            long Id { get; }

            void Invoke(ISimulationEvent simulationEvent);
        }

        private sealed class EventSubscription<TEvent> : IDisposable, ISubscription
            where TEvent : struct, ISimulationEvent
        {
            private EventBus? _owner;
            private readonly Action<TEvent> _handler;

            public EventSubscription(EventBus owner, long id, Action<TEvent> handler)
            {
                _owner = owner;
                Id = id;
                _handler = handler;
            }

            public Type EventType
            {
                get
                {
                    return typeof(TEvent);
                }
            }

            public long Id { get; }

            public void Invoke(ISimulationEvent simulationEvent)
            {
                _handler((TEvent)simulationEvent);
            }

            public void Dispose()
            {
                if (_owner != null)
                {
                    _owner.Unsubscribe(this);
                    _owner = null;
                }
            }
        }
    }

    /// <summary>
    /// Describes an observer exception captured during event delivery.
    /// </summary>
    public sealed class EventDispatchFailure
    {
        internal EventDispatchFailure(Type eventType, long subscriptionId, Exception exception)
        {
            EventType = eventType;
            SubscriptionId = subscriptionId;
            Exception = exception;
        }

        /// <summary>
        /// Gets the type of event being delivered.
        /// </summary>
        public Type EventType { get; }

        /// <summary>
        /// Gets the stable identifier of the failing subscription.
        /// </summary>
        public long SubscriptionId { get; }

        /// <summary>
        /// Gets the exception thrown by the observer.
        /// </summary>
        public Exception Exception { get; }
    }

    /// <summary>
    /// Contains the outcome of one event-queue dispatch.
    /// </summary>
    public sealed class EventDispatchResult
    {
        /// <summary>
        /// The result of a dispatch that found no pending events.
        /// </summary>
        internal static readonly EventDispatchResult Empty =
            new EventDispatchResult(0, new List<EventDispatchFailure>());

        internal EventDispatchResult(int eventsProcessed, List<EventDispatchFailure> failures)
        {
            EventsProcessed = eventsProcessed;
            Failures = failures.AsReadOnly();
        }

        /// <summary>
        /// Gets the number of queued events processed, including events without subscribers.
        /// </summary>
        public int EventsProcessed { get; }

        /// <summary>
        /// Gets observer failures in deterministic delivery order. Callers should report them.
        /// </summary>
        public IReadOnlyList<EventDispatchFailure> Failures { get; }
    }
}
