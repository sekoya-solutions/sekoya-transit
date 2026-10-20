# ADR 0022: PCG32 random number generator

- Status: Accepted
- Date: 2026-10-20
- Deciders: Owner

## Context

The simulation needs a Unity-independent, seeded pseudo-random number generator (PRNG) so identical seeds and operations produce identical results across supported runtimes ([ADR 0003](0003-simulation-architecture.md)). Procedural generation also requires cross-platform repeatability ([ADR 0014](0014-procedural-generation.md)). The generator's state must be capturable for deterministic continuation and save/load.

## Options considered

1. **PCG-XSH-RR 64/32 (PCG32):** a small, well-defined generator with 64-bit internal state, a selectable stream, 32-bit outputs, and a reference seeding procedure.
2. **SplitMix64:** a simpler single-state generator, but without PCG32's stream selector.
3. **xoshiro128\*\*:** a compact generator with 128-bit state, requiring a separate seed-expansion procedure.

## Decision

Use the canonical PCG-XSH-RR 64/32 algorithm, approved by the owner for Issue #21:

- Accept a 64-bit seed and a 64-bit sequence selector. The PCG reference procedure initializes state to zero, derives the odd increment as `(sequence << 1) | 1`, advances once, adds the seed with unsigned wraparound, and advances again.
- Advance state with `state = state * 6364136223846793005 + increment` modulo 2^64. Generate each output using the PCG-XSH-RR 64/32 output transform from the previous state.
- Capture both internal state and increment to restore the exact subsequent sequence.
- Generate bounded integers over `[minInclusive, maxExclusive)` using rejection sampling to avoid modulo bias; reject empty and reversed ranges.
- Keep implementation in pure C# 9 Core code. Golden-vector tests lock the algorithm and seeding behavior.

## Consequences

- This versioned algorithm and seeding contract become deterministic simulation behavior. Changing them can change generated maps and random outcomes; golden-vector failures require deliberate review.
- Separate sequence selectors can provide distinct PCG streams where callers need isolation, but stream allocation remains the responsibility of simulation systems.
- The state is plain unsigned integer data and can be captured by save systems without depending on Unity APIs.
- Statistical quality is not a substitute for cryptographic security; this generator is for simulation only.
