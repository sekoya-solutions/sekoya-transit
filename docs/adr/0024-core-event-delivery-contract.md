# ADR 0024: Queued Core event delivery contract

- Status: Accepted
- Date: 2026-10-20
- Deciders: Owner delegated the event-failure policy to the assistant, prioritizing game stability

## Context

[ADR 0003](0003-simulation-architecture.md) requires the deterministic simulation to emit events for presentation observers, without depending on Unity objects or allowing presentation to own simulation state. Issue #23 needs an explicit delivery contract that protects simulation processing from presentation failures while retaining predictable delivery.

## Options considered

1. **Synchronous observer calls during publication.** Simple to understand, but presentation code runs inside simulation command/tick processing and an observer exception can interrupt that processing.
2. **Queue events and let presentation explicitly drain them.** Keeps presentation outside command application and makes delivery order explicit. Subscriber failures still need a policy.
3. **Stop delivery on the first subscriber exception.** Makes failures immediately visible but lets one presentation defect prevent later observers from receiving queued events.
4. **Capture subscriber failures and continue delivery.** Isolates presentation failures from simulation and other observers, provided failures are returned explicitly rather than discarded.

## Decision

Use a Unity-independent, typed event bus with queued delivery. `Publish` only appends an immutable value event; presentation explicitly calls `DispatchPending` after simulation processing. Events are delivered in global publication order, and subscribers of each event run in registration order.

Dispatch processes only events queued when that call begins. Events published by an observer wait for the next dispatch call. Subscribers are snapshotted per event: subscription changes made by an observer do not affect delivery of the current event, but do affect later events. Subscribing or unsubscribing before dispatch takes effect for the pending event.

One observer exception does not stop other observers or events. The dispatch result contains each exception, its event type, and subscription identifier in delivery order; the presentation caller is responsible for reporting these failures. The bus is single-threaded and does not provide synchronization. Event payloads are readonly structs containing immutable values and no simulation-state references. Presentation remains observational and must issue commands for state changes.

The owner delegated the subscriber-failure choice to the assistant, with stability (avoiding avoidable crashes) as the priority. This decision isolates presentation failures without silently swallowing them.

## Consequences

- A presentation observer cannot run inside simulation publication or command application.
- A faulty observer does not prevent other observers or queued events from being processed; callers must inspect and report failures from each dispatch result.
- Dispatch timing and subscription changes are explicit and deterministic.
- The event-bus API and tests can run in both plain .NET and Unity EditMode test runners without Unity dependencies.
- The single-threaded contract is deliberate; any future cross-thread event production requires a separate design decision.
