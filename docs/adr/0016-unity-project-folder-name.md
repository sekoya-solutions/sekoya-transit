# ADR 0016: Unity project folder name

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
[ADR 0005](0005-repository-layout-and-git-workflow.md) placed the Unity project in a subfolder named `game/`. The owner pointed out that a generic name would be identical across all of their projects. Unity Hub creates the project folder from the project name.

## Options considered
1. Keep `game/`: generic, identical across projects.
2. **Name the folder after the game: `TransportTycoon2/`** (no spaces).
3. Unity project at the repository root: no subfolder, but the root is cluttered with Unity folders and Hub cannot create a project in a non-empty folder.

## Decision
Option 2. The Unity project lives in `TransportTycoon2/` at the repository root. This amends only the folder name in ADR 0005; its other decisions stand.

## Consequences
- Repository docs, CI paths, and agent instructions refer to `TransportTycoon2/`.
- The in-game product name is configured separately in Player Settings.
