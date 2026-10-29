# ADR 0016: Unity project folder name

- Status: Accepted (amended 2026-10-29, Issue #198)
- Date: 2026-10-19
- Deciders: Owner

## Context

[ADR 0005](0005-repository-layout-and-git-workflow.md) placed the Unity project in a subfolder named `game/`. The owner pointed out that a generic name would be identical across all of their projects. Unity Hub creates the project folder from the project name.

## Options considered

1. Keep `game/`: generic, identical across projects.
2. **Name the folder after the game** (no spaces).
3. Unity project at the repository root: no subfolder, but the root is cluttered with Unity folders and Hub cannot create a project in a non-empty folder.

## Decision

Option 2. The Unity project lives in `SekoyaTransit/` at the repository root. This amends only the folder name in ADR 0005; its other decisions stand.

## Consequences

- Repository docs, CI paths, and agent instructions refer to `SekoyaTransit/`.
- The in-game product name is configured separately in Player Settings.

## Amendment 2026-10-29: project renamed to Sekoya Transit (Issue #198)

The folder was first named `TransportTycoon2/` after the working title "Transport Tycoon 2". That title reuses the "Transport Tycoon" trademark, so before the repository or demo goes public the project was renamed to **Sekoya Transit** (the owner's company brand). The folder moved to `SekoyaTransit/` with history kept (`git mv`), along with the code namespaces and assemblies (`TransportTycoon.*` to `SekoyaTransit.*`), the .NET projects under `dotnet/`, the GitHub repository (`sekoya-solutions/sekoya-transit`), and the Player Settings product and company names (`Sekoya Transit`, `Sekoya Solutions`). Saves store no project or type names, so the save format is unchanged; saves and preferences from before the rename stay under the old persistent data path and are not carried over.
