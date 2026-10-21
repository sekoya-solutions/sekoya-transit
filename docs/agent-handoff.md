# Artificial Intelligence (AI) Agent Handoff

This page provides a concise, model-neutral entry point for continuing work in this repository. [AGENTS.md](../AGENTS.md) contains the mandatory project rules; accepted technical decisions are in the [Architecture Decision Record (ADR) index](adr/README.md); [chat-history.md](chat-history.md) is the complete append-only conversation archive.

Markdown documentation is client-independent, but an AI application may not automatically load `AGENTS.md`. If the client does not, explicitly provide `AGENTS.md` and this handoff as project instructions. Keep those files as the source of truth rather than creating conflicting client-specific copies.

## Current status

Status snapshot verified on 2026-10-21:

- Milestone M0 (Foundations) and Milestone M1 (Core skeleton and map generation) are complete and closed. M1 has 22 closed issues, no open issues, and no unchecked acceptance criteria.
- Phase 1 is still in progress. M2 (Road transport vertical slice) is the next milestone; its three epics and 16 child issues are in GitHub.
- The next planned task is Issue #37, “Isometric chunked map rendering,” under Epic #6 (Rendering and camera). Issue #37 is open, and its acceptance criteria are still a placeholder. Epic #6 is Todo. No M2 implementation has started.
- Before starting #37, define testable acceptance criteria, set both #37 and Epic #6 to In Progress on the GitHub Project board, and verify both statuses. Do not silently change the backlog scope.
- M2 backlog grouping: Epic #6 contains #37–#40, Epic #7 contains #41–#48, and Epic #8 contains #49–#52. GitHub Issues and the Project board are authoritative for live status.

## Repository snapshot and persistence warning

At the time this handoff was written, local `main` was at `75b704d`, matching `origin/main`. All current documentation updates, including the M1 roadmap and history closeout, are local working-tree changes and have not been committed or pushed. Run `git status --short --branch` before continuing. These edits will not appear in another clone until they are committed and published through the repository workflow.

The GitHub repository is `sekoya-solutions/transport-tycoon-2`. Use `gh` for GitHub operations when available. Keep work on short-lived branches and use pull requests; do not merge without the owner's explicit authorization.

## First task: Issue #37

Read these sources before scoping or implementing rendering work:

- [Issue #37](https://github.com/sekoya-solutions/transport-tycoon-2/issues/37) and [Epic #6](https://github.com/sekoya-solutions/transport-tycoon-2/issues/6)
- [ADR 0017: map rendering approach and performance budgets](adr/0017-map-rendering-approach-and-performance-budgets.md)
- [Web Graphics Library (WebGL) feasibility spike](spikes/webgl-feasibility.md)
- [ADR 0003: deterministic simulation boundary](adr/0003-simulation-architecture.md), [ADR 0004: code organization and UI framework](adr/0004-code-organization-and-ui.md), [ADR 0012: world and map data structures](adr/0012-world-and-map-data-structures.md), and [ADR 0014: procedural generation](adr/0014-procedural-generation.md)
- [Product vision](specs/product-vision.md) and [gameplay systems](specs/gameplay-systems.md)

The accepted rendering direction is custom 32 × 32-tile mesh chunks, rebuilt when a tile in the chunk changes; Unity Tilemap is not the main-map renderer. Core owns map data and remains independent of Unity presentation. The WebGL budgets in ADR 0017 are provisional: the measurements came from an RTX 5090 desktop and do not establish low-end-device or other-browser performance.

## Engineering and delivery rules

- Keep simulation and game state deterministic, Unity-independent, and mutated through commands. Rendering and UI observe Core state; they do not own or mutate it.
- Unity project version is pinned in `TransportTycoon2/ProjectSettings/ProjectVersion.txt` (currently Unity 6000.3.26f1). Core uses C# 9, has `#nullable enable`, and must not reference `UnityEngine`.
- Add regression tests with features. For Core changes, run `dotnet test dotnet/TransportTycoon.Core.Tests` and `dotnet format dotnet/TransportTycoon.Core.Tests --verify-no-changes` before committing.
- For pull requests (PRs) changing `TransportTycoon2/`, Unity EditMode tests run in Continuous Integration (CI). Run local EditMode tests and Windows/WebGL builds only once per PR, as final pre-merge validation after the owner authorizes merging; do not run them before PR creation or repeat them if already completed. Skip local Unity builds for documentation-only changes. See [ADR 0021](adr/0021-unity-ci-minute-budget.md) and [Unity CI instructions](ci/unity-license.md).
- Before completing an issue, commit and push its feature branch and open a PR using the repository template, with a verified `Closes #N` reference. Leave it open for owner review. After authorized merge, verify the merged result, check each accepted issue criterion, and synchronize GitHub issue and Project statuses.
- Append every conversation exchange to [chat-history.md](chat-history.md). Update [AGENTS.md](../AGENTS.md) in the same turn whenever the owner establishes a project-wide rule or preference.

## Where the sources of truth live

- Project rules and workflow: [AGENTS.md](../AGENTS.md)
- Current milestones and high-level sequence: [roadmap.md](roadmap.md) and GitHub milestones/issues
- Product scope: [product-vision.md](specs/product-vision.md) and [gameplay-systems.md](specs/gameplay-systems.md)
- Technical choices: accepted ADRs listed in [adr/README.md](adr/README.md); proposed ADRs are not approved
- Runtime content structure: [content-format.md](specs/content-format.md)
- AI-art pilot limitations and provenance: [art-pipeline.md](specs/art-pipeline.md); Issue #16 validates feasibility only and does not establish production readiness
- Detailed chronological context: [chat-history.md](chat-history.md)

Re-check Git, GitHub issue/Project status, and the relevant source documents before acting: this status snapshot can become stale.
