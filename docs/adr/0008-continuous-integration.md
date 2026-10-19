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
- **Job 2, pull requests to `main` and pushes to `main`:** Unity EditMode tests and Windows/WebGL builds via GameCI. Requires the `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` GitHub Actions secrets for the Personal license. Setup is documented in [Unity license for GitHub Actions](../ci/unity-license.md); missing secrets fail the Unity job explicitly.
- **Job 3, later:** deploy the WebGL demo to GitHub Pages (Unity WebGL compression and header handling to be verified).

## Consequences
- Branch protection on `main` requires green checks.
- Unity builds are slow and consume Actions minutes; use caching of the Library folder.
- Secrets handling and license setup are part of GitHub setup.
