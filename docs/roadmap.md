# Roadmap

Status: **final version for now. V1 is done (2026-10-29). V2 is iterative, with no planned order or execution plan, so this file is no longer updated per issue.**

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
| Building | [#209](https://github.com/sekoya-solutions/sekoya-transit/issues/209) | Click and drag to build straight road and rail lines in one atomic command |
| HUD | [#215](https://github.com/sekoya-solutions/sekoya-transit/issues/215) | Map hint bar removed; top bar regrouped |
| HUD | [#225](https://github.com/sekoya-solutions/sekoya-transit/issues/225) | Pause button before the speed buttons |
| HUD | [#233](https://github.com/sekoya-solutions/sekoya-transit/issues/233) | Full-window panels no longer cover the toolbars |
| HUD | [#237](https://github.com/sekoya-solutions/sekoya-transit/issues/237) | Font sizes as a small set of USS variables |
| HUD | [#239](https://github.com/sekoya-solutions/sekoya-transit/issues/239) | Resources shown as icons, names in tooltips |
| Localization | [#217](https://github.com/sekoya-solutions/sekoya-transit/issues/217) | Only English ships; no "No translation found" in a French browser |
| World | [#219](https://github.com/sekoya-solutions/sekoya-transit/issues/219) | Towns and industries take 2x2 tiles |
| World | [#221](https://github.com/sekoya-solutions/sekoya-transit/issues/221) | At least 10 tiles between towns and industries |
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
