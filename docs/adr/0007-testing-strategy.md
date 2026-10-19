# ADR 0007: Testing strategy

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
Robust continuous regression testing is an explicit portfolio goal ([product-vision.md](../specs/product-vision.md)). The simulation is deterministic and Unity-independent ([ADR 0003](0003-simulation-architecture.md)).

## Options considered
1. **Dual runner**: Core tests run both in Unity (Unity Test Framework, NUnit) and in a plain .NET test project compiling the same Core sources (`dotnet test`).
2. Unity Test Framework only: single runner, but every run requires launching the Unity editor and a license, which is slow locally and in CI.

## Decision
Option 1, with these test levels:
- **Unit tests** for Core rules (economy, production chains, towns, vehicles, loans).
- **Determinism tests**: replay a recorded command log with a fixed seed and compare state hashes; also verify save/load round-trips.
- **Property-style tests** for invariants (e.g. cargo is never created or destroyed unexpectedly, money is conserved by transactions).
- **Play Mode tests** (few) for presentation and UI wiring.
- **Headless smoke test**: generate a map and run N ticks.
- **Performance test**: tick time on a 512x512 map tracked against a budget in CI.
- Code coverage reported for Core; about 80% is guidance, not an initial gate.

The asmdef (Assembly Definition) setup remains the source of truth for Unity; the plain .NET project links the same source files.

## Consequences
- Core must stay free of `UnityEngine` references (enforced by `noEngineReferences` on its asmdef and by the plain .NET build).
- A small amount of project-file maintenance for the .NET test project.
- Tests are written alongside features; a feature is not done without tests.
