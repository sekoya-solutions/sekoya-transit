# Architecture Decision Records (ADR)

An Architecture Decision Record (ADR) captures one significant technical decision: context, options considered, decision, and consequences.

## Rules

- One file per decision: `NNNN-short-title.md` (four digits, sequential).
- Status: `Proposed`, `Accepted`, `Superseded by NNNN`, or `Rejected`. Only the owner moves an ADR to `Accepted`.
- Never rewrite an accepted ADR: supersede it with a new one.
- Follow [template.md](template.md).

## Index

| # | Title | Status |
| --- | --- | --- |
| [0001](0001-unity-version.md) | Unity version | Accepted |
| [0002](0002-render-pipeline.md) | Render pipeline | Accepted |
| [0003](0003-simulation-architecture.md) | Deterministic simulation separated from presentation | Accepted |
| [0004](0004-code-organization-and-ui.md) | Code organization and UI framework | Accepted |
| [0005](0005-repository-layout-and-git-workflow.md) | Repository layout and Git workflow | Accepted |
| [0006](0006-coding-standards.md) | Coding standards | Accepted |
| [0007](0007-testing-strategy.md) | Testing strategy | Accepted |
| [0008](0008-continuous-integration.md) | Continuous integration | Accepted |
| [0009](0009-save-format-and-persistence.md) | Save format and persistence | Accepted |
| [0010](0010-data-driven-content-format.md) | Data-driven content format | Accepted |
| [0011](0011-localization.md) | Localization | Accepted |
| [0012](0012-world-and-map-data-structures.md) | World and map data structures | Accepted |
| [0013](0013-pathfinding-and-movement.md) | Pathfinding and movement | Accepted |
| [0014](0014-procedural-generation.md) | Procedural generation | Accepted |
| [0015](0015-unity-editor-version-policy.md) | Unity editor version policy (stay on 6.3 LTS, evaluate 6.7 LTS later) | Accepted |
| [0016](0016-unity-project-folder-name.md) | Unity project folder name (`SekoyaTransit/`, amends 0005) | Accepted |
| [0017](0017-map-rendering-approach-and-performance-budgets.md) | Map rendering approach (mesh chunks) and performance budgets | Accepted |
| [0018](0018-audio-approach.md) | V1 audio approach (CC0 assets; no mixer asset) | Accepted |
| [0019](0019-unity-package-selection.md) | Unity package selection | Accepted |
| [0020](0020-independent-markdown-quality-checks.md) | Independent Markdown quality checks | Proposed |
| [0021](0021-unity-ci-minute-budget.md) | Unity CI minute budget (EditMode on `main` only; platform builds manual except the demo deploy) | Accepted |
| [0022](0022-pcg32-random-generator.md) | PCG32 random number generator | Accepted |
| [0023](0023-fixed-point-representation.md) | Fixed-point representation | Accepted |
| [0024](0024-core-event-delivery-contract.md) | Queued Core event delivery contract | Accepted |
| [0025](0025-deterministic-state-hash-format.md) | Deterministic state hash format | Accepted |
| [0026](0026-simulation-calendar.md) | Simulation calendar | Accepted |
| [0027](0027-deterministic-gradient-noise.md) | Deterministic gradient noise | Accepted |
| [0028](0028-deterministic-town-placement.md) | Deterministic town placement | Accepted |
| [0029](0029-deterministic-industry-placement.md) | Deterministic industry placement | Accepted |
| [0030](0030-selective-local-unity-platform-builds.md) | Selective local Unity platform builds (local EditMode PR gate; WebGL when packaging risk) | Accepted |
| [0031](0031-ui-icons-as-hand-authored-svg.md) | UI icons as hand-authored SVG | Accepted |
| [0032](0032-map-art-from-kenney-cc0-packs.md) | Map art from Kenney CC0 packs, with 3D models baked to sprites | Accepted |
| [0033](0033-headless-balance-simulation.md) | Headless balance simulation (scripted player on the real Core) | Accepted |
| [0034](0034-webgl-demo-on-github-pages.md) | WebGL demo on GitHub Pages (GameCI release build, gzip with decompression fallback, automatic on `main` after a successful build) | Accepted |
