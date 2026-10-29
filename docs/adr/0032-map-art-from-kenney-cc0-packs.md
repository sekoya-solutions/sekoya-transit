# ADR 0032: Map art from Kenney CC0 packs, with 3D models baked to sprites

- Status: Accepted
- Date: 2026-10-28
- Deciders: Owner (approved 2026-10-28)

## Context

Issue [#69](https://github.com/sekoya-solutions/transport-tycoon-2/issues/69) replaces the placeholder map (flat colored diamonds from [ADR 0017](0017-map-rendering-approach-and-performance-budgets.md)'s chunk meshes, plus colored diamond markers for vehicles) with final art for terrain, roads, rails, level crossings, stations, towns, industries, trucks, locomotives, and wagons. The [Issue #16 ComfyUI pilot](../specs/art-pipeline.md) proved generation is feasible but not production-ready, so the owner asked to look for free, open-licensed art first.

Constraints:

- The map is a 2:1 isometric grid: one tile is 1 × 0.5 world units on screen (`IsometricProjection`). V1 terrain is flat ([gameplay-systems.md](../specs/gameplay-systems.md)).
- Roads and rails connect to their four orthogonal neighbors only. The player builds one tile per click and vehicles may turn on any tile, so a network needs all 16 neighbor shapes: single, dead end, straight, corner, T, and crossway. Road and rail may share a tile (level crossing).
- Each town and each industry occupies exactly one tile ([ADR 0028](0028-deterministic-town-placement.md), [ADR 0029](0029-deterministic-industry-placement.md)). Industries, vehicles, and cargo are data-driven, so new content must not break the map when it has no art yet.
- The project renders with the Universal Render Pipeline (URP) 2D renderer ([ADR 0002](0002-render-pipeline.md)) and must stay within the WebGL budgets of ADR 0017.

## Options considered

1. **Open 2D isometric sets only.** Kenney's CC0 2D isometric packs (roads, landscape, city, buildings, vehicles) and Screaming Brain Studios' CC0 road packs match the 2:1 grid, but none contains rails, trains, freight wagons, or a convincing truck. Rejected as incomplete.
2. **Complete Transport Tycoon-style sets (OpenGFX, Simutrans pak128).** These cover everything, but they are GPL v2 (copyleft) or Artistic-licensed with many authors, and they would make the game look like a copy of Transport Tycoon Deluxe. Rejected.
3. **Live 3D models in the game.** These need 3D lighting in the 2D renderer and a mesh per object on screen, against ADR 0017's budgets. Rejected.
4. **Kenney CC0 packs, mixing 2D tiles with 3D models baked to sprites (chosen).** Kenney's 2D isometric road tiles cover every road shape. Kenney's 3D kits (Train Kit, Car Kit, City Kit Industrial, City Kit Suburban) provide the rest and are rendered once, in the editor, into sprites at the map's exact angle. All packs are Creative Commons Zero (CC0, a public domain dedication) from one author, so licensing is simple and the style is broadly consistent. The owner reviewed renders and an 8 × 8 mock map made this way.

Ways to produce the baked sprites: hand-picked Kenney preview images (64 pixels (px), wrong angle: rejected); a throwaway Unity project outside the repository (not reproducible by reviewers: rejected); **an editor bake tool in the repository plus the committed output sprites (chosen)**.

## Decision

### Sources

| Map element | Source |
| --- | --- |
| Terrain | No tile art. Flat colors taken from the top faces of Kenney Isometric Landscape tiles 067 (grass), 059 (sand), and 083 (earth). |
| Roads | Kenney Isometric Roads 2D tiles: `roadNS`/`roadEW`, `crossroadNES`/`ESW`/`NSW`/`NEW`, `crossroad`, `roadNE`/`ES`/`SW`/`NW`, `endN`/`E`/`S`/`W`, cropped to the tile's top diamond. |
| Rails | Train Kit `track` (one tile), baked into every shape (see below). |
| Level crossing | The road tile plus Train Kit `railroad-rail-straight` (rails without sleepers) scaled to one tile. |
| Stations | City Kit Industrial `building-a` (road station) and `building-o` (rail station). |
| Towns | City Kit Suburban houses (`building-type-a`, `-c`, `-f`, `-m`). |
| Industries | City Kit Industrial buildings by industry id (`sawmill` = `building-s`, `furniture_factory` = `building-m`, `iron_mine` = `building-e`); `forest` = 2D conifers from Isometric Roads. |
| Vehicles | Car Kit `delivery` (every truck model), Train Kit `train-locomotive-c` (every locomotive model) and `train-carriage-container-blue` (every wagon). |

Kenney's direction letters map onto the grid as N = screen upper-left (+Y), E = upper-right (+X), S = lower-right (−Y), and W = lower-left (−X).

### Baking

- The source models (only the files used, with their `colormap.png` textures) live in an editor-only folder under `Assets/_Project/Art/`. They are never referenced by a scene or the player build.
- An editor tool renders them into PNG sprites with an orthographic camera at pitch 30° and yaw 45°, which turns a square tile into the 2:1 diamond, under one fixed light. It draws through a command buffer with its own simple lit shader, so the result does not depend on the URP 2D renderer.
- Rails: straight = the track piece turned 0° or 90°; crossway = two pieces; T = a straight piece plus a half-length piece; corner = the track mesh subdivided and bent into a quarter circle (the Kenney texture coordinates, and so the colors, are kept); dead end and single tile = straight. The track is narrowed to the locomotive's scale.
- Vehicles are baked in four headings (N, E, S, W). Car Kit and Train Kit models both face +Z (north at yaw 0).
- The baked PNGs are committed (Git Large File Storage, [ADR 0005](0005-repository-layout-and-git-workflow.md)) and are what the game uses. Re-baking may differ slightly between graphics cards, so the committed files are the reference, not the bake.

### In-game rendering

- **Terrain** stays in ADR 0017's chunk meshes as vertex colors. Sand and earth patches are decorative: Presentation derives them from the world seed and tile coordinate with the Core gradient noise ([ADR 0027](0027-deterministic-gradient-noise.md)). There is no Core, save, or gameplay change, and the same seed always looks the same. Only the front edges of the map show a brown "block side" strip. A thin darker grid line runs along every tile edge (Issue [#193](https://github.com/sekoya-solutions/transport-tycoon-2/issues/193)): each ground diamond maps a generated 64 x 64 texture whose 1-pixel border is 80 % gray, multiplied with the vertex color, so the grid adds no vertices and fades with mip maps when zoomed out.
- **Roads and rails** are a second mesh per chunk using one texture atlas. Each tile's shape comes from its four neighbors (a 4-bit mask); a road or rail station next to a tile counts as a connection for its mode. A level crossing draws the road quad, then the rails-only quad.
- **Stations, towns, industries, forest trees, and vehicles** are sprites sorted by isometric depth. A vehicle shows the sprite for its current heading.
- **Map label icons** (Issue #193): the cargo icons in industry, town, and vehicle labels are the UI cargo SVGs of [ADR 0031](0031-ui-icons-as-hand-authored-svg.md) baked into 64-pixel PNG sprites by a second editor tool (Transport Tycoon / Art / Bake Cargo Icons, also run at the end of the map art bake). It tessellates each SVG with Unity's vector graphics module and fills the triangles on the CPU with 4 × 4 supersampling, so it needs no camera or shader and gives the same pixels on every machine. Labels are world-space text runs plus these sprites laid out on one line.
- **Lookup with fallback**, as in [ADR 0031](0031-ui-icons-as-hand-authored-svg.md): industry sprites are found by industry id and fall back to a generic industry sprite; vehicles use one sprite set per mode (truck, locomotive, wagon) for now; a town picks its house from a fixed list by town id.

## Consequences

- Positive: complete, consistent, license-free art for every V1 map element; rails get every shape from a single Kenney piece; adding an industry or vehicle model still needs only data (it falls back to generic art); the runtime never touches 3D models.
- Negative: two art styles side by side (flat 2D roads, shaded 3D renders); the 2D road tiles are only 100 px wide, so the closest camera zoom is limited to keep a tile at most about 135 px wide on a 1080p screen; the station building hides a vehicle standing on its tile; one sprite stands for a whole town of up to 10,000 inhabitants.
- Follow-up: credits for every pack in `CREDITS.md`; the art-pipeline spec documents the bake tool; per-model vehicle art, town growth visuals, and decorative scenery trees are possible later and are out of scope here. The Issue #16 pilot images remain a study and are not used on the map.
