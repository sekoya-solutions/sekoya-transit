# ADR 0021: Unity CI minute budget

- Status: Accepted
- Date: 2026-10-20
- Deciders: Owner

## Context

Unity GameCI jobs are expensive on the repository's limited GitHub Actions minute allowance. A cold-cache Windows and WebGL build pair can consume about an hour. ADR 0008 previously scheduled EditMode tests and platform builds on pushes to `main`.

## Options considered

1. **Keep automatic post-merge tests and builds:** immediate main-branch feedback, but consumes substantial Actions minutes on every Unity change.
2. **Disable Unity automation entirely:** avoids hosted runner cost, but loses automated EditMode feedback on Unity changes in pull requests.
3. **Run EditMode tests on Unity-changing pull requests; run platform builds locally before merge and keep hosted platform builds manual:** retains automated pre-merge test feedback without recurring main-branch build costs.

## Decision

Adopt option 3. This supersedes only the Unity workflow trigger choices in [ADR 0008](0008-continuous-integration.md); other CI decisions there remain unchanged.

- Do not run GameCI automatically on pushes to `main`.
- Run Unity EditMode tests on pull requests that change `TransportTycoon2/**`; allow manual dispatch for on-demand validation.
- Run Windows and WebGL builds only through explicit manual workflow dispatch.
- Before merging a pull request that changes the Unity project, run EditMode tests and both platform builds locally with the Unity version pinned by `TransportTycoon2/ProjectSettings/ProjectVersion.txt`.
- Skip local Unity validation for documentation- and workflow-only changes. Keep the independent plain-.NET and Markdown CI jobs unchanged.

The manual local commands are documented in [Unity license for GitHub Actions](../ci/unity-license.md).

## Consequences

- Routine pushes and merges to `main` no longer consume Unity GameCI minutes.
- Unity-affecting pull requests retain automated EditMode coverage, and the final platform-build check moves to the developer's machine before merge.
- Local Windows and WebGL builds still take time, but do not spend hosted Actions minutes. The on-demand GameCI workflow remains available when a local environment is unavailable or a hosted confirmation is needed.
- If Unity code begins depending on files outside `TransportTycoon2/`, revisit the pull-request path filter.
