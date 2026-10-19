# ADR 0005: Repository layout and Git workflow

- Status: Accepted (folder name amended by [ADR 0016](0016-unity-project-folder-name.md): `TransportTycoon2/` instead of `game/`)
- Date: 2026-10-19
- Deciders: Owner

## Context
The repository holds documentation, continuous integration (CI) configuration, tooling, and a Unity project. Portfolio visibility of the history matters ([AGENTS.md](../../AGENTS.md)).

## Options considered
1. **Unity project in a `game/` subfolder**; repo root holds `docs/`, `AGENTS.md`, `.github/`, tooling.
2. Unity project at the repository root: simpler paths, but the root is cluttered with Unity folders and mixes concerns.

For the workflow: trunk-based development with short-lived branches and pull requests (chosen) versus direct commits to `main` (no review trail) versus Git Flow (too heavy for a solo project).

## Decision
- Layout: Unity project in `game/`; Unity Hub opens `game/`.
- Workflow: trunk-based. Short-lived feature branches, pull requests even when solo, squash-merge into `main`, PRs linked to issues.
- Commit messages follow **Conventional Commits** (`feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`, `ci:`).
- Unity settings: Asset Serialization = Force Text, Version Control = Visible Meta Files; UnityYAMLMerge configured as the merge tool for scene/prefab files.
- Binary assets (images, audio, fonts, models) are tracked with **Git Large File Storage (LFS)**.

## Consequences
- GitHub's free LFS quota is small (1 GB storage and monthly bandwidth at the time of writing); AI-generated art volume must be monitored, with a fallback to a paid data pack or external asset storage if needed.
- CI paths target `game/`.
- Repository initialization, `.gitignore`, `.gitattributes`, and branch protection are done during GitHub setup.
