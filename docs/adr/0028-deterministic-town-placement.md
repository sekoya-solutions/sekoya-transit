# ADR 0028: Deterministic town placement

- Status: Accepted
- Date: 2026-10-21
- Deciders: Owner

## Context

Procedural generation places towns after terrain and before industries ([ADR 0014](0014-procedural-generation.md)). Core uses zero-based tile coordinates and stable row-major IDs ([ADR 0012](0012-world-and-map-data-structures.md)). Town placement must be reproducible while leaving town populations, buildings, and presentation out of this focused step.

## Options considered

1. **Seeded PCG32 candidate sampling:** shuffle map tiles deterministically and accept anchors satisfying configurable separation. It is simple, independent of presentation, and keeps town placement random without requiring the noise field.
2. **Noise-ranked candidate placement:** integrates gradient noise immediately, but couples town distribution to terrain-noise tuning before map-level randomness has been reviewed.
3. **Hand-authored fixed coordinates:** simple but not suitable for seeded procedural maps.

## Decision

Use the seeded candidate sampler selected by the owner:

- Each town is represented by one zero-based tile anchor. Since [Issue #219](https://github.com/sekoya-solutions/sekoya-transit/issues/219) the anchor is the lowest-X, lowest-Y tile of the town's 2x2 block (`SiteFootprint`): only anchors whose whole block fits in the map are candidates, and the generator tags all four tiles with the town's object ID. Population and needs are out of scope here.
- `TownPlacementSettings` supplies supported map size, requested town count, and minimum Chebyshev distance in tiles. These are data-driven inputs; no balance defaults are imposed.
- Initialize all tile IDs in row-major order, shuffle with the canonical PCG-XSH-RR 64/32 generator using a fixed town-placement stream selector, then scan the shuffled IDs. Accept a candidate only if its Chebyshev distance from each nearby accepted anchor is at least the configured minimum.
- Return exactly the requested number of positions in deterministic selection order. Reject invalid settings, pre-reject counts above the theoretical spacing capacity, and throw explicitly if the deterministic candidate order cannot fulfill the request.
- Noise is not used for town placement in this step. Evaluate the noise field's map-level visual and gameplay distribution later when actual map generation is integrated.

## Consequences

- The same map size, requested count, minimum spacing, and seed produce the same ordered town anchors on every supported runtime.
- The spacing and requested count are configurable, while each result is independent of Unity presentation.
- Greedy randomized placement may reject a configuration when its deterministic candidate order cannot place the requested count even if another arrangement exists; failure is explicit rather than returning a partial success.
- The town placement PRNG sequence and acceptance order are part of deterministic generation behavior and are locked by a fixed known-answer test.
