# Backlog proposal (V1)

Status: **Approved and created on GitHub (issues #1-#73). GitHub is now the source of truth; this file is kept as a historical snapshot.**
Once approved, each epic becomes a GitHub issue labeled `type: epic`, and each task a child issue, all assigned to a milestone and the [project board](https://github.com/users/sekoya-solutions/projects/2). After creation, GitHub is the source of truth for tasks and this file is archived.

Sizing is relative (S, M, L), not a time estimate. Order within a milestone is the suggested build order.

## M0 Foundations

Epic: **Project foundations**

- Done: repository, GitHub setup, Unity project, Core scaffold, CI dotnet job.
- Spike: WebGL feasibility (render 512x512 chunked isometric map, pooled moving sprites, UI Toolkit, Localization, IndexedDB save); output: rendering ADR and performance budgets (L).
- Unity CI job with GameCI: tests and Windows and WebGL builds, license secret setup (M).
- Decide audio approach and Unity package selection (S).
- Workshop: AI art pipeline with ComfyUI, style bible, naming and import conventions (L).
- Markdown lint and link check job (S).
- Content JSON schemas and validation test harness skeleton (M).

## M1 Core skeleton and map generation

Epic: **Deterministic simulation core** ([ADR 0003](adr/0003-simulation-architecture.md))

- Simulation loop: fixed tick, speed control, pause (S).
- Command pipeline: command interface, validation, apply, command log (M).
- Seeded deterministic random number generator (S).
- Fixed-point math types (M).
- Event bus from Core to presentation (S).
- State hashing and determinism replay test harness (M).
- Calendar: 1900 start, tick to date, 30 real minutes per year at normal speed (S).

Epic: **World and generation** ([ADR 0012](adr/0012-world-and-map-data-structures.md), [ADR 0014](adr/0014-procedural-generation.md))

- Tile storage arrays and coordinate types for 128/256/512 (M).
- Deterministic noise implementation (M).
- Town placement (M).
- Industry placement following chain rules, validation (L).
- Golden-seed tests (S).

Epic: **Content pipeline** ([ADR 0010](adr/0010-data-driven-content-format.md))

- JSON loader and definitions for cargo, industries, recipes (M).
- Referential integrity validation in CI (M).
- First content set: 5 chains of 3 levels (about 15 industries) with placeholder numbers (M).

Epic: **Save and load** ([ADR 0009](adr/0009-save-format-and-persistence.md))

- Versioned snapshot serialization with gzip and migration framework (L).
- `ISaveStore` for desktop and WebGL, autosave and manual save (M).
- Save compatibility test with archived saves (S).

## M2 Road transport vertical slice

Epic: **Rendering and camera** (rendering ADR from spike)

- Isometric chunked map rendering (L).
- Camera pan and zoom, mouse and keyboard (M).
- Minimap (M, optional).
- Tile selection and building placement preview (M).

Epic: **Road network and vehicles** ([ADR 0013](adr/0013-pathfinding-and-movement.md))

- Build and demolish roads (commands) (M).
- Road graph with incremental updates (L).
- Deterministic A* with cached routes (M).
- Road stations adjacent to industries or town buildings (M).
- Vehicles, orders, movement along edges (L).
- Cargo capacity, loading and unloading with wait-for-full option (M).
- Station cargo storage with cap and decay (M).
- Delivery income and production chain processing (L).

Epic: **Core UI**

- UI Toolkit shell, theme, localization wiring (M).
- Build toolbar (M).
- Vehicle list with return on investment (M).
- Industry list (production and transported) (M).

## M3 Rail transport

- Rail tracks, point-to-point, and graph (L).
- Rail stations and trains, wagons and capacity (L).
- Separate road and rail station rules (S).
- Content: rail vehicles (M).

## M4 Towns and needs

- Town population, growth tiers and needs per [gameplay-systems.md](specs/gameplay-systems.md) (L).
- Needs supply and demand tracking, growth score (M).
- Passenger and mail generation between towns (L).
- Town list UI with needs supplied and demand (M).

## M5 Finance, quests and progression

- Money, running costs, vehicle purchase (M).
- Loans with interest, bankruptcy rule (M).
- Vehicle availability by year and permanent unlocks with money (M).
- Score: assets plus cash (S).
- Main quests and random quests (L).
- Finance recap UI and quest UI (M).
- New game flow: seed and map size S/M/L (M).

## M6 Polish, audio and WebGL demo

- Audio: sound effects and one ambient track (M).
- Art integration of final sprites (L).
- Performance pass on 512x512 (L).
- WebGL build, deploy demo to GitHub Pages (M).
- README, trailer or screenshots, portfolio write-up (M).
- Economy balancing workshop (L).
