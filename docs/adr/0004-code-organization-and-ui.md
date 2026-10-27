# ADR 0004: Code organization and UI framework

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
Clean, expandable architecture is a portfolio goal. The simulation must stay independent from Unity ([ADR 0003](0003-simulation-architecture.md)).

## Options considered
1. **Assembly Definitions (asmdef) per layer**, manual composition root, **UI Toolkit** for the UI.
2. A dependency injection framework (e.g. VContainer or Zenject) from the start: more power, more complexity; can be added later if justified.
3. Everything in one assembly: fast to start, no enforced boundaries.
4. UI with Unity UI (uGUI): mature, but UI Toolkit is Unity's forward direction and suits data-heavy list/window UIs.

## Decision
Option 1:
- Layers as asmdefs with one-way dependencies: `Core` (simulation, no UnityEngine) <- `Content` (data definitions/loading) <- `Presentation` (rendering, input, audio) and `UI`; test assemblies per layer. Exact names and structure to be defined when the project is scaffolded.
- Dependencies wired by hand in a single composition root; no DI framework until a concrete need is documented in a new ADR.
- UI built with **UI Toolkit** (UXML/USS), reading state through view models, never mutating simulation state directly (commands only).
- Panels added from Issue #66 on (finance, achievements, contracts, toasts) are separate controllers with plain C# view-models that are tested in EditMode; `MapPlayShell` only wires them in through one host (`GoalsShellPanels`). The new game screen, the in-game seed display, and the game-over New game button (Issue #67) follow the same pattern through `NewGameShellPanels`. Moving the older panels out of `MapPlayShell` is tracked separately.
- All user-facing strings go through a localization layer (i18n-ready, English only in V1).

## Consequences
- Layer violations are caught by the compiler.
- UI Toolkit in WebGL and its font/localization handling must be verified in the prototype.
