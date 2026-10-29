# Spike: WebGL feasibility (issue #13)

- Date: 2026-10-19
- Status: Complete
- Code: `SekoyaTransit/Assets/_Spikes/WebGLFeasibility/` (throwaway, not production code)
- Outcome: [ADR 0017](../adr/0017-map-rendering-approach-and-performance-budgets.md)

## Question

Can the game's core load (a 512x512 isometric map, thousands of moving vehicles, UI Toolkit, Localization, saves) run acceptably in a Unity 6.3 WebGL build, and which map rendering approach should be used?

## Method

- Scene: 512x512 tiles (262,144), procedural terrain, rendered in two ways:
  - **A. Mesh chunks**: 32x32-tile chunks, one mesh each.
  - **B. Tilemap**: a single isometric `Tilemap`.
- Vehicles: pooled `SpriteRenderer`s random-walking on the map, in counts 0 / 2,000 / 10,000 / 30,000.
- Each combination runs 3 camera phases (close, mid, far = orthographic size 6 / 30 / 130), 5 s each after 60 warm-up frames.
- The same benchmark code runs in a Windows player and in the WebGL build (served locally, Chromium-based integrated browser).
- Saves: deterministic 2.5 MB payload, gzip, write to a file under `persistentDataPath` and to raw IndexedDB (own `.jslib`), then reload the page and verify.
- Hardware: RTX 5090, desktop. **Numbers are best case, not a minimum-spec guarantee.** The Windows run was vsync-capped at 157 fps; WebGL at about 125 fps (requestAnimationFrame cap).

## Results

### Rendering, WebGL (CPU main-thread ms per frame / average fps)

| Vehicles | Zoom | Mesh chunks | Tilemap |
| --- | --- | --- | --- |
| 0 | close | 0.7 ms / 125 fps | 1.3 ms / 125 fps |
| 0 | mid | 0.7 ms / 126 fps | 10.4 ms / 93 fps |
| 0 | far | 0.9 ms / 126 fps | 38.2 ms / 26 fps |
| 10,000 | close | 2.3 ms / 125 fps | 2.9 ms / 125 fps |
| 10,000 | mid | 2.7 ms / 125 fps | 13.5 ms / 72 fps |
| 10,000 | far | 9.0 ms / 105 fps | 47.9 ms / 21 fps |
| 30,000 | close | 6.2 ms / 124 fps | 7.3 ms / 121 fps |
| 30,000 | mid | 9.3 ms / 103 fps | 21.9 ms / 44 fps |
| 30,000 | far | 42.1 ms / 24 fps | 82.1 ms / 12 fps |

(2,000 vehicles are within noise of 0 for both.)

Windows player, for reference: both hold 157 fps up to 10k vehicles; at 30k vehicles far, Mesh 125 fps and Tilemap 107 fps. Mesh builds in 41 ms (30-49 ms WebGL), Tilemap in 107-110 ms. Unity memory on WebGL: Mesh 64-112 MB, Tilemap 141-215 MB.

Findings:

- The Tilemap's cost grows steeply as the map zooms out (more tiles on screen) and dominates WebGL even with no vehicle.
- Mesh chunks are nearly free regardless of zoom; the remaining cost is vehicles (about 0.1 ms per 1k when zoomed in, noticeably more when zoomed out, since every sprite is still a draw item).
- 30k vehicles zoomed far out is not viable as individual sprites on WebGL (24 fps even with meshes). 10k is fine.

### UI Toolkit, Localization

- UI Toolkit panel (runtime, built in code) renders and updates on WebGL.
- Localization (en/fr, Addressables) works on WebGL, init 207-223 ms. **Pitfall found:** Addressables content is platform-specific; building it while the active target is Windows produced a WebGL build with 404s on `StandaloneWindows64` bundles and 0 locales. Always switch the active build target before building Addressables (CI must do this).
- `WaitForCompletion` is not allowed on WebGL; use async handlers.

### Saves

| Item | Result |
| --- | --- |
| Payload | 2,568,162 B raw, 1,084,606 B gzip (Fastest = Optimal) |
| gzip / gunzip time | 65-89 ms / 11 ms |
| File under `persistentDataPath` (`/idbfs`) | write 1 ms, **not found after page reload** |
| Raw IndexedDB via `.jslib` | write 139 ms, **found after reload, hash verified** |

Unity's file-based persistence was not flushed to IndexedDB without an explicit `FS.syncfs`; a dedicated IndexedDB bridge is deterministic and also lets the web page own export/import. The jslib API is polling-based (no managed callbacks), acceptable for save/load.

### Build size (uncompressed / gzip)

| File | Raw | gzip |
| --- | --- | --- |
| WebGL.wasm | 58.6 MB | 14.7 MB |
| WebGL.data | 14.6 MB | 5.6 MB |
| framework.js | 0.4 MB | small |
| Total | ~73 MB | **~20.5 MB** (Brotli typically smaller, not measured) |

This is an unoptimised baseline (default stripping, all default packages). Reducing it is a follow-up, not a blocker.

## Verdicts

| Area | Verdict |
| --- | --- |
| Unity WebGL as the target | **Go** |
| Map rendering | **Mesh chunks (A). Tilemap (B) rejected** for the full map |
| UI Toolkit | **Go** |
| Localization package | **Go** (with the Addressables build-target rule) |
| Saves | **Go**: gzip + raw IndexedDB bridge; not `persistentDataPath` alone |
| Vehicles | **Go up to ~10k** individually drawn; beyond that needs LOD/aggregation |

## Limits of this spike

- Single machine, high-end GPU; low-end hardware and other browsers (Firefox, Safari) untested. GPU timings are unavailable on WebGL.
- Procedural flat-colour terrain, no real art, lighting or overlays.
- Vehicle simulation is a random walk, not the real simulation.
