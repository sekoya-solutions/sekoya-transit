# Roadmap

Status: **Draft. Phase 0 and Milestones M0–M1 are complete; Phase 1 remains in progress through the M2 road-transport vertical slice.** See the [AI agent handoff](agent-handoff.md) for the current issue and repository state.
Scope per game version is in [product-vision.md](specs/product-vision.md). Once GitHub is set up, milestones and issues become the source of truth for tasks and this file stays a high-level overview.

## Phase 0: Foundations (completed)

1. Specs and initial technical decisions ([specs](specs/), [ADRs](adr/README.md)). The M0 foundation scope is complete. Higher-quality AI art generation and production in-game art placement remain future work; the [Issue #16 ComfyUI feasibility study](specs/art-pipeline.md) validates feasibility only, not production readiness.
2. **Repository setup**: initialize Git locally with `.gitignore` (Unity), `.gitattributes` (Git Large File Storage (LFS) rules, line endings), `.editorconfig`, license, README; create the GitHub repository; first commit; push.
3. **GitHub setup**: labels, milestones (V1 phases), issue templates and pull request template, project board, branch protection on `main`, fast CI workflow (`dotnet` job) skeleton.
4. **Backlog creation**: turn the specs into milestones and issues (epics and tasks).
5. **Unity project scaffolding** in `TransportTycoon2/`: create with the pinned 6.3 LTS patch, 2D URP template, assembly definitions per [ADR 0004](adr/0004-code-organization-and-ui.md), `Core` plain .NET test project ([ADR 0007](adr/0007-testing-strategy.md)), analyzers ([ADR 0006](adr/0006-coding-standards.md)).
6. **Technical spike (WebGL prototype)**: render a 512x512 chunked isometric map, move pooled vehicle sprites, test UI Toolkit, Localization, and IndexedDB saves on WebGL. Outcome: rendering ADR and performance budgets.

## Parallel workshops (any time)

- AI art workflow: ComfyUI feasibility validated in [Issue #16](specs/art-pipeline.md); consistent high-quality generation, curation, and production placement remain future work.

## Phase 1: V1 vertical slice (in progress)

Milestone M1, Core skeleton and map generation, is complete: the deterministic Core foundation, world generation, content pipeline, and initial save/load compatibility are implemented. Milestone M2, Road transport vertical slice, is next; Phase 1 remains in progress until a road transport chain works end to end, then expands per [gameplay-systems.md](specs/gameplay-systems.md). Detailed breakdown comes from the backlog.

## Later

Economy balancing workshop after the game is built. V2 and V3+ per the scope table.
