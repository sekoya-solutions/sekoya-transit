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
- The map art (terrain colors, road and rail atlas, sorting convention) is decided in [ADR 0032](0032-map-art-from-kenney-cc0-packs.md): terrain stays vertex-colored, and roads and rails use a second per-chunk mesh with one texture atlas.
- Vehicle LOD/aggregation at far zoom must be planned before large networks (spike: 30k vehicles at far zoom gave 24 fps).
- Build pipeline rule: switch the active build target before building Addressables.
- Saves use a dedicated IndexedDB bridge (`.jslib`), not `persistentDataPath` alone (see ADR 0009).
- Build size is an unoptimised 20.5 MB gzip; stripping and removing unused packages is a follow-up.
- Budgets are provisional until measured on lower-end hardware and other browsers.

## Amendment: frame pacing and measured results (Issue #70, 2026-10-27)

### Frame pacing
Without a limit, the game redrew an unchanged map at the display refresh rate (165 fps on a 165 Hz monitor). The frame rate is now limited to about the 60 fps budget:

- **Windows player:** vertical sync off and `Application.targetFrameRate = 60`.
- **WebGL:** draw on every Nth browser animation frame (`QualitySettings.vSyncCount = N`, no target frame rate). A WebGL target frame rate is scheduled with JavaScript timers and cost about twice the CPU per frame in Chrome (61 fps at 58% of one core, against 82.5 fps at 45%). Unity reports 60 Hz on WebGL whatever the display, so N is chosen from the median frame interval over the first second: N = floor(refresh rate / 60), so the game runs at 60 fps or more (82 fps at 165 Hz, 72 at 144 Hz, 60 at 60 or 120 Hz).

### Measurement method
Development WebGL build in hardware-accelerated Chrome; check that the `WEBGL_debug_renderer_info` renderer string names a real graphics card, and keep the window uncovered (Chrome throttles covered windows). The Claude desktop in-app browser draws WebGL in software when its hardware acceleration is off, so it is not used for performance numbers. Scenario: Large map, seed 12345, simulation at x1. The Development-only frame logger prints frame rate, script time and managed allocations every 5 s to the browser console; Chrome CPU is the sum over Chrome processes, sampled for 15 s. F10 (Development players only) adds the load: 20 road lines with 8 trucks each and 10 rail lines with 4 three-wagon trains each (200 vehicles).

### Results (NVIDIA RTX 5090, 165 Hz display)
| Scenario | Frame rate | Scripts per frame | Managed allocation per frame | Chrome CPU (one core = 100%) |
|---|---|---|---|---|
| Before: idle, default zoom | 165 fps | 0.17 ms | 3,968 B | 81% |
| After: idle, default zoom | 82.5 fps | 0.10 ms | 30-34 B | 38% |
| After: idle, farthest zoom | 82.5 fps | 0.08 ms | 30 B | 40-48% |
| After: 200 vehicles, default zoom | 82.5 fps (frames at most 14 ms) | 0.6 ms | 65-143 B | 47% |
| After: 200 vehicles, farthest zoom | 82.5 fps (frames at most 14 ms, one 29 ms) | 0.55 ms | 74-104 B | 51% |

The farthest zoom still shows only part of a 512x512 map, so the vehicle runs keep most of the 200 vehicles off screen. About 30 B per frame also appears on the new game menu, where no game per-frame code allocates; it is attributed to Unity packages. Game code no longer allocates on the idle map: idle simulation ticks, label and vehicle refreshes, the date label and the wagon trail are guarded by .NET and EditMode allocation tests.

### Consequences of the amendment
- Road and rail graph rebuilds reuse their map-sized scratch arrays (they produced about 3 MB of garbage per built tile on a 512x512 map, which WebGL cannot collect within a frame, and ran a long scripted build out of memory). A rebuild still scans the whole map, so building many tiles in one frame costs CPU (2.4 s for the 200-vehicle load); an incremental graph update is possible future work.
- Further CPU savings when nothing moves (redrawing less often while idle) were left out of Issue #70.
