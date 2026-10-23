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

Core `RoadStation` entities are 1×1 tiles stored on `GeneratedWorld` with a dedicated `TileMap` station channel (`0` = empty, otherwise `station.Id + 1`). Build requires orthogonal adjacency to a town or industry object; an adjacent road is optional. `RoadGraph.RebuildFromMap` treats station tiles as nodes, promotes orthogonally adjacent road tiles to nodes, and adds length-1 station↔road edges. Save format version 3 persists the ordered station list; vehicle cargo and orders remain Issues #47 and #45.

## Implementation notes (Issue #45)

Core `RoadVehicle` entities are id-indexed on `GeneratedWorld` with looping station-id orders. Movement advances along `RoadGraph` edges in FixedPoint tile-steps per tick (`RoadVehicleMovement.PlaceholderSpeed`), using `RoadRouteCache` between consecutive order stops. Unreachable destinations set `IsStuck` without throwing; graph version changes finish or snap mid-edge travel then repath. Spawn is station-only via `BuyRoadVehicle` / `SetRoadVehicleOrders` / `SellRoadVehicle` (free). Save format version 4 persists vehicles; cargo loading remains Issue #46. MapPlay **V** spawns at the selected station with auto-orders across reachable stations.

## Implementation notes (Issue #46)

On order-stop arrival, vehicles enter a service phase: unload all cargo into the station, then load from the station at `RoadVehicleCargoTransfer.UnitsPerTick`, up to `RoadVehicle.PlaceholderCargoCapacity`. Per-order `WaitForFull` keeps the vehicle at the stop until full when the station is empty; otherwise it leaves when nothing more can transfer. Stations are mutable entities with a `CargoInventory`. Save format version 5 persists cargo stacks, wait-for-full flags, and service state. MapPlay seeds demo `wood` at industry-adjacent stations and tints loaded vehicles.

## Implementation notes (Issue #47)

Road stations share a placeholder cargo capacity (`RoadStation.PlaceholderCargoCapacity`). Unload uses free station capacity; leftover stays on the vehicle and unload completes when the vehicle is empty or the station is full. `RoadStationCargoDecay` removes `DecayAmount` units from each non-empty station every `DecayIntervalTicks` (ascending cargo-id order), on the same simulation tick path as vehicle movement. Over-capacity station bags are trimmed on construct/restore; save format stays at version 5. MapPlay shows temporary world-space cargo totals on stations and vehicles.

## Implementation notes (Issue #48)

Placed industries hold a capped `CargoInventory`. `IndustryProduction` runs every `ProductionIntervalTicks` (placeholder 30): pull recipe inputs from adjacent stations (L2+), run one content recipe cycle when inputs and free capacity allow, then push outputs to adjacent stations. Building a station also runs one immediate pulse for orthogonally adjacent industries so primary producers fill the new stop without waiting for the interval. Unload credits `GeneratedWorld.CompanyCash` by `pricePerUnit` only when the station is adjacent to a town or to an industry that lists the cargo as an input (`DeliveryAcceptance`). Save format version 6 persists company cash and industry cargo. MapPlay loads the content catalog, runs production on the tick path, and shows a temporary cash readout; towns and L2+ industries do not auto-fill stations without deliveries.
