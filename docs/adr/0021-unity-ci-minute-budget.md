# ADR 0021: Unity CI minute budget

- Status: Accepted
- Date: 2026-10-20
- Deciders: Owner (amended 2026-10-22: EditMode on `main` only; no PR EditMode)

## Context

Unity GameCI jobs are expensive on the repository's limited GitHub Actions minute allowance. A cold-cache Windows and WebGL build pair can consume about an hour. EditMode tests are cheaper than platform builds but still cost roughly ten or more minutes per run and block pull-request review when required on every push. ADR 0008 previously scheduled EditMode tests and platform builds on pushes to `main`. An earlier form of this ADR moved EditMode to Unity-changing pull requests and disabled automatic GameCI on `main` entirely; the owner later clarified that the minute-budget concern was aimed at **platform builds**, not EditMode, and that pull-request EditMode wait time was the main pain.

## Options considered

1. **Keep automatic post-merge platform builds:** immediate main-branch packaging feedback, but consumes substantial Actions minutes on every Unity change.
2. **Disable Unity automation entirely:** avoids hosted runner cost, but loses any independent GameCI EditMode signal.
3. **EditMode on Unity-changing pull requests; platform builds manual only:** independent pre-merge hosted EditMode, but duplicates local EditMode already run before opening PRs and adds ten-plus minutes of PR wait per push.
4. **(2026-10-22) EditMode on pushes to `main` only (path-filtered); no PR EditMode; platform builds manual only; local EditMode required before opening Unity-touching PRs:** removes PR wait; keeps a post-merge Linux GameCI audit; platform builds stay off automatic hooks.

## Decision

Adopt option 4. This supersedes the Unity workflow trigger choices in [ADR 0008](0008-continuous-integration.md) and the earlier “EditMode on PRs / no GameCI on `main`” wording of this ADR. Other CI decisions in ADR 0008 remain unchanged except where this ADR and [ADR 0030](0030-selective-local-unity-platform-builds.md) say otherwise.

- Run Unity EditMode tests automatically on pushes to `main` that change `TransportTycoon2/**` or `.github/workflows/unity.yml`.
- Do **not** run Unity EditMode automatically on pull requests.
- Keep `workflow_dispatch` so hosted EditMode (and platform builds) can run on demand.
- Never run Windows or WebGL GameCI builds automatically on pull requests or on pushes to `main`; those remain manual dispatch only.
- Before opening a pull request that changes `TransportTycoon2/`, run EditMode locally with the Unity version pinned by `TransportTycoon2/ProjectSettings/ProjectVersion.txt` (see [ADR 0030](0030-selective-local-unity-platform-builds.md) for merge-time player-build rules).
- Skip local Unity validation for documentation- and workflow-only changes. Keep the independent plain-.NET and Markdown CI jobs unchanged.

The manual local commands are documented in [Unity license for GitHub Actions](../ci/unity-license.md).

## Consequences

- Pull requests no longer wait on hosted EditMode; review can proceed after plain-.NET and Markdown checks.
- Each Unity-affecting merge to `main` still gets one independent Linux GameCI EditMode run as a post-merge audit.
- Platform builds never burn Actions minutes unless someone dispatches the workflow.
- Agents and developers must treat local EditMode as the pre-PR gate; skipping it can land a failure that only appears after merge.
- If Unity code begins depending on files outside `TransportTycoon2/`, revisit the path filters.
