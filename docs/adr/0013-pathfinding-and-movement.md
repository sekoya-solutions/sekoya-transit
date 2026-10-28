# ADR 0013: Pathfinding and movement

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
V1 vehicles follow built roads or rails between stations according to orders, with no collisions, signals, or traffic ([gameplay-systems.md](../specs/gameplay-systems.md)). V2 adds signals, collisions, and slopes. Pathfinding must be deterministic and cheap on maps up to 512x512.

## Options considered
1. **Transport network graph + A\* with cached routes.**
2. Grid-based pathfinding on tiles for every request: simple but wasteful and ignores segment lengths.
3. Unity NavMesh or third-party navigation: tied to Unity, non-deterministic, breaks Core independence.

## Decision
Option 1:
- Roads and rails are modeled as **graphs** (nodes: junctions, endpoints, stations; edges: segments with length), one network layer per transport mode, derived from placed tiles and **updated incrementally** when infrastructure changes.
- Vehicles route with **A\*** over the graph; routes between order stops are **cached** and invalidated when the graph changes.
- Movement advances along edges in **fixed-point units per tick**.
- A* uses **deterministic tie-breaking** (stable ordering by id) so identical inputs give identical routes.
- V2 signals, collisions, and slopes extend the graph (edge occupancy, costs) instead of replacing it.

## Consequences
- Graph maintenance on infrastructure edits (build and demolish) needs thorough tests.
- Vehicles with unreachable destinations need a defined behaviour (order fails, vehicle flagged for the player).

## Implementation notes (Issue #42)

Core `RoadGraph` is a compressed orthogonal graph derived from the road tile channel: nodes at endpoints, junctions, isolates, and a deterministic promoted tile for pure degree-2 cycles; edges store integer length and an ordered tile path. Node ids equal the node tile id. The graph is not saved; it is rebuilt from tiles on world construction and after each successful `BuildRoad` / `DemolishRoad` (full `RebuildFromMap` in V1). A monotonic `Version` supports route-cache invalidation for Issue #43. Station nodes remain Issue #44.

## Implementation notes (Issue #43)

Core `RoadPathfinder.TryFindPath` runs A* over `RoadGraph` (node id → node id) with Manhattan heuristic on node tile coordinates and open-set tie-break `(f, g, nodeId)`. Successful routes are `RoadPath` values (ordered node ids, edge ids, total length). `RoadRouteCache` stores successes keyed by `(from, to)` stamped with `RoadGraph.Version` and recomputes when the version changes. Paths are not persisted. Mid-edge starts and vehicle/order behaviour remain Issue #45.

## Implementation notes (Issue #44)

Core `RoadStation` entities are 1×1 tiles stored on `GeneratedWorld` with a dedicated `TileMap` station channel (`0` = empty, otherwise `station.Id + 1`). Build requires orthogonal adjacency to a town or industry object; an adjacent road is optional. The station tile itself must not hold road or rail (Issue #55). `RoadGraph.RebuildFromMap` treats station tiles as nodes, promotes orthogonally adjacent road tiles to nodes, and adds length-1 station↔road edges. Save format version 3 persists the ordered station list; vehicle cargo and orders remain Issues #47 and #45.

## Implementation notes (Issue #45)

Core `RoadVehicle` entities are id-indexed on `GeneratedWorld` with looping station-id orders. Movement advances along `RoadGraph` edges in FixedPoint tile-steps per tick (`RoadVehicleMovement.PlaceholderSpeed`), using `RoadRouteCache` between consecutive order stops. Unreachable destinations set `IsStuck` without throwing; graph version changes finish or snap mid-edge travel then repath. When new track joins the middle of a vehicle's edge and splits it, the vehicle continues on the split part that holds it, at the same tile (it remembers its edge path while the graph is unchanged; Issue #193); it snaps to the edge start only when no split part matches, such as after a demolish. Rail vehicles follow the same rule. Spawn is station-only via `BuyRoadVehicle` / `SetRoadVehicleOrders` / `SellRoadVehicle` (free). Save format version 4 persists vehicles; cargo loading remains Issue #46. MapPlay **V** / Vehicles **Buy** spawns at the selected station **without** auto-orders (idle until the player sets at least two stops via the Vehicles route UI, Issue #136).

## Implementation notes (Issue #46 / superseded by #132)

Historical: vehicles unloaded/loaded against station cargo bags. **Issue #132** removed station cargo; see below.

## Implementation notes (Issue #47 / superseded by #132)

Historical: station capacity caps and `RoadStationCargoDecay`. Removed in Issue #132.

## Implementation notes (Issue #48 / updated by #132)

Placed industries hold a capped `CargoInventory`. `IndustryProduction` runs every `ProductionIntervalTicks` (placeholder 30): run one content recipe when inputs allow; outputs stay on the industry (no station push). When the bag is full, production still credits **Produced** and wastes excess output; level 2/3 still consume inputs when wasting. Building a station runs one immediate pulse for orthogonally adjacent industries. Unload credits `GeneratedWorld.CompanyCash` by `pricePerUnit` when the station is adjacent to a town or to an industry that lists the cargo as an input (`DeliveryAcceptance`). Freight unloaded at a town-adjacent station is consumed immediately (not stocked).

## Implementation notes (Issue #132)

Stations are stops only (no cargo bag). While servicing: unload **1 unit/tick per vehicle** into the adjacent industry (or town consume); then each industry distributes **1 unit/tick** round-robin across all loading vehicles (sorted by vehicle id; cursor persisted). Month **Transported** credits on vehicle load. Wait-for-full stays until full; load-and-leave leaves when the industry has no further loadable cargo for it. MapPlay shows industry cargo totals (including 0); town labels deferred. Save format version 9.

## Implementation notes (Issue #53)

Core `RailGraph` mirrors `RoadGraph`: compressed orthogonal graph from the rail tile channel, full `RebuildFromMap` after each successful `BuildRail` / `DemolishRail`, monotonic `Version` for cache invalidation. Core `RailPathfinder.TryFindPath` and `RailRouteCache` mirror the road APIs (A* over node ids, version-stamped cache). Rails may share tiles with roads; graph layers stay independent. Save format version 10 persists the rail channel; MapPlay renders rail tiles in placeholder grey (rail tint wins when both modes occupy a tile).

## Implementation notes (Issue #55)

V1 road and rail station placement rules are aligned on occupancy and connection semantics. Both modes use a 1×1 station tile that is the graph connection point; matching infrastructure attaches on an adjacent tile and is not required at build time. Neither station type may share its tile with object, road, rail, or the other station type (`RoadStationPlacement` / `RailStationPlacement` and build commands). A road station and a rail station may both be orthogonally adjacent to the same town or industry (multimodal hub). No save-format bump.

## Implementation notes (Issue #56)

Rail vehicles are locomotives plus zero or more wagons. Buying a train spawns a locomotive with `WagonCount = 0` (capacity 0). `AddRailWagon` / `SellRailWagon` change wagon count up to a fixed locomotive max (placeholder 3); capacity is `wagonCount × 10`. A train with fewer than one wagon **does not leave park or advance along edges**, even when it has a looping station route. Selling a wagon discards cargo above the new capacity. Save format version 12 persists `wagonCount`. Map placeholders show the locomotive plus one trailing marker per wagon.
