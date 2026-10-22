# ADR 0012: World and map data structures

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
Maps are square grids of 128, 256, or 512 tiles per side ([product-vision.md](../specs/product-vision.md)). The simulation must be deterministic, serializable, testable, and WebGL-friendly ([ADR 0003](0003-simulation-architecture.md)). V2 adds slopes and water; terraforming comes later.

## Options considered
1. **Struct-of-arrays tile storage with id-indexed entities.**
2. One object per tile: simple to read, but heavy in memory and slow to serialize at 262,144 tiles.
3. Unity Tilemap as the source of truth: couples state to Unity, breaking Core independence.

## Decision
Option 1:
- Tile data (terrain type, object id, ownership, road type, station occupancy, and so on) is stored in **flat arrays indexed by tile id** (y * width + x). Roads and stations use dedicated channels so they do not reuse town/industry object IDs.
- Entities (vehicles, stations, industries, towns, routes) live in **id-indexed collections with stable integer ids**; state never holds object references to other entities, only ids. This keeps serialization, commands, and any future network replication simple. V1 road stations (Issue #44) are 1×1 entities whose map channel stores `station.Id + 1` (or `0` when empty). V1 road vehicles (Issue #45) are mutable id-indexed entities referencing station ids for orders and graph node ids for edge travel; they are not stored in a tile channel.
- Core only knows grid coordinates; the **isometric projection is a presentation concern**.
- V2 per-tile height is added as an extra array without redesign.
- Presentation renders in **chunks** (for example 32x32 tiles), updating only visible or dirty chunks; vehicles use pooled sprites. The exact rendering technique (Unity Tilemap versus custom chunk meshes) is decided from WebGL prototype measurements in a later ADR.

## Consequences
- Array layouts need versioning in saves ([ADR 0009](0009-save-format-and-persistence.md)).
- Stable ids require careful deletion/reuse rules (define when the Core is implemented).
