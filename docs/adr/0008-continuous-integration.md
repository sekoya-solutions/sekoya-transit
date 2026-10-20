# ADR 0008: Continuous integration

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
The repository will be hosted on GitHub ([ADR 0005](0005-repository-layout-and-git-workflow.md)). CI should give fast feedback and prove the project builds and passes tests ([ADR 0007](0007-testing-strategy.md)).

## Options considered
1. **GitHub Actions** with a fast plain-.NET job and a Unity job using GameCI actions.
2. Unity-only CI: slow and license-dependent for every check.
3. External CI services: extra tooling with no benefit for a GitHub-hosted solo project.

## Decision
GitHub Actions:
- **Job 1, every pull request (fast):** `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`, and Markdown lint plus link check on `docs/`.
- **Job 2, pull requests to `main`, pushes to `main`, and manual dispatch (path-filtered):** Unity EditMode tests via GameCI. Pull-request and push runs trigger only when `TransportTycoon2/**` or the Unity workflow changes, so documentation-only changes skip Unity CI; manual dispatch always runs. Requires the `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` GitHub Actions secrets for the Personal license. Setup is documented in [Unity license for GitHub Actions](../ci/unity-license.md); missing secrets fail the Unity job explicitly.
- **Job 3, pushes to `main` and manual dispatch only:** Windows and WebGL builds via GameCI. Platform builds do not block pull requests; manual dispatch remains available to validate them before a merge when needed. Each build has a 90-minute timeout to allow for cold-cache WebGL builds.
- **Job 4, later:** deploy the WebGL demo to GitHub Pages (Unity WebGL compression and header handling to be verified).

## Consequences
- Branch protection on `main` requires green checks.
- Unity builds are slow and consume Actions minutes; use caching of the Library folder and run platform builds outside the pull-request gate.
- Secrets handling and license setup are part of GitHub setup.
