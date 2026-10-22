# ADR 0030: Selective local Unity platform builds

- Status: Accepted
- Date: 2026-10-21
- Deciders: Owner (amended 2026-10-22: trust PR EditMode CI)

## Context

[ADR 0021](0021-unity-ci-minute-budget.md) moved Windows and Web Graphics Library (WebGL) GameCI builds off automatic hooks to save Actions minutes, and required local EditMode tests plus **both** Windows and WebGL player builds before merging any Unity-changing pull request. On a fast desktop those player builds still cost meaningful time, while pull-request EditMode Continuous Integration (CI) already compiles the same project scripts for most changes. The portfolio targets Windows and WebGL, but the marginal catch rate of a full Windows player build after a green EditMode run is low compared with WebGL-specific failure modes (linker stripping, `.jslib`, Addressables build target, WebGL `#if` paths).

A later amendment (2026-10-22) dropped the mandatory **local** EditMode re-run after merge authorization when GitHub Actions EditMode already passed on the pull request head: that local pass duplicated CI on the same pinned editor/tests and burned machine time without extra signal.

## Options considered

1. **Keep ADR 0021 as written (EditMode + Windows + WebGL locally every Unity PR):** maximum pre-merge confidence; high time cost on low-risk PRs.
2. **Drop all local player builds; rely on EditMode CI only:** fastest; weak coverage for WebGL packaging and platform-only code.
3. **Selective local builds:** keep EditMode as the default gate; require WebGL (player or scene preview build) only when the change raises WebGL/packaging risk; treat Windows player builds as optional except for milestone or demo cuts.
4. **(2026-10-22) Trust PR EditMode CI; no local EditMode re-run when CI is green on the head to merge:** avoids duplicate EditMode; still allows optional local EditMode when diagnosing CI gaps or CI did not run.

## Decision

Adopt option 3, with option 4 for EditMode. This supersedes only the **local pre-merge Windows + WebGL every time** requirement in ADR 0021, and amends the former “always local EditMode after merge authorization” gate. ADR 0021’s GameCI trigger policy remains in force: no automatic GameCI on `main`; EditMode on Unity-changing PRs; hosted platform builds manual/on-demand only.

Before the owner authorizes merging a pull request that changes `TransportTycoon2/`:

1. **EditMode:** rely on the pull request’s Unity EditMode CI job when it passed on the commit being merged. Do **not** run a second local EditMode pass solely because merge was authorized. Run local EditMode only if CI EditMode did not run, failed and needs local diagnosis, or the owner asks for it.
2. **WebGL player or scene WebGL preview build:** required when the PR touches Presentation, plugins / `.jslib`, PlayerSettings, Addressables, WebGL-specific code, packaging/linker files, or other packaging risk. A successful scene WebGL preview build (for example MapPlay or art-pilot preview entry points) counts. Skip for Core-only, content-data-only, docs-only, or pure test changes that do not affect player packaging. At most once per PR; skip at merge time if already run for that PR.
3. **Windows player build:** not required by default after a green EditMode run. Run it when the owner asks, before a milestone/demo cut, or when the change is Windows-packaging-specific.
4. **Skip** all local Unity player builds for documentation- and workflow-only changes (unchanged from ADR 0021).

Do not run these local player builds before opening the PR. Track whether the required WebGL subset already ran for the PR; do not repeat when merge approval arrives.

## Consequences

- Most Unity PRs need no local Unity work after authorization if EditMode CI is green and WebGL risk does not apply.
- WebGL packaging regressions remain gated when Presentation or packaging surfaces change.
- Windows player confidence shifts to occasional/milestone checks and on-demand GameCI when needed.
- Agents must classify each Unity PR against the WebGL-trigger list instead of always building both targets, and must not duplicate green PR EditMode CI locally at merge time.
