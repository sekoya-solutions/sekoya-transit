# ADR 0003: Deterministic simulation separated from presentation

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
Hard constraints from [product-vision.md](../specs/product-vision.md): future AI opponents (V2) and multiplayer (V3+), maps up to 512x512, WebGL target, robust automated regression tests, save/load, data-driven content.

## Options considered
1. **Pure C# simulation** in its own assembly with no `UnityEngine` dependency; Unity is only the view and input layer.
   - Pros: fast edit-mode unit tests, deterministic, serializable, ready for AI and multiplayer, WebGL-safe.
   - Cons: no ECS (Entity Component System) performance gains; must design data layouts carefully and measure.
2. Unity Data-Oriented Technology Stack (DOTS/ECS): high performance, but weak WebGL support (no multithreading), steep complexity, harder to unit test.
3. Classic GameObjects/MonoBehaviours holding logic: quick to start, but non-deterministic, hard to test, and hard to extend to AI or multiplayer.

## Decision
Option 1. The simulation:
- Runs on a **fixed tick**, independent of frame rate; game speed is the number of ticks per second.
- Uses **integer or fixed-point math** (no floating point in simulation state) and a **seeded random number generator** owned by the simulation, so the same seed and commands give identical results.
- Mutates state **only through commands** (e.g. BuildRoad, BuyVehicle, TakeLoan) validated and applied by the simulation. Players, AI, and (later) network peers all issue the same commands.
- Emits **events** that the presentation layer observes; it never reads Unity objects.
- Keeps state in **serializable plain data**, which also underpins save/load.
- Is **data-driven**: cargo, industries, vehicles, and needs are defined in data files loaded at start.

## Consequences
- Presentation (sprites, UI, audio) interpolates between ticks and must not own game state.
- Determinism needs tests (replay a command log and compare state hashes) in continuous integration.
- Performance on large maps must be profiled early; optimize with struct arrays and spatial indexing if needed.
- Save format and WebGL persistence will be addressed in a follow-up ADR.
