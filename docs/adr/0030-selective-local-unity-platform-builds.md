# ADR 0030: Selective local Unity platform builds

- Status: Accepted
- Date: 2026-10-21
- Deciders: Owner

## Context

[ADR 0021](0021-unity-ci-minute-budget.md) moved Windows and Web Graphics Library (WebGL) GameCI builds off automatic hooks to save Actions minutes, and required local EditMode tests plus **both** Windows and WebGL player builds before merging any Unity-changing pull request. On a fast desktop those player builds still cost meaningful time, while pull-request EditMode Continuous Integration (CI) already compiles the same project scripts for most changes. The portfolio targets Windows and WebGL, but the marginal catch rate of a full Windows player build after a green EditMode run is low compared with WebGL-specific failure modes (linker stripping, `.jslib`, Addressables build target, WebGL `#if` paths).

## Options considered

1. **Keep ADR 0021 as written (EditMode + Windows + WebGL locally every Unity PR):** maximum pre-merge confidence; high time cost on low-risk PRs.
2. **Drop all local player builds; rely on EditMode CI only:** fastest; weak coverage for WebGL packaging and platform-only code.
3. **Selective local builds:** keep EditMode as the default gate; require WebGL (player or scene preview build) only when the change raises WebGL/packaging risk; treat Windows player builds as optional except for milestone or demo cuts.

## Decision

Adopt option 3. This supersedes only the **local pre-merge Windows + WebGL every time** requirement in ADR 0021. ADR 0021’s GameCI trigger policy remains in force: no automatic GameCI on `main`; EditMode on Unity-changing PRs; hosted platform builds manual/on-demand only.

Before the owner authorizes merging a pull request that changes `TransportTycoon2/`:

1. **Always (unless already run once for this PR):** local Unity EditMode tests on the pinned editor version, at most once per PR after merge authorization (same once-per-PR discipline as ADR 0021).
2. **WebGL player or scene WebGL preview build:** required when the PR touches Presentation, plugins / `.jslib`, PlayerSettings, Addressables, WebGL-specific code, packaging/linker files, or other packaging risk. A successful scene WebGL preview build (for example MapPlay or art-pilot preview entry points) counts. Skip for Core-only, content-data-only, docs-only, or pure test changes that do not affect player packaging.
3. **Windows player build:** not required by default after a green EditMode run. Run it when the owner asks, before a milestone/demo cut, or when the change is Windows-packaging-specific.
4. **Skip** all local Unity player builds for documentation- and workflow-only changes (unchanged from ADR 0021).

Do not run these local player builds before opening the PR. Track whether the required subset already ran for the PR; do not repeat when merge approval arrives.

## Consequences

- Most Unity PRs cost EditMode time only locally after authorization.
- WebGL packaging regressions remain gated when Presentation or packaging surfaces change.
- Windows player confidence shifts to occasional/milestone checks and on-demand GameCI when needed.
- Agents must classify each Unity PR against the WebGL-trigger list instead of always building both targets.
