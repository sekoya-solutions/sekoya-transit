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
- Panels added from Issue #66 on (finance, achievements, contracts, toasts) are separate controllers with plain C# view-models that are tested in EditMode; `MapPlayShell` only wires them in through one host (`GoalsShellPanels`). The new game screen, the in-game seed display, and the game-over New game button (Issue #67) follow the same pattern through `NewGameShellPanels`. The older vehicles (with buy options and the route editor), unlocks, industries, and towns panels follow it too (Issue #178), through `ListShellPanels`, so `MapPlayShell` keeps only the HUD, toolbar, and speed controls and wires the three hosts in. The HUD is a top bar (cash, score, seed, date, speed controls, and the buttons of every list and goal panel; Issue #181), the build toolbar has one Delete tool in place of the four demolish tools, and route editing and vehicle buying follow the selected station (`VehicleRouteEditRules`): without a selected station the vehicles list shows every vehicle and none can be edited; with a station selected it lists only vehicles of the station's kind that are parked or routed through that station (Issue #193 preview feedback), and only that kind can be bought. Selecting a station of the other kind unselects the vehicle being edited.
- Icon-only buttons (Issue #182) keep their localized name and shortcut in the standard `tooltip` property, which `ShellTooltip` shows at runtime (UI Toolkit only displays tooltips in the Editor). Rows that show cargo use `TextLine`: plain text plus cargo icon pieces built from the localized format string, so translations keep their word order, and drawn by `TextLineView`. Cargo icons are resolved by cargo id through the generated `CargoIconSet` asset with a generic fallback ([ADR 0031](0031-ui-icons-as-hand-authored-svg.md)).
- All user-facing strings go through a localization layer (i18n-ready, English only in V1).

## Consequences
- Layer violations are caught by the compiler.
- UI Toolkit in WebGL and its font/localization handling must be verified in the prototype.
