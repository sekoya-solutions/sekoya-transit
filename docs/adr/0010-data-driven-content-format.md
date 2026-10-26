# ADR 0010: Data-driven content format

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
Cargo, industries, recipes, vehicles, town needs, quests, and balance values must be data-only and easily expandable ([gameplay-systems.md](../specs/gameplay-systems.md)). Core must remain independent from Unity ([ADR 0003](0003-simulation-architecture.md)).

## Options considered
1. **JSON files with JSON Schema validation**, loaded by the Content layer into immutable definition objects.
2. Unity ScriptableObjects: Inspector editing, but tied to the Unity editor, breaking plain `dotnet test`, Core independence, diff readability, and modding.
3. CSV/YAML: CSV is poor for nested data; YAML adds a dependency and ambiguity with no clear gain.

## Decision
Option 1:
- Content lives in JSON files under the game's content folder, one file per definition family (`cargo.json`, `industries.json`, `towns.json`, `economy.json`, `vehicles.json`, and later quest families). `towns.json` also carries demand/growth knobs (`demandPerNeedPerMonth`, `populationPerSatisfiedNeed`) used by Core town supply tracking (Issue #58), plus passenger/mail generation knobs (`passengersPerPopulationUnit`, `mailPerPopulationUnit`, `passengerWaitingCapacity`) for Issue #59. `cargo.json` includes passenger-class entries (`passengers`, `mail`) with manufacturing level 0. `economy.json` carries starting company cash (Issue #61). `vehicles.json` carries road/rail type stats, purchase/upkeep, and year/unlock fields (Issue #145; unlock enforcement in Issue #63).
- JSON Schemas ship with the repository for editor autocompletion and validation.
- A validation test (run in CI) checks schema conformity and **referential integrity** (every referenced cargo, industry, or vehicle exists; recipes are consistent; chains have no cycles).
- Core receives immutable definitions; it never reads files.
- The Unity Content assembly consumes explicitly supplied `TextAsset` inputs using Unity's `JsonUtility`, then creates immutable Core definitions. This uses no external runtime JSON package and applies only to game content; save-game serialization remains a separate decision under [ADR 0009](0009-save-format-and-persistence.md).

## Consequences
- No Inspector editing; mitigated by schemas and clear validation errors.
- Provides a natural path to modding and balancing without recompiling.
- Display strings are never in content files, only localization keys ([ADR 0011](0011-localization.md)).
