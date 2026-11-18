# ADR 0033: Headless balance simulation

- Status: Accepted
- Date: 2026-10-29
- Deciders: Owner

## Context

Issue [#73](https://github.com/sekoya-solutions/sekoya-transit/issues/73) asks whether the economy progression is fun: are vehicle unlocks reachable without being bought at once, and do town and industry growth keep giving the player something to do. Answering by playing takes hours per check (one game year is 30 real minutes) and is not repeatable. Every balance value lives in the content JSON ([ADR 0010](0010-data-driven-content-format.md)), and the simulation is deterministic, Unity-free C# ([ADR 0003](0003-simulation-architecture.md)), so the game can be played without Unity.

## Options considered

1. **Spreadsheet or formula model of the economy:** fast to tweak, but it re-implements the rules (loading time, stock caps, need tiers, upkeep) and drifts from the code; it already missed issues the real game shows, such as level 3 goods having no buyer.
2. **Scripted player on the real Core, as a .NET console tool:** plays real games through the same commands as the UI (stations, A* road and rail paths, vehicle purchases, unlocks) on maps generated from the shipped content; every rule is the shipped one. The player is a heuristic, not an optimal or human player.
3. **The same scripted player as a test in continuous integration:** turns balance targets into a gate, but a 20-year game takes about a minute per seed and balance targets are judgement calls the owner tunes, not regressions.

## Decision

Option 2. `dotnet/SekoyaTransit.Balance` is a console project that compiles the Core sources and the Unity content loader (with a two-type `UnityEngine` shim: `TextAsset` and `JsonUtility`), loads the content JSON, plays one game per seed, and prints a yearly table (cash, score, income, fleet, routes, open route opportunities, town populations, needs met, grown industries) and a summary (bankruptcy, first $1M and $10M, unlock waits, years with route work, first level 3 town delivery). It is a developer tool and is not shipped. Continuous integration (CI) builds it and checks its formatting in the .NET job on every pull request (amended 2026-10-29, Issue [#196](https://github.com/sekoya-solutions/sekoya-transit/issues/196)), so a Core or loader change cannot break it unnoticed; CI never runs a simulation. Its use and the progression targets are documented in its [README](../../dotnet/SekoyaTransit.Balance/README.md) and [economy-progression.md](../specs/economy-progression.md).

## Consequences

- Any balance change can be checked in minutes on several seeds, and the results are reproducible (same content, seed and options give the same game).
- The tool reads the real loader, so content that fails to load in Unity fails here too.
- The scripted player is a model of a steady player; its habits (it saves for the oldest available unlock, replaces its whole fleet after an unlock, opens at most one route a month) shape the numbers, so results are guidance, not proof that a human enjoys the game.
- Amended 2026-11-05, Issue [#308](https://github.com/sekoya-solutions/sekoya-transit/issues/308): the "about a minute" estimate in the options above no longer held once fleets grew, at 7 to 23 minutes for a 40-year game, because cargo loading scanned every vehicle for every industry and town on each tick. Loading now goes through the vehicles once per tick. The tool also plays games in parallel (`--parallel`) and hashes the full game state (`--state-hash`), which is how a Core change is proved not to change gameplay.
- Amended 2026-11-08, Issue [#347](https://github.com/sekoya-solutions/sekoya-transit/issues/347): the tool gains a contract-only player with a Markdown report and a one-vehicle income test, both still developer tools outside continuous integration. A small unit test covers the one-vehicle test; it is linked into the Core test project.
- The tool must keep compiling when Core or the loader change; it builds with the same analyzer and warning settings as the other `dotnet/` projects, and the CI .NET job enforces that.
