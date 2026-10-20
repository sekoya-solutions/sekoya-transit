# ADR 0019: Unity package selection

- Status: Accepted
- Date: 2026-10-20
- Deciders: Owner

## Context
The Unity 2D URP template installed packages for multiple workflows that are not all part of the accepted architecture. Extra direct dependencies increase project surface area and should have a current or planned use. Unity-generated package manifests must be changed through Unity Package Manager rather than edited by hand.

## Options considered
1. Keep every template package: avoids removal work but retains unrelated tooling and dependencies.
2. Remove packages that have no current or accepted near-term use, retain architecture/spike dependencies, and defer art-pipeline tools until that workflow is decided.
3. Replace Unity's built-in capabilities with third-party packages: no current requirement justifies additional dependencies.

## Decision
Choose option 2:
- Remove `com.unity.multiplayer.center` (no current multiplayer integration; future AI and multiplayer requirements do not require this setup tool), `com.unity.timeline` (no approved timeline-driven feature), `com.unity.visualscripting` (the project uses code and explicit assembly boundaries), and `com.unity.2d.tilemap.extras` (no use in the project; the accepted map renderer is custom mesh chunks, while the feasibility spike uses built-in Tilemap).
- Retain packages needed by the accepted URP, UI Toolkit, Input System, Localization, and testing choices, as well as dependencies used by the existing WebGL feasibility spike.
- Keep `com.unity.ugui` in the resolved package set because the current URP core package declares it as a dependency; this does not change the project's UI Toolkit decision.
- Defer removal or adoption of art authoring/import packages (including Aseprite, PSD, 2D Animation, and SpriteShape packages) until the art pipeline is decided.
- Add, remove, or update packages through Unity Package Manager and commit the Unity-generated manifest and lockfile changes.

## Consequences
- The project no longer directly depends on the four unused template packages listed above.
- Some art-pipeline packages remain temporarily; their presence is not an approval of a specific asset workflow.
- Add packages only when an approved project requirement needs them, and validate package resolution and Unity compilation after dependency changes.
- Unity's Editor removes the previously committed Addressables linker file during project refresh; a successful player build regenerates it. Treat it as generated build output rather than hand-maintained source.
