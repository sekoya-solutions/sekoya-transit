# ADR 0027: Deterministic gradient noise

- Status: Accepted
- Date: 2026-10-21
- Deciders: Owner

## Context

Procedural map generation needs a Unity-independent noise primitive whose output is identical across supported runtimes ([ADR 0014](0014-procedural-generation.md)). Core already has a portable Q32.32 fixed-point representation ([ADR 0023](0023-fixed-point-representation.md)). The owner selected fixed-point gradient noise for Issue #27 and approved the sampling contract below.

## Options considered

1. **Fixed-point gradient noise:** smooth two-dimensional fields, deterministic lattice gradients, and no floating-point dependency; requires explicitly specified gradient, hashing, interpolation, and rounding behavior.
2. **Fixed-point value noise:** simpler lattice values and interpolation, but a different spatial character that was not selected.
3. Unity or floating-point noise implementations: rejected for Core because they do not meet the cross-platform determinism constraint.

## Decision

Implement a two-dimensional Perlin-style gradient-noise primitive in Core with the following versioned behavior:

- `Sample(seed, x, y)` accepts a 64-bit seed and Q32.32 coordinates. Coordinates are in lattice units; callers control feature scale by scaling the inputs. This issue does not add octaves or a complete map-generation pipeline.
- Split signed coordinates with arithmetic right shift for the lattice index and the low 32 raw bits for the fractional part. This is mathematical floor decomposition, including for negative coordinates. Lattice points return zero.
- Select one of eight fixed gradients (four cardinal and four diagonal) from a stateless 64-bit hash of the seed and signed lattice coordinates. Coordinate hashing uses the documented axis multipliers and SplitMix64 finalizer constants; it does not advance the simulation PCG stream.
- Use Q32.32 gradient dot products and quintic fade `6t^5 - 15t^4 + 10t^3`, followed by bilinear interpolation. Every fixed-point operation follows ADR 0023's truncation-toward-zero semantics; fade and final output are clamped to `[0, 1]` and `[-1, 1]` respectively.
- Diagonal gradient components use raw Q32.32 value `3037000500`. Golden-answer tests define the exact seed hash, gradient ordering, arithmetic, and sample output.
- Keep the implementation independent of Unity and floating-point arithmetic.

The owner requested validating actual map-generation randomness later, when the noise primitive is integrated into terrain generation. This ADR accepts the reusable mathematical primitive only; it does not assert that generated maps already have an approved visual or gameplay distribution.

## Consequences

- Identical seed and coordinates return an identical bounded result independent of sampling order and runtime.
- The seed hash, gradient table, Q32.32 evaluation order, and output clamps are part of deterministic behavior; changing them requires deliberate golden-vector review.
- Callers choose frequency by scaling coordinates and may layer samples in a future generation system. The noise primitive does not decide terrain composition or balance.
- Validate the resulting spatial appearance, seed variation, and map-level randomness after the primitive is wired into actual map generation.
