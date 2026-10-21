# ADR 0026: Simulation calendar

- Status: Accepted
- Date: 2026-10-21
- Deciders: Owner

## Context

The game starts in 1900 and requires an in-game year to take about 30 real minutes at normal speed. Calendar progression must be deterministic, independent of rendering frame rate and wall-clock time, and driven by the fixed-tick simulation loop ([ADR 0003](0003-simulation-architecture.md)).

## Options considered

1. **Fixed 365-day years with no leap days.** Provides a constant, easy-to-understand year duration and avoids special leap-year rules.
2. **Gregorian calendar with leap years.** Familiar dates, but year length varies and therefore changes real-time pacing unless the day length also varies.
3. **Advance the date from wall-clock elapsed time.** Simple to connect to real time, but introduces frame batching and pause risks into deterministic state.

## Decision

Use an owner-approved fixed calendar starting at 1900-01-01. Each year has 365 days; February always has 28 days, and there are no leap days. There are 1,800 seconds of normal-speed simulation time per game year, distributed uniformly over 365 days.

The calendar derives its displayed whole-day date directly from the simulation tick and the configured normal-speed tick duration. Tick zero is 1900-01-01. For tick index `t`, tick duration `d` in `TimeSpan` ticks, and year duration `Y = 1,800 * TimeSpan.TicksPerSecond`, the elapsed whole days are `floor(t * d * 365 / Y)`. Sub-day progress is retained implicitly by deriving the date from the absolute tick rather than rounding and accumulating per-frame updates. Integer/decimal arithmetic is used; floating-point and wall-clock time are not used.

The owner approved fixed 365-day years and deriving calendar time from the simulation loop's tick duration. `SimulationLoop.TickDuration` exposes the configured normal-speed duration so composition can configure the calendar consistently. Pausing stops tick advancement; speed multipliers affect calendar progression through the accelerated tick count.

## Consequences

- Calendar state is reproducible from the tick and tick duration and can be tested independently of Unity.
- Different elapsed-time batching that produces the same tick produces the same date; no per-frame calendar drift occurs.
- Dates are shown at whole-day resolution. The configured tick duration can quantize when a day boundary is crossed by less than one simulation tick.
- The fixed calendar intentionally differs from the Gregorian calendar and has no leap days.
- Dates beyond the `int` year range and arithmetic overflow are surfaced as `OverflowException`.
- The 30-minute annual pacing is an approved starting value and remains tunable through the simulation tick-duration configuration and future playtesting.
