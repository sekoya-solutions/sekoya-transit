# ADR 0009: Save format and persistence

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
V1 needs one autosave slot plus manual saves; a WebGL build is required ([gameplay-systems.md](../specs/gameplay-systems.md)). Simulation state is plain serializable data ([ADR 0003](0003-simulation-architecture.md)).

## Options considered
1. **Versioned JavaScript Object Notation (JSON) compressed with gzip**: inspectable, diffable in tests, acceptable size; serializer behind an interface.
2. Binary format from the start (e.g. MessagePack): smaller and faster, but opaque and harder to debug.
3. Unity `PlayerPrefs`: unsuitable for large data.

## Decision
- A save is a snapshot of simulation state plus the map seed and a `formatVersion`. Serialization sits behind an interface so a binary format can replace JSON without touching Core.
- Every format change adds an explicit **migration step**; archived old saves are loaded in a CI compatibility test.
- Storage sits behind an `ISaveStore` interface:
  - **Desktop:** files in Unity's `persistentDataPath`; atomic write (temp file then rename); one autosave slot plus manual slots.
  - **WebGL:** `persistentDataPath` maps to the browser's IndexedDB; a platform adapter performs the required explicit filesystem sync after each write.
- Later: **export/import of save files** in the WebGL build, because browser storage is subject to quotas and can be cleared by the user or unavailable in private mode.

## Initial V1 format implementation

On 2026-10-21, the owner selected Unity's `JsonUtility` for the JSON adapter, with serializer-specific code outside Core and Unity EditMode coverage. Core save models and migration behavior remain Unity-independent; the adapter can be replaced without changing Core. The 64-bit world seed and simulation tick are represented as invariant-culture decimal strings in JSON to preserve their exact range and distinguish missing values from zero.

The initial version-1 snapshot covers the state implemented so far: world seed, current simulation tick, map size and all tile channels, ordered town anchors, and ordered industry placements. The calendar date is derived from the tick and configured tick duration, so it is not stored separately. Town population/needs, industry production and inventories, vehicles, economy, and command history are not implemented yet; add their state in later versioned formats with migrations when those systems land. This baseline favors a meaningful round-trip of today's world over an envelope with an opaque payload that cannot yet represent or validate game state.

The storage boundary uses Task-based asynchronous operations. The WebGL adapter must await the browser filesystem's IndexedDB synchronization after mutating save data rather than block the Unity main thread; desktop implementations use the same awaitable contract. Manual slot identifiers are case-insensitive ASCII letters, digits, hyphens, or underscores, normalized to lowercase so desktop filesystems use consistent slot identity.

## Consequences
- If 512x512 saves prove too large or slow, switch the serializer (new ADR) while keeping the migration chain.
- Save/load round-trip determinism is part of the test suite ([ADR 0007](0007-testing-strategy.md)).
- No map sharing in V1.
