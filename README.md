# Sekoya Transit

A transport and logistics tycoon game built with Unity, inspired by Transport Tycoon Deluxe and Rise of Industry. A personal portfolio project: deterministic C# simulation, clean architecture, automated testing, and a WebGL demo.

**[Play the demo in your browser](https://sekoya-solutions.github.io/sekoya-transit/)** (WebGL, desktop browser recommended; about 25 MB download).

Status: V1 is done: the vertical slice is playable end to end (Milestones M0–M6 complete). V2 continues as incremental issues, fixes and evolutions. See the [roadmap](docs/roadmap.md).

## Screenshots

![The map of a new game: a town, two industries, and the toolbars](docs/media/map-overview.png)

![A road route: a truck carrying wool between a sheep farm and a town, with the Vehicles and Route panel](docs/media/road-route.png)

![A train on a rail line between two towns, with the Contracts panel open](docs/media/rail-contracts.png)

![The new game screen with seed and map size](docs/media/new-game.png)

## What you can do

- Start a seeded procedural map in three sizes (128, 256 or 512 tiles per side).
- Build roads, rails and stations; buy trucks and trains and give them routes.
- Feed production chains (industries) and keep growing towns supplied with passengers, mail and goods.
- Manage money and running costs, unlock vehicles by year, complete contracts and achievements, and avoid bankruptcy.
- Save, load and continue; the game is pausable with speed controls and has sound effects and a music track.

## Engineering highlights

- **Deterministic Core**: the simulation is plain C# with no Unity dependency, driven by player commands, and verified by state hashes ([ADR 0003](docs/adr/0003-simulation-architecture.md), [ADR 0025](docs/adr/0025-deterministic-state-hash-format.md)).
- **Data-driven content**: vehicles, industries, economy, contracts and achievements live in JSON ([ADR 0010](docs/adr/0010-data-driven-content-format.md)).
- **Tested and gated**: more than 1,000 automated tests, a headless balance simulation, and CI on every pull request ([ADR 0007](docs/adr/0007-testing-strategy.md), [ADR 0008](docs/adr/0008-continuous-integration.md), [ADR 0033](docs/adr/0033-headless-balance-simulation.md)).
- **Performance budgets**: custom mesh-chunk map rendering holds a 512x512 map at a steady frame rate ([ADR 0017](docs/adr/0017-map-rendering-approach-and-performance-budgets.md)).
- **Documented decisions**: 34 Architecture Decision Records (ADRs) explain every significant technical choice.

The full case study, including how AI assists the workflow, is in [docs/portfolio.md](docs/portfolio.md).

## Build and test

Requirements: the .NET SDK for the Core tests, and Unity 6.3 LTS (see [ADR 0001](docs/adr/0001-unity-version.md) and [ADR 0015](docs/adr/0015-unity-editor-version-policy.md)) to open the game.

```bash
dotnet test dotnet/SekoyaTransit.Core.Tests
dotnet format dotnet/SekoyaTransit.Core.Tests --verify-no-changes
```

Open the `SekoyaTransit/` folder in Unity Hub to run the game or the Unity EditMode tests.

## Documentation

- [Portfolio write-up](docs/portfolio.md)
- [Product vision and scope](docs/specs/product-vision.md)
- [Gameplay systems](docs/specs/gameplay-systems.md)
- [Content format and validation](docs/specs/content-format.md)
- [Architecture Decision Records](docs/adr/README.md)
- [Roadmap](docs/roadmap.md)
- [Local chat history process](docs/chat-history/README.md) (logs are gitignored) and [agent instructions](AGENTS.md)

## Repository layout

- `docs/`: specifications, decisions, roadmap
- `dotnet/`: the plain .NET Core tests and the balance simulation tool
- `SekoyaTransit/`: the Unity project (Unity 6.3 LTS, 2D URP)
- `.github/`: workflows and templates

## License

All rights reserved: the source is public to read, but it may not be used, copied, modified, or distributed without permission. See [LICENSE](LICENSE). Third-party assets keep their own licenses (all CC0 1.0); they are listed in [CREDITS.md](CREDITS.md).
