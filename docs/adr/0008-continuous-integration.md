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
- **Job 2, pull requests to `main`:** Unity tests and builds (Windows and WebGL) via GameCI. Needs a Unity license stored as a GitHub secret; Personal license activation steps will be documented when set up.
- **Job 3, later:** deploy the WebGL demo to GitHub Pages (Unity WebGL compression and header handling to be verified).

## Consequences
- Branch protection on `main` requires green checks.
- Unity builds are slow and consume Actions minutes; use caching of the Library folder.
- Secrets handling and license setup are part of GitHub setup.
