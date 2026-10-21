# ADR 0025: Deterministic state hash format

- Status: Accepted
- Date: 2026-10-21
- Deciders: Owner

## Context

[ADR 0007](0007-testing-strategy.md) calls for deterministic command replay tests that compare state hashes. The simulation does not yet have a concrete world-state schema, so Issue #24 must establish a stable hashing contract without introducing a production save format or depending on runtime-specific object hashing.

## Options considered

1. **Secure Hash Algorithm 2 with a 256-bit digest (SHA-256) over explicitly encoded canonical bytes.** Stable across supported runtimes and collision-resistant; hashing is slower than a small non-cryptographic hash, but the Issue #24 use is regression testing rather than per-tick production work.
2. **A lightweight 64-bit non-cryptographic hash.** Faster, but has a substantially smaller output space and would require custom algorithm implementation or another dependency.
3. **Runtime `GetHashCode`, reflection, or automatic object serialization.** Convenient, but field order, implementation, or serialized representation may vary by runtime and version, making results unsuitable as cross-runner determinism evidence.

## Decision

Use SHA-256 over canonical bytes produced by explicit, state-specific snapshot code. The owner approved SHA-256 over explicitly encoded canonical bytes for Issue #24.

The test harness uses a version byte followed by fields in a documented, explicit order. Fixed-width numeric values are encoded little-endian. Variable-length byte or text data, if introduced in a future snapshot, must be length-prefixed; text must use UTF-8. Collections must include a count and use a stable, explicitly defined ordering. Floating-point values and runtime object hashes are excluded. State-specific snapshot code must include every value that can affect future simulation, including the captured pseudo-random number generator state and stream increment.

The replay-test fixture snapshot version 1 writes, in order: schema version (`byte`), current tick (`Int64`), score raw value (`Int64`), completed action count (`Int32`), pseudo-random generator state (`UInt64`), and generator increment (`UInt64`). Its seeded two-command replay has the golden SHA-256 digest `ED6500541379335C70D74B348EAEBF9A3C14134C0BB8D294E4F3D7B600AF1669`.

This contract applies to the determinism test harness and its fixtures. It does not define production world-state serialization or save compatibility.

## Consequences

- Equal canonical state bytes produce equal SHA-256 digests across the plain .NET and Unity test runners.
- A known-answer vector guards the hash implementation, while replay tests compare independently evolved states and include the random-generator state in the snapshot.
- Adding or reordering snapshot fields requires an explicit schema-version change and updated known-answer test.
- SHA-256 cost is acceptable for test snapshots; do not put this diagnostic hashing path into every production tick without profiling and a separate decision.
