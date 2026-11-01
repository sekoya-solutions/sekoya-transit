# Roadmap

Status: **final version for now. V1 is done (2026-10-29). V2 is iterative, with no planned order or execution plan, so this file holds no plan or next-work pointer: it only lists V2 issues as they are delivered, added in each issue's first implementation commit.**

- Scope per game version: [product-vision.md](specs/product-vision.md).
- Live task status: GitHub [milestones, issues and the Project board](https://github.com/sekoya-solutions/sekoya-transit/issues) are the source of truth.
- Per-issue history: closed issues, merged pull requests and [ADRs](adr/README.md).

## V1 (done 2026-10-29)

| Milestone | Theme | Status |
| --- | --- | --- |
| Phase 0 | Foundations: specs, ADRs, repository and GitHub setup, Unity scaffolding, WebGL technical spike | Done |
| M0 | Foundation | Done |
| M1 | Core skeleton, map generation, content pipeline, save/load | Done |
| M2 | Road transport vertical slice | Done |
| M3 | Rail transport ([Epic #9](https://github.com/sekoya-solutions/sekoya-transit/issues/9)) | Done |
| M4 | Towns and needs ([Epic #10](https://github.com/sekoya-solutions/sekoya-transit/issues/10)) | Done |
| M5 | Finance, achievements, contracts and progression ([Epic #11](https://github.com/sekoya-solutions/sekoya-transit/issues/11)) | Done |
| M6 | Polish, audio and WebGL demo ([Epic #12](https://github.com/sekoya-solutions/sekoya-transit/issues/12)) | Done |

Highlights of the V1 result:

- A deterministic Core (commands, data-driven balance, versioned saves) with road and rail transport, five industry chains, town needs, finance, achievements and contracts.
- A map of 128, 256 or 512 tiles per side, drawn with custom mesh chunks ([ADR 0017](adr/0017-map-rendering-approach-and-performance-budgets.md)) and Kenney CC0 art ([ADR 0032](adr/0032-map-art-from-kenney-cc0-packs.md)).
- Audio, a save/load UI, a new game screen and a balance simulation ([ADR 0033](adr/0033-headless-balance-simulation.md)).
- The project was renamed from "Transport Tycoon 2" to Sekoya Transit ([#198](https://github.com/sekoya-solutions/sekoya-transit/issues/198)).
- The repository is public; the WebGL demo is at `https://sekoya-solutions.github.io/sekoya-transit/` and rebuilds automatically after each game-affecting merge to `main` ([ADR 0034](adr/0034-webgl-demo-on-github-pages.md)).
- README and portfolio case study: [README](../README.md), [portfolio.md](portfolio.md).

## V2 (incremental)

V2 is a stream of individual issues (fixes, then evolutions from the V2 scope in the product vision), created one at a time from the owner's prompts. There is no backlog, order or milestone plan. Delivered so far:

| Area | Issue | Result |
| --- | --- | --- |
| UI | [#270](https://github.com/sekoya-solutions/sekoya-transit/issues/270) | Toolbar: date at the bottom right instead of the seed (kept as hidden data); all toolbar buttons share one bigger size |
| Transport | [#268](https://github.com/sekoya-solutions/sekoya-transit/issues/268) | Opening a route's station window forces the Select tool and greys out the other tools, with a hint at the bottom of the window |
| Progression | [#266](https://github.com/sekoya-solutions/sekoya-transit/issues/266) | Active contracts box at the top left of the map; the 3 initial contracts are accepted at game start |
| Transport | [#264](https://github.com/sekoya-solutions/sekoya-transit/issues/264) | Clicking an existing station adds it to the open route even when a station build tool is active |
| Building | [#209](https://github.com/sekoya-solutions/sekoya-transit/issues/209) | Click and drag to build straight road and rail lines in one atomic command |
| HUD | [#215](https://github.com/sekoya-solutions/sekoya-transit/issues/215) | Map hint bar removed; top bar regrouped |
| HUD | [#225](https://github.com/sekoya-solutions/sekoya-transit/issues/225) | Pause button before the speed buttons |
| HUD | [#233](https://github.com/sekoya-solutions/sekoya-transit/issues/233) | Full-window panels no longer cover the toolbars |
| HUD | [#237](https://github.com/sekoya-solutions/sekoya-transit/issues/237) | Font sizes as a small set of USS variables |
| HUD | [#252](https://github.com/sekoya-solutions/sekoya-transit/issues/252) | UI scales with the screen size (reference 1920x1080), so browser zoom no longer changes the layout |
| HUD | [#256](https://github.com/sekoya-solutions/sekoya-transit/issues/256) | Select tool icon redrawn as a clean, symmetric pointer |
| HUD | [#239](https://github.com/sekoya-solutions/sekoya-transit/issues/239) | Resources shown as icons, names in tooltips |
| Localization | [#217](https://github.com/sekoya-solutions/sekoya-transit/issues/217) | Only English ships; no "No translation found" in a French browser |
| Art | [#248](https://github.com/sekoya-solutions/sekoya-transit/issues/248) | Original Grok Imagine sprites for towns and the forest chain (forest, sawmill, furniture factory) |
| Art | [#254](https://github.com/sekoya-solutions/sekoya-transit/issues/254) | Original Grok Imagine sprites for the other industry chains (wheat, iron, livestock, sheep); every chain industry now has original art |
| Art | [#260](https://github.com/sekoya-solutions/sekoya-transit/issues/260) | Original Grok Imagine sprites for the truck, locomotive and wagon |
| Art | [#262](https://github.com/sekoya-solutions/sekoya-transit/issues/262) | Original Grok Imagine sprites for the stations, roads and rails; only the generic industry fallback still uses Kenney art |
| Art | [#272](https://github.com/sekoya-solutions/sekoya-transit/issues/272) | Original Grok Imagine ground atlas: weighted terrain variants and soft borders between grass, earth and sand |
| Presentation | [#274](https://github.com/sekoya-solutions/sekoya-transit/issues/274) | Selecting any tile of a town or industry highlights the whole building block |
| World | [#258](https://github.com/sekoya-solutions/sekoya-transit/issues/258) | Darker ground border around each town and industry block, to see its base square |
| World | [#250](https://github.com/sekoya-solutions/sekoya-transit/issues/250) | Towns take 3x3 tiles (room for stations); industries stay 2x2 |
| World | [#219](https://github.com/sekoya-solutions/sekoya-transit/issues/219) | Towns and industries take 2x2 tiles |
| World | [#246](https://github.com/sekoya-solutions/sekoya-transit/issues/246) | Any of a town's or industry's 4 tiles selects it on the map |
| World | [#221](https://github.com/sekoya-solutions/sekoya-transit/issues/221) | At least 10 tiles between towns and industries |
| Towns | [#242](https://github.com/sekoya-solutions/sekoya-transit/issues/242) | Growth from partly and fully met needs per level, towns panel rework, Raw / Processed / Finished goods (save format 28) |
| Towns | [#244](https://github.com/sekoya-solutions/sekoya-transit/issues/244) | Monthly demand per need raised to 500 / 400 / 300 by level |
| Progression | [#223](https://github.com/sekoya-solutions/sekoya-transit/issues/223) | Achievements as bronze, silver and gold tracks in a full-screen window (save format 25) |
| Progression | [#227](https://github.com/sekoya-solutions/sekoya-transit/issues/227) | Contracts rework: proposal, accepted, completed, failed (save format 26) |
| Transport | [#229](https://github.com/sekoya-solutions/sekoya-transit/issues/229) | Named routes with shared stops and vehicles, Routes window (save format 27) |
| Tests and CI | [#231](https://github.com/sekoya-solutions/sekoya-transit/issues/231) | Allocation tests compiled out of Unity instead of ignored |
| Tests and CI | [#235](https://github.com/sekoya-solutions/sekoya-transit/issues/235) | Stale WebGL demo builds cancelled when a newer commit lands |

Known open follow-ups (not scheduled):

- Delete the unreachable pre-27 save migrations.
- Remove the unused selected-vehicle map tint.
- Refresh the unlock timings in [economy-progression.md](specs/economy-progression.md); further balance tuning (for example per map size) re-runs the balance simulation.
- Minimap ([#39](https://github.com/sekoya-solutions/sekoya-transit/issues/39), closed as not planned until V2 is scoped).
- Loans with interest (deferred from V1).
- Higher-quality AI art generation and production art placement ([art-pipeline.md](specs/art-pipeline.md)); the Issue #16 ComfyUI study validated feasibility only.
- V2 and V3+ scope: see the scope table in [product-vision.md](specs/product-vision.md).
