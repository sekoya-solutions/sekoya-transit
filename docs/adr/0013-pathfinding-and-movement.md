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

Core `RoadPathfinder.TryFindPath` runs A* over `RoadGraph` (node id → node id) with Manhattan heuristic on node tile coordinates and open-set tie-break `(f, g, nodeId)`. Successful routes are `RoadPath` values (ordered node ids, edge ids, total length). `RoadRouteCache` stores successes keyed by `(from, to)` stamped with `RoadGraph.Version` and recomputes when the version changes. Paths are not persisted. Mid-edge starts and vehicle/order behaviour remain Issue #45; station nodes remain Issue #44.
