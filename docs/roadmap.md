# Roadmap

Status: **final version for now. V1 is done (2026-10-29). V2 is iterative, with no planned order or execution plan, so this file holds no plan or next-work pointer: it only lists V2 issues as they are delivered, added in each issue's first implementation commit.**

- Scope per game version: [product-vision.md](specs/product-vision.md).
- Live task status: GitHub [milestones, issues and the Project board](https://github.com/sekoya-solutions/sekoya-transit/issues) are the source of truth.
- Per-issue history: closed issues, merged pull requests and [ADRs](adr/README.md).

## V1 (done 2026-10-29)

| Milestone | Theme | Status |
| --- | --- | --- |
| Phase 0 | Foundations: specs, ADRs, repository and GitHub setup, Unity scaffolding, WebGL technical spike | Done |
| M0 | Foundation | Done |
| M1 | Core skeleton, map generation, content pipeline, save/load | Done |
| M2 | Road transport vertical slice | Done |
| M3 | Rail transport ([Epic #9](https://github.com/sekoya-solutions/sekoya-transit/issues/9)) | Done |
| M4 | Towns and needs ([Epic #10](https://github.com/sekoya-solutions/sekoya-transit/issues/10)) | Done |
| M5 | Finance, achievements, contracts and progression ([Epic #11](https://github.com/sekoya-solutions/sekoya-transit/issues/11)) | Done |
| M6 | Polish, audio and WebGL demo ([Epic #12](https://github.com/sekoya-solutions/sekoya-transit/issues/12)) | Done |

Highlights of the V1 result:

- A deterministic Core (commands, data-driven balance, versioned saves) with road and rail transport, five industry chains, town needs, finance, achievements and contracts.
- A map of 128, 256 or 512 tiles per side, drawn with custom mesh chunks ([ADR 0017](adr/0017-map-rendering-approach-and-performance-budgets.md)) and Kenney CC0 art ([ADR 0032](adr/0032-map-art-from-kenney-cc0-packs.md)).
- Audio, a save/load UI, a new game screen and a balance simulation ([ADR 0033](adr/0033-headless-balance-simulation.md)).
- The project was renamed from "Transport Tycoon 2" to Sekoya Transit ([#198](https://github.com/sekoya-solutions/sekoya-transit/issues/198)).
- The repository is public; the WebGL demo is at `https://sekoya-solutions.github.io/sekoya-transit/` and rebuilds automatically after each game-affecting merge to `main` ([ADR 0034](adr/0034-webgl-demo-on-github-pages.md)).
- README and portfolio case study: [README](../README.md), [portfolio.md](portfolio.md).

## V2 (incremental)

V2 is a stream of individual issues (fixes, then evolutions from the V2 scope in the product vision), created one at a time from the owner's prompts. There is no backlog, order or milestone plan. Delivered so far:

| Area | Issue | Result |
| --- | --- | --- |
| Economy | [#318](https://github.com/sekoya-solutions/sekoya-transit/issues/318) | Cargo prices set by level: $10 per raw good, $20 per processed good, $40 per finished good, passengers and mail $10; contract reward raised from 5 × to 10 × the cargo price |
| Playtest | [#316](https://github.com/sekoya-solutions/sekoya-transit/issues/316) | Cheat keys 1 to 4 (top row) in every build: +1 month, +1 year (replaces F9), +$10,000 recorded as "Cheats" income, and unlock every vehicle type with the year gate lifted (save format 33) |
| Economy | [#314](https://github.com/sekoya-solutions/sekoya-transit/issues/314) | Vehicle upkeep proportional to cargo capacity: a rate per unit per month falling from $10 (MK1) to $6 (MK5), plus a fixed locomotive fee from $200 to $400, so full trains cost less per unit than short ones |
| Progression | [#312](https://github.com/sekoya-solutions/sekoya-transit/issues/312) | Longer contract deadlines: 2, 4 and 6 months for level 1, 2 and 3 cargo, and 12 months for the contracts a new game accepts automatically (data-driven `initialDurationMonths`) |
| Towns | [#310](https://github.com/sekoya-solutions/sekoya-transit/issues/310) | A town need is partly met only from 50 % of its demand; below that it gives no growth |
| Tests and CI | [#308](https://github.com/sekoya-solutions/sekoya-transit/issues/308) | Cargo loading goes through the vehicles once per tick instead of once per industry or town, with gameplay unchanged; the balance tool plays games in parallel and can hash the full game state to prove a Core change leaves gameplay unchanged |
| HUD | [#305](https://github.com/sekoya-solutions/sekoya-transit/issues/305) | Score removed from the top bar (shown only on the game-over screen); panel buttons reordered: Vehicles, Routes, Industries, Towns, Contracts, Finance, Unlocks |
| UI | [#303](https://github.com/sekoya-solutions/sekoya-transit/issues/303) | Contract toasts show the resource icon with its name in a tooltip instead of the name as text; a toast icon can be hovered without blocking the map, and the tooltip hides when its toast expires |
| UI | [#301](https://github.com/sekoya-solutions/sekoya-transit/issues/301) | The shell is revealed only once every load-time text request has finished and its text is on the labels; a failed request cannot block it, and a safety timeout of 10 seconds of running time reveals the shell if loading stalls or throws. Replaces the fixed two-frame wait of #298 |
| UI | [#298](https://github.com/sekoya-solutions/sekoya-transit/issues/298) | The shell is hidden during load and revealed after a fixed two-frame wait, which shortens the placeholder flash but does not prevent it on a slow load (replaced by #301) |
| UI | [#296](https://github.com/sekoya-solutions/sekoya-transit/issues/296) | Finances rework: full-screen window with a table by vehicle type (truck, train, total) and period (last and current month, last and current year), bought, sold, unlocked and contract lines and the total profit; the ledger records money per vehicle mode and per month (save format 32) |
| Transport | [#294](https://github.com/sekoya-solutions/sekoya-transit/issues/294) | Last-month profits of vehicles and routes wait for a full month of data: an entity created mid-month shows "not available yet" instead of a fake loss until the next month closes (save format 31) |
| UI | [#286](https://github.com/sekoya-solutions/sekoya-transit/issues/286) | Vehicles unlock rework: full-screen window with Trucks, Trains, Boats and Planes columns, one card per vehicle (sprite, stats, buy button, greyed when not yet available) and a "Coming soon" teaser for boats and planes |
| Presentation | [#288](https://github.com/sekoya-solutions/sekoya-transit/issues/288) | Vehicle art for tiers MK1 to MK5: trucks, locomotives and wagons (wagons take their locomotive's tier) are drawn with their own sprites on the map and in the Vehicles unlock window; new locomotive type `loco_mk5` (1930) |
| UI | [#290](https://github.com/sekoya-solutions/sekoya-transit/issues/290) | Close cross in the top-right corner of the Vehicles, Industries, Towns, Finance and Contracts side panels, like the wide windows |
| Towns | [#292](https://github.com/sekoya-solutions/sekoya-transit/issues/292) | Town growth reduced to 5 tiers (1,000 / 2,000 / 5,000 / 10,000 inhabitants, up to 3 level 3 needs), growth level shown as 1/5 to 5/5, one new town sprite per tier |
| Presentation | [#284](https://github.com/sekoya-solutions/sekoya-transit/issues/284) | Cash-in sound only for deliveries visible on screen; one floating cash icon (the UI dollar icon) with the total amount rises over the station after each vehicle finishes unloading |
| UI | [#270](https://github.com/sekoya-solutions/sekoya-transit/issues/270) | Toolbar: date at the bottom right instead of the seed (kept as hidden data); all toolbar buttons share one bigger size |
| Transport | [#268](https://github.com/sekoya-solutions/sekoya-transit/issues/268) | Opening a route's station window forces the Select tool and greys out the other tools, with a hint at the bottom of the window |
| Progression | [#266](https://github.com/sekoya-solutions/sekoya-transit/issues/266) | Active contracts box at the top left of the map; the 3 initial contracts are accepted at game start |
| Transport | [#264](https://github.com/sekoya-solutions/sekoya-transit/issues/264) | Clicking an existing station adds it to the open route even when a station build tool is active |
| Building | [#209](https://github.com/sekoya-solutions/sekoya-transit/issues/209) | Click and drag to build straight road and rail lines in one atomic command |
| HUD | [#215](https://github.com/sekoya-solutions/sekoya-transit/issues/215) | Map hint bar removed; top bar regrouped |
| HUD | [#225](https://github.com/sekoya-solutions/sekoya-transit/issues/225) | Pause button before the speed buttons |
| HUD | [#233](https://github.com/sekoya-solutions/sekoya-transit/issues/233) | Full-window panels no longer cover the toolbars |
| HUD | [#237](https://github.com/sekoya-solutions/sekoya-transit/issues/237) | Font sizes as a small set of USS variables |
| HUD | [#252](https://github.com/sekoya-solutions/sekoya-transit/issues/252) | UI scales with the screen size (reference 1920x1080), so browser zoom no longer changes the layout |
| HUD | [#256](https://github.com/sekoya-solutions/sekoya-transit/issues/256) | Select tool icon redrawn as a clean, symmetric pointer |
| HUD | [#239](https://github.com/sekoya-solutions/sekoya-transit/issues/239) | Resources shown as icons, names in tooltips |
| Localization | [#217](https://github.com/sekoya-solutions/sekoya-transit/issues/217) | Only English ships; no "No translation found" in a French browser |
| Art | [#248](https://github.com/sekoya-solutions/sekoya-transit/issues/248) | Original Grok Imagine sprites for towns and the forest chain (forest, sawmill, furniture factory) |
| Art | [#254](https://github.com/sekoya-solutions/sekoya-transit/issues/254) | Original Grok Imagine sprites for the other industry chains (wheat, iron, livestock, sheep); every chain industry now has original art |
| Art | [#260](https://github.com/sekoya-solutions/sekoya-transit/issues/260) | Original Grok Imagine sprites for the truck, locomotive and wagon |
| Art | [#262](https://github.com/sekoya-solutions/sekoya-transit/issues/262) | Original Grok Imagine sprites for the stations, roads and rails; only the generic industry fallback still uses Kenney art |
| Art | [#272](https://github.com/sekoya-solutions/sekoya-transit/issues/272) | Original Grok Imagine ground atlas: weighted terrain variants and soft borders between grass, earth and sand |
| World | [#282](https://github.com/sekoya-solutions/sekoya-transit/issues/282) | Industry growth in 3 tiers (x2 output and stock per tier, 80 % delivered for 12 months in a row) and the industries panel rework with rules text and cards (save format 30) |
| Transport | [#280](https://github.com/sekoya-solutions/sekoya-transit/issues/280) | Vehicle list rework: cards coloured by last month's profit, per-vehicle monthly income, unassigned section, route links instead of "Manage routes" (save format 29) |
| Presentation | [#274](https://github.com/sekoya-solutions/sekoya-transit/issues/274) | Selecting any tile of a town or industry highlights the whole building block |
| Presentation | [#276](https://github.com/sekoya-solutions/sekoya-transit/issues/276) | Stations are named "Road station #X" / "Rail station #X" (numbered from 1) and labelled on the map |
| Progression | [#278](https://github.com/sekoya-solutions/sekoya-transit/issues/278) | Contract deadlines follow the need level: 1, 2 and 3 months for level 1, 2 and 3 cargo (data-driven `durationMonthsByLevel`) |
| World | [#258](https://github.com/sekoya-solutions/sekoya-transit/issues/258) | Darker ground border around each town and industry block, to see its base square |
| World | [#250](https://github.com/sekoya-solutions/sekoya-transit/issues/250) | Towns take 3x3 tiles (room for stations); industries stay 2x2 |
| World | [#219](https://github.com/sekoya-solutions/sekoya-transit/issues/219) | Towns and industries take 2x2 tiles |
| World | [#246](https://github.com/sekoya-solutions/sekoya-transit/issues/246) | Any of a town's or industry's 4 tiles selects it on the map |
| World | [#221](https://github.com/sekoya-solutions/sekoya-transit/issues/221) | At least 10 tiles between towns and industries |
| Towns | [#242](https://github.com/sekoya-solutions/sekoya-transit/issues/242) | Growth from partly and fully met needs per level, towns panel rework, Raw / Processed / Finished goods (save format 28) |
| Towns | [#244](https://github.com/sekoya-solutions/sekoya-transit/issues/244) | Monthly demand per need raised to 500 / 400 / 300 by level |
| Progression | [#223](https://github.com/sekoya-solutions/sekoya-transit/issues/223) | Achievements as bronze, silver and gold tracks in a full-screen window (save format 25) |
| Progression | [#227](https://github.com/sekoya-solutions/sekoya-transit/issues/227) | Contracts rework: proposal, accepted, completed, failed (save format 26) |
| Transport | [#229](https://github.com/sekoya-solutions/sekoya-transit/issues/229) | Named routes with shared stops and vehicles, Routes window (save format 27) |
| Tests and CI | [#231](https://github.com/sekoya-solutions/sekoya-transit/issues/231) | Allocation tests compiled out of Unity instead of ignored |
| Tests and CI | [#235](https://github.com/sekoya-solutions/sekoya-transit/issues/235) | Stale WebGL demo builds cancelled when a newer commit lands |

Known open follow-ups (not scheduled):

- Delete the unreachable pre-27 save migrations.
- Remove the unused selected-vehicle map tint.
- Refresh the unlock timings in [economy-progression.md](specs/economy-progression.md); further balance tuning (for example per map size) re-runs the balance simulation.
- Minimap ([#39](https://github.com/sekoya-solutions/sekoya-transit/issues/39), closed as not planned until V2 is scoped).
- Loans with interest (deferred from V1).
- Higher-quality AI art generation and production art placement ([art-pipeline.md](specs/art-pipeline.md)); the Issue #16 ComfyUI study validated feasibility only.
- V2 and V3+ scope: see the scope table in [product-vision.md](specs/product-vision.md).
