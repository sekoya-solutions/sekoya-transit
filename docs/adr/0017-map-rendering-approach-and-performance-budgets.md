# ADR 0017: Map rendering approach and performance budgets

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner (approved 2026-10-19)

## Context
[ADR 0012](0012-world-and-map-data-structures.md) left open whether the map is rendered with Unity's `Tilemap` or custom meshes, to be decided from WebGL measurements. The spike ([webgl-feasibility](../spikes/webgl-feasibility.md), issue #13) measured both on a 512x512 map in a WebGL build.

## Options considered
1. **Unity `Tilemap` (isometric)**: simple authoring and editor tooling. On WebGL, CPU cost grew with visible tiles: 10 ms at mid zoom and 38 ms at far zoom with no vehicles (26 fps). Memory 141-215 MB.
2. **Custom mesh chunks (32x32 tiles, one mesh per chunk)**: more code to write. Cost nearly flat across zoom (0.7-0.9 ms), memory 64-112 MB, build time 30-49 ms.

## Decision
Option 2. The map is rendered from 32x32-tile chunk meshes, rebuilt only when a tile in the chunk changes (dirty flag). The simulation never depends on rendering objects: tile data stays in the data structures of ADR 0012 and the view reads from it.

### Performance budgets (WebGL, desktop mid/high-end, to be re-validated on lower-end hardware)
| Budget | Value |
|---|---|
| Frame time at the display refresh rate | 60 fps minimum (16.6 ms) at every zoom level |
| Map rendering cost, any zoom | <= 2 ms main-thread CPU |
| Individually drawn vehicles on screen | <= 10,000 (above this, LOD or aggregation is required) |
| Unity memory | <= 300 MB, hard ceiling 500 MB |
| Save | <= 3 MB raw, written gzip-compressed through IndexedDB |
| WebGL download (gzip) | <= 25 MB for V1 |

## Consequences
- The Tilemap is not used for the main map. Small overlays (cursor, selection, ghost placement) may use plain sprites.
- Chunk meshes need a custom isometric sorting/layering convention and a texture atlas for terrain; both belong to the rendering implementation issues.
- Vehicle LOD/aggregation at far zoom must be planned before large networks (spike: 30k vehicles at far zoom gave 24 fps).
- Build pipeline rule: switch the active build target before building Addressables.
- Saves use a dedicated IndexedDB bridge (`.jslib`), not `persistentDataPath` alone (see ADR 0009).
- Build size is an unoptimised 20.5 MB gzip; stripping and removing unused packages is a follow-up.
- Budgets are provisional until measured on lower-end hardware and other browsers.
