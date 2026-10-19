# ADR 0014: Procedural generation

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
Maps are procedurally generated from a seed, with towns and industries placed so production chains are playable ([gameplay-systems.md](../specs/gameplay-systems.md)). The same seed must produce the same map on every platform, including WebGL ([ADR 0003](0003-simulation-architecture.md)).

## Options considered
1. **Generation implemented in Core** as pure functions of (seed, size, settings), with a custom deterministic noise implementation.
2. Unity's `Mathf.PerlinNoise` or `UnityEngine.Random`: not guaranteed identical across platforms or versions, and couples Core to Unity.
3. Third-party noise libraries: possible, but floating-point determinism across IL2CPP/WebGL must still be proven.

## Decision
Option 1:
- Pipeline: terrain (flat in V1, with decorative features) then towns then industries following chain rules (primaries on suitable zones, secondaries and tertiaries at plausible distances).
- **Validation checks** after generation: every town reachable, every chain completable, minimum spacing; regenerate or adjust deterministically on failure.
- Noise uses integer or fixed-point math in Core.
- **Golden-seed tests**: given seeds and sizes produce known map hashes, asserted in CI on all supported runtimes, including a WebGL-targeted check where feasible.

## Consequences
- Changing the generator changes maps for existing seeds; saves store the generated state, not just the seed, so old saves remain valid.
- Generation parameters live in data files ([ADR 0010](0010-data-driven-content-format.md)).
