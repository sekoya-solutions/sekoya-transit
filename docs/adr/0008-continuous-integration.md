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
- **Job 1, every pull request (fast):** `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` for the Core tests, a build and format check of the balance simulation tool ([ADR 0033](0033-headless-balance-simulation.md); no simulation is run), and Markdown lint plus link check on `docs/`.
- **Job 2, pushes to `main` and manual dispatch (path-filtered):** Unity EditMode tests via GameCI. Automatic runs trigger only when `TransportTycoon2/**` or `.github/workflows/unity.yml` changes, so documentation-only merges skip Unity CI; manual dispatch always runs. EditMode does **not** run on pull requests ([ADR 0021](0021-unity-ci-minute-budget.md)). Requires the `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` GitHub Actions secrets for the Personal license. Setup is documented in [Unity license for GitHub Actions](../ci/unity-license.md); missing secrets fail the Unity job explicitly.
- **Job 3, manual dispatch only:** Windows and WebGL builds via GameCI. Platform builds never run automatically on pull requests or pushes to `main` ([ADR 0021](0021-unity-ci-minute-budget.md)). Each build has a 90-minute timeout to allow for cold-cache WebGL builds.
- **Balance simulation, pushes to `main` and manual dispatch (path-filtered):** the balance tool plays every map size and reports the economy progression targets in the run summary, without failing on a missed target ([ADR 0033](0033-headless-balance-simulation.md)). Automatic runs trigger only when Core, the content loader, the content data, the tool or its workflow change.
- **Job 4, later:** deploy the WebGL demo to GitHub Pages (Unity WebGL compression and header handling to be verified).

Unity trigger details and local pre-PR EditMode / selective WebGL rules are owned by [ADR 0021](0021-unity-ci-minute-budget.md) and [ADR 0030](0030-selective-local-unity-platform-builds.md).

## Consequences
- Branch protection on `main` requires green checks for the jobs that actually run on the event (plain-.NET and Markdown on PRs; EditMode on qualifying `main` pushes).
- Unity platform builds are slow and consume Actions minutes; use caching of the Library folder and keep them on-demand only.
- Secrets handling and license setup are part of GitHub setup.
