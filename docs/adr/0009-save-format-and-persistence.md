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

The initial version-1 snapshot covers the state implemented so far: world seed, current simulation tick, map size and all tile channels then present (terrain, object, owner), ordered town anchors, and ordered industry placements. The calendar date is derived from the tick and configured tick duration, so it is not stored separately. Town population/needs, industry production and inventories, vehicles, economy, and command history are not implemented yet; add their state in later versioned formats with migrations when those systems land. This baseline favors a meaningful round-trip of today's world over an envelope with an opaque payload that cannot yet represent or validate game state.

**Format version 2** (Issue #41) adds a dedicated per-tile road type channel (`0` = no road, `1` = basic road) so roads do not collide with town/industry object IDs. An explicit `SaveFormatV1ToV2RoadChannelMigration` fills empty roads when loading archived version-1 saves.

**Format version 3** (Issue #44) adds an ordered road-station list `(id, x, y)`. An explicit `SaveFormatV2ToV3RoadStationsMigration` supplies an empty station list when loading version-2 saves. The runtime `TileMap` station occupancy channel is derived from that list when stations are placed in the live world; it is not a separate serialized tile array.

**Format version 4** (Issue #45) adds an ordered road-vehicle list `(id, typeId, orderStationIds, orderIndex, isStuck, isOnEdge, edgeFromNodeId, edgeToNodeId, progressAlongEdgeRaw)`. An explicit `SaveFormatV3ToV4RoadVehiclesMigration` supplies an empty vehicle list when loading version-3 saves.

**Format version 5** (Issue #46) adds station and vehicle cargo stacks, per-order `waitForFull` flags, vehicle cargo capacity, and in-progress service flags (`isServicingStop`, `isUnloadComplete`). An explicit `SaveFormatV4ToV5CargoMigration` bumps version-4 saves (empty cargo and wait-for-full false). Issue #47 keeps format version 5: station capacity and decay are runtime rules; over-capacity station cargo is trimmed when stations are constructed or restored.

**Format version 6** (Issue #48) adds company cash (invariant-culture decimal string in JSON) and per-industry cargo stacks on placed industries. An explicit `SaveFormatV5ToV6EconomyProductionMigration` sets cash to `GeneratedWorld.PlaceholderStartingCash` and leaves industry cargo empty when loading version-5 saves.

**Format version 7** (Issue #51) adds per-vehicle lifetime delivery income (invariant-culture decimal string in JSON on each road vehicle). An explicit `SaveFormatV6ToV7VehicleLifetimeIncomeMigration` bumps version-6 saves; missing income fields deserialize as zero.

**Format version 8** (Issue #52) adds per-industry calendar-month produced and transported counters plus the year/month those counters apply to (`monthProduced` / `monthTransported` as invariant-culture decimal strings; `statsYear` / `statsMonth` as integers, with `0`/`0` meaning unset until production runs). An explicit `SaveFormatV7ToV8IndustryMonthStatsMigration` bumps version-7 saves; missing fields deserialize as zero.

**Format version 9** (Issue #132) drops station cargo (stations are stops only). Industry **Transported** means units loaded onto vehicles. Persists per-industry `loadRoundRobinIndex` for fair multi-vehicle loading. An explicit `SaveFormatV8ToV9StationsStopsOnlyMigration` bumps version-8 saves (station cargo ignored on load; round-robin cursor defaults to 0).

**Format version 10** (Issue #53) adds a dedicated per-tile rail type channel (`0` = no rail, `1` = basic track) parallel to the road channel so roads and rails can coexist on one tile. An explicit `SaveFormatV9ToV10RailChannelMigration` fills empty rails when loading version-9 saves.

**Format version 11** (Issue #54) adds ordered rail-station and rail-vehicle lists (including cargo, orders with wait-for-full, service flags, and lifetime delivery income), parallel to road stations/vehicles. Rail-station tile occupancy uses a dedicated TileMap channel (separate from the road station channel; encoded as `id + 1`, sentinel `0`) restored from the entity list. An explicit `SaveFormatV10ToV11RailStationsVehiclesMigration` supplies empty lists when loading version-10 saves.

**Format version 12** (Issue #56) persists per-rail-vehicle `wagonCount`. Cargo capacity remains `wagonCount × capacityPerWagon` (placeholder 10). An explicit `SaveFormatV11ToV12RailWagonCountMigration` sets `wagonCount` from capacity when loading version-11 saves (`capacity / 10`, treating the previous fixed 30-capacity trains as 3 wagons).

**Format version 13** (Issue #57) persists per-town `population` and ordered freight `needs` (cargo ids) alongside town anchors. An explicit `SaveFormatV12ToV13TownPopulationMigration` assigns placeholder population `100` and empty needs when loading version-12 saves.

**Format version 14** (Issue #58) persists per-need `monthSupply` plus town `statsYear` / `statsMonth` for monthly supply meters and growth. An explicit `SaveFormatV13ToV14TownSupplyGrowthMigration` bumps version-13 saves (supply and stats default to zero/unset). Vehicle allowed-load cargo lists are not persisted; they are recomputed from orders after load.

**Format version 15** (Issue #59) persists town passenger waiting cargo stacks plus `loadRoundRobinIndex`, and per-road/rail-vehicle `passengerCargoOriginTownId` (−1 unset). An explicit `SaveFormatV14ToV15PassengerMailMigration` bumps version-14 saves (empty waiting bags, unset origins).

**Format version 16** (Issue #60) persists town calendar-month `monthPassengersGenerated` / `monthPassengersTransported` / `monthMailGenerated` / `monthMailTransported` counters for the towns list UI. An explicit `SaveFormatV15ToV16TownPassengerMonthStatsMigration` bumps version-15 saves (counters default to zero). The current format version constant lives on `SaveSnapshot`; production code builds the pipeline via `CurrentSaveMigrations.CreatePipeline()`.

The storage boundary uses Task-based asynchronous operations. The WebGL adapter must await the browser filesystem's IndexedDB synchronization after mutating save data rather than block the Unity main thread; desktop implementations use the same awaitable contract. Manual slot identifiers are case-insensitive ASCII letters, digits, hyphens, or underscores, normalized to lowercase so desktop filesystems use consistent slot identity.

## Archived compatibility baseline

Issue #36 adds `TransportTycoon2/Assets/_Project/Tests/Save/Fixtures/Issue34V1.bytes`, a frozen gzip-compressed JSON version-1 fixture representing a small map with seed `1234567890123456789`, tick `987654321`, a town, a forest industry, and a marked terrain/ownership tile. Its SHA-256 is `3AD53B47BFB68A615702C50513AF70CFA240DFE19A88C061ABD51A30AE13DB37`. The Unity EditMode compatibility test reads these archived bytes directly and loads them through `JsonGzipSaveSerializer` and the current migration pipeline; it does not generate the fixture during the test. After format version 16, that fixture must migrate to the current version with an empty road channel (all `NoRoadTypeId`), an empty rail channel (all `NoRailTypeId`), an empty road-station list (no station cargo), an empty road-vehicle list, an empty rail-station list, an empty rail-vehicle list, empty vehicle cargo inventories, placeholder starting company cash, empty industry cargo, zero lifetime delivery income on any vehicles, zero/unset industry month produced/transported stats, industry load round-robin cursors at zero, rail vehicles (if any) carrying explicit `wagonCount`, towns carrying placeholder population `100` with empty needs, zero need supply, unset town stats month, empty passenger waiting cargo, town load round-robin at zero, zero town passenger/mail month generated/transported counters, and vehicle passenger-origin town ids unset (−1).

This is the first compatibility baseline because no earlier production format or released player saves exist. Create future archive fixtures from the last supported save format before changing that format, preserve prior fixtures in the suite, and update fixture provenance plus expected-state assertions in the same review. Do not invent older formats or add migrations without a real format change.

## Consequences
- If 512x512 saves prove too large or slow, switch the serializer (new ADR) while keeping the migration chain.
- Save/load round-trip determinism is part of the test suite ([ADR 0007](0007-testing-strategy.md)).
- No map sharing in V1.
