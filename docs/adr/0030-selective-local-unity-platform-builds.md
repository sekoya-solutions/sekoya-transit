# ADR 0030: Selective local Unity platform builds

- Status: Accepted
- Date: 2026-10-21
- Deciders: Owner (amended 2026-10-22: local EditMode is the PR gate; hosted EditMode on `main` only)

## Context

[ADR 0021](0021-unity-ci-minute-budget.md) keeps Windows and Web Graphics Library (WebGL) GameCI builds off automatic hooks to save Actions minutes. An earlier amendment trusted pull-request EditMode CI and skipped a local EditMode re-run at merge time. After moving hosted EditMode off pull requests (ADR 0021 amended 2026-10-22), local EditMode is again the pre-merge gate; hosted EditMode on `main` is a post-merge audit only.

On a fast desktop, full Windows and WebGL player builds still cost meaningful time. The portfolio targets Windows and WebGL, but the marginal catch rate of a full Windows player build after a green EditMode run is low compared with WebGL-specific failure modes (linker stripping, `.jslib`, Addressables build target, WebGL `#if` paths).

## Options considered

1. **EditMode + Windows + WebGL locally every Unity PR:** maximum pre-merge confidence; high time cost on low-risk PRs.
2. **Drop all local player builds; rely on hosted EditMode only:** fastest; weak coverage for WebGL packaging and platform-only code; insufficient once EditMode leaves the PR gate.
3. **Selective local builds:** require local EditMode for Unity-touching PRs; require WebGL (player or scene preview build) only when the change raises WebGL/packaging risk; treat Windows player builds as optional except for milestone or demo cuts.
4. **(superseded) Trust PR EditMode CI; no local EditMode when CI is green:** removed when PR EditMode CI was dropped (ADR 0021).

## Decision

Adopt option 3. This supersedes only the **local pre-merge Windows + WebGL every time** requirement formerly implied by ADR 0021, and replaces the “trust PR EditMode CI” amendment. ADR 0021’s GameCI trigger policy remains in force: EditMode on qualifying pushes to `main` only; no PR EditMode; hosted platform builds manual/on-demand only.

For a pull request that changes `TransportTycoon2/`:

1. **EditMode:** run locally on the commit head before opening the PR (and again before merge if the head moved without a new local pass). Hosted EditMode on `main` after merge is an audit, not a substitute for the local pre-PR gate.
2. **WebGL player or scene WebGL preview build:** required when the PR touches Presentation, plugins / `.jslib`, PlayerSettings, Addressables, WebGL-specific code, packaging/linker files, or other packaging risk. A successful scene WebGL preview build (for example MapPlay or art-pilot preview entry points) counts. Skip for Core-only, content-data-only, docs-only, or pure test changes that do not affect player packaging. At most once per PR; skip at merge time if already run for that PR.
3. **Windows player build:** not required by default after a green EditMode run. Run it when the owner asks, before a milestone/demo cut, or when the change is Windows-packaging-specific.
4. **Skip** all local Unity player builds for documentation- and workflow-only changes (unchanged from ADR 0021).

Do not run local **player** builds before opening the PR unless the owner asks for an early preview. Track whether the required WebGL subset already ran for the PR; do not repeat when merge approval arrives.

## Consequences

- Unity-affecting PRs stay reviewable without waiting on GameCI EditMode.
- WebGL packaging regressions remain gated when Presentation or packaging surfaces change.
- Windows player confidence shifts to occasional/milestone checks and on-demand GameCI when needed.
- Agents must run local EditMode before opening Unity-touching PRs, classify each PR against the WebGL-trigger list, and must not assume a green PR Unity check exists.
