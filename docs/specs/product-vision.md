# Product Vision and Scope

Status: **Draft from product discovery (chat history entries 6 to 8). Pending owner review.**

## Vision

A Transport Tycoon-style game inspired by Transport Tycoon Deluxe (TTD) and Rise of Industry (first game). Built in Unity as a portfolio piece: playable demo, clean extensible architecture, robust automated regression testing.

## Core loop

Build transport infrastructure, move cargo and passengers along production chains and between towns, earn money per delivery, pay running and infrastructure costs, reinvest to grow.

## Scope by version

| Area | V1 | V2 | V3+ |
| --- | --- | --- | --- |
| Transport modes | Road, rail | Air, water | |
| Rail | Point-to-point tracks, no signals | | |
| Road | Vehicles follow built roads, no collisions | | |
| Terrain | Flat tiles | Slopes, water, bridges, tunnels | Terraforming |
| Vehicles | Orders/routes, capacity affecting load/unload time and costs, running costs | Depots, servicing, aging | |
| Opponents | Single player, no AI | AI opponents | Multiplayer |
| Industries | Production chains, present at start plus random spawning | | |
| Late-game money sink | Vehicle unlocks and expansion | Player-built factories | |
| Towns | See "Towns" below | | |
| Finance | Money, loans, running and infrastructure costs, bankruptcy after a limit of loans (to refine); no inflation, no subsidies | | |
| Goals | Sandbox, score/company value, main quest (milestones), random quests | | |
| Maps | Seeded procedural generation, sizes S/M/L (128/256/512 tiles per side) | | |
| Time | Real-time with pause and speed controls | | |

## Towns (V1 intent)

Towns have populations that need transport inside and outside the town, and random resource needs. They grow slowly when people are transported or needs are satisfied, which brings more people and more complex needs. Exact V1 minimum to be scoped in a dedicated session.

## Presentation and platform

- 2D isometric sprites, modern and clean toolbars and detail windows.
- Art: AI-generated, curated by the owner.
- Audio: sound effects and music in V1.
- Platforms: Windows PC plus a WebGL (browser) demo.
- Meta: save/load, autosave. English only in V1, but the code base must be internationalization (i18n) compatible.

## Architectural constraints

- Simulation separated from presentation, deterministic, with player actions expressed as commands, so AI opponents (V2) and multiplayer (V3+) can be added.
- Must support maps up to 512x512 tiles (S/M/L = 128/256/512).
- Clean architecture, easily expandable for V2/V3, with continuous automated testing against regressions.

## Constraints

- Solo developer, 20 to 40 hours per week. Aspirational target: end of 2026, not a hard deadline.

## Open topics

- Detailed gameplay rules are in [gameplay-systems.md](gameplay-systems.md).
- Unity version, render pipeline, and architecture choices (to be proposed as Architecture Decision Records).
