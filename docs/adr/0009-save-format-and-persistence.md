# ADR 0009: Save format and persistence

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
V1 needs one autosave slot plus manual saves; a WebGL build is required ([gameplay-systems.md](../specs/gameplay-systems.md)). Simulation state is plain serializable data ([ADR 0003](0003-simulation-architecture.md)).

## Options considered
1. **Versioned JSON compressed with gzip**: inspectable, diffable in tests, acceptable size; serializer behind an interface.
2. Binary format from the start (e.g. MessagePack): smaller and faster, but opaque and harder to debug.
3. Unity `PlayerPrefs`: unsuitable for large data.

## Decision
- A save is a snapshot of simulation state plus the map seed and a `formatVersion`. Serialization sits behind an interface so a binary format can replace JSON without touching Core.
- Every format change adds an explicit **migration step**; archived old saves are loaded in a CI compatibility test.
- Storage sits behind an `ISaveStore` interface:
  - **Desktop:** files in Unity's `persistentDataPath`; atomic write (temp file then rename); one autosave slot plus manual slots.
  - **WebGL:** `persistentDataPath` maps to the browser's IndexedDB; a platform adapter performs the required explicit filesystem sync after each write.
- Later: **export/import of save files** in the WebGL build, because browser storage is subject to quotas and can be cleared by the user or unavailable in private mode.

## Consequences
- If 512x512 saves prove too large or slow, switch the serializer (new ADR) while keeping the migration chain.
- Save/load round-trip determinism is part of the test suite ([ADR 0007](0007-testing-strategy.md)).
- No map sharing in V1.
