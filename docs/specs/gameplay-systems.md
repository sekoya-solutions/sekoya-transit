# Gameplay Systems Specification (V1 focus)

Status: **Draft from product discovery deep-dives (chat history entries 9 and 10). Pending owner review.**
Scope reference: [product-vision.md](product-vision.md). All numeric values below are placeholders to be tuned: every parameter must be data-driven and easily adjustable (see "Tuning principle").

## Tuning principle

All balance values (cargo prices, production rates, vehicle stats, town thresholds, interest rates, quest goals) live in data files, not code. Adding a cargo, industry, vehicle, or need must be possible by adding data only. Content must be easily expandable well beyond the V1 catalog.

## 1. Cargo

- Two classes in V1: **passengers** and **cargo**. Cargo types may be mixed in one vehicle in V1.
- V2: liquid class, and cargo that cannot be mixed.
- Freight cargo has an explicit **manufacturing level** 1–3 (matching its producer industry chain level); passengers use manufacturing level 0. Town needs draw from these levels.
- Cargo has a **fixed price** per unit. Higher-level products are worth more than lower-level ones, and some chains are more valuable than others.
- Towns have a random supply and demand bonus applied to the price.
- V2: distance bonus and transit-time penalty, so that long routes are unprofitable with slow early vehicles.

## 2. Industries and production chains

- V1 catalog: about **5 chains of 3 levels, so about 15 industries** (e.g. wood, planks, furniture; wheat, flour, bread).
- Levels: level 1 = primary (raw), level 2 = secondary, level 3 = final.
- Recipes are **fixed with strict ratios** (e.g. 2 planks + 1 metal = 1 furniture).
- Primary production is **fixed** in V1.
- Level 3 products have no purpose except satisfying town needs.
- The industry map is **fixed during a game in V1** (generated at start, seeded).
- V2: random spawning, buying industries (expensive), industries closing with a delayed warning, production upgrades unlocked when well serviced and bought with money.
- **Player-built factories are V2** (decided in deep-dive C, superseding the earlier V1 intent). In V1 the player builds stations and roads/rails only; the V1 late-game money sink is vehicle unlocks and expansion.

## 3. Stations

- Separate **road stations** and **rail stations** (Issue #55 locks the V1 placement and connection rules below).
- V1: **fixed station size** (1×1 tile); V2: several sizes to accommodate more vehicles.
- V1: **no catchment area**. A station must be **orthogonally adjacent to an industry or to any town building** to load/unload there. A catchment radius may be reconsidered in V2.
- The station tile **is** the connection point for its mode (road graph node or rail graph node). Matching road or rail attaches on an **adjacent** tile, not on the station tile itself.
- **Build does not require** adjacent matching road/rail; the player may place the station first and connect later. Isolated stations are valid but unreachable until connected.
- **Station tile occupancy (both modes):** the tile must be empty of object, road, rail, road station, and rail station. Road and rail may still share non-station tiles with each other.
- **Multimodal hubs:** a road station and a rail station may sit on different tiles that are both orthogonally adjacent to the same town or industry.
- Road/rail connection building must be **very simple** in V1.
- Stations are **stops only** (Issue #132): they hold **no cargo inventory**. Vehicles load from / unload into the unique orthogonally adjacent industry (or town freight is consumed on unload). Map generation keeps industry/town sites at least Chebyshev distance 3 apart so one station cannot attach to two sites.

## 4. Vehicles and routes

- **Orders/routes** between stations.
- **Capacity** is mandatory: it impacts load/unload time and travel costs.
- Loading: by default **load what is available and leave**; an option to **wait for full load** is included in V1 if not too complex.
- **Trains (V1):** buying a train purchases the **locomotive only**. The player adds and sells **wagons** separately. Cargo capacity is `wagonCount × capacityPerWagon` (placeholder: 10 per wagon). The locomotive's maximum wagon count is a model property (placeholder fixed max of 3 until the vehicle catalog). A locomotive with **zero wagons** has capacity 0 and **stays parked** even if it has a route. Selling a wagon while over the new capacity **discards** excess cargo. Map placeholders show the locomotive plus one marker per wagon.
- Road vehicles keep a single placeholder capacity until the shared vehicle catalog lands.
- Roughly **10 vehicle types** at V1 with increasing capacity and/or speed and different costs. The **data-driven road and rail vehicle catalog** is Epic [#11](https://github.com/sekoya-solutions/transport-tycoon-2/issues/11) / Issue [#145](https://github.com/sekoya-solutions/transport-tycoon-2/issues/145) (M5), not M3.
- Unlocking: vehicles **appear by year** and are then **unlocked with money** (Issue [#63](https://github.com/sekoya-solutions/transport-tycoon-2/issues/63)). Each vehicle type has its own unlock cost and the unlock is **permanent**. Newer types are incremental improvements (e.g. a truck with a little more cargo, a train with a little more speed). This is a major progression upgrade, only affordable after accumulating a lot of money.
- V2: depots, servicing/maintenance, aging.

## 5. Economy and finance

- V1 costs: **fixed per-vehicle upkeep per calendar month** and **vehicle purchase price** (data-driven in `economy.json`; placeholder truck / locomotive / wagon until the vehicle catalog). Buy rejects when cash is insufficient. Sell credits **half** the current purchase price (a train refunds half the locomotive plus half a wagon × wagon count). Parked vehicles still pay upkeep. If monthly upkeep exceeds cash, cash **clamps at 0**. **Bankruptcy occurs when cash reaches 0 and ends active gameplay with a simple game-over screen** (Issue [#62](https://github.com/sekoya-solutions/transport-tycoon-2/issues/62)). A route can lose money if unprofitable.
- V2 costs: cost per distance travelled, infrastructure upkeep, and loans with interest until repaid. Loan terms and interest are to be tuned with the whole economy. Roads, rails, and stations stay free in V1.
- No inflation, no subsidies.
- **Score (V1)** = assets value + cash.

## 6. Towns

- Everything is highly tunable via `towns.json` (starting population range, max population, growth tiers). Placeholder numbers: towns start at about 100 to 500 inhabitants and can grow to about 10,000.
- Towns start with about 3 level 1 needs and gain a need roughly every 1,000 inhabitants. Placeholder at 10,000 inhabitants: 6 level 1, 3 level 2, 1 level 3 needs.
- Needs are assigned deterministically per town from freight cargo of the matching manufacturing level (stable prefix when population grows into a higher tier).
- Each need has flat monthly **demand** (`demandPerNeedPerMonth` in `towns.json`) and tracks **supply** (units delivered that month). A need is met when supply ≥ demand; the **growth score** is the count of met needs. At month end the town gains `populationPerSatisfiedNeed` inhabitants per met need (clamped to max population), which can unlock more complex needs. Town unload accepts only needed freight. Vehicles load a cargo only if some stop on their route accepts it (town need or industry input).
- Population growth increases **passenger demand**. Passengers and mail travel **between towns only** in V1 (no internal town transport). Towns periodically generate passenger-class cargo (`passengers` and `mail`) into a capped waiting inventory (`passengersPerPopulationUnit`, `mailPerPopulationUnit`, `passengerWaitingCapacity` in `towns.json`). Vehicles load from the town bag at adjacent stations when the route visits at least two distinct towns, and earn fixed `pricePerUnit` delivering to a different town (origin tracked on the vehicle; same-town unload is refused). Passenger delivery does not credit freight needs.
- V2: shrink penalties for unmet needs.

## 7. Goals and quests

- Sandbox with score.
- **Main quests**: large milestones, e.g. grow a city to 1k / 5k / 10k inhabitants, accumulate 1 / 10 million money, own 10 vehicles, supply 100 products.
- **Random quests**: a bonus way to earn money. Delivery goals to towns (possible city growth bonus), time-limited, **no penalty on failure**.

## 8. Calendar

- Starts on **January 1, 1900**. The simulation uses a **fixed 365-day year with no leap days**. One in-game year is **30 real minutes at normal speed**, derived from the configured simulation tick duration; the displayed date advances by whole days and retains sub-day progress. Pausing advances no ticks and therefore no date; speed multipliers advance the calendar through additional simulation ticks. Vehicle availability is tied to the year, then unlocked with money. The 30-minute pacing remains subject to later playtesting and tuning.

## 9. Controls and UI (V1)

- Mouse and keyboard only. Basic camera: pan in 4 directions, zoom in/out. No minimap in V1 (deferred to V2; see closed issue [#39](https://github.com/sekoya-solutions/transport-tycoon-2/issues/39)).
- MapPlay displays the current calendar date in `YYYY-MM-DD` format and provides X1, X2, and X3 simulation speed controls. X1 is selected by default; the date remains derived from simulation ticks and follows the existing pause and speed behavior.
- Screens: **build toolbar**; **vehicle list** with return on investment (ROI); **industry list** with production and transported amounts; **town list** with needs supplied versus demand plus passenger/mail Generated/Transported; **finance recap**. Town and industry list rows link to the map (select tile, center camera); selecting a town or industry tile opens the matching list and highlights the row. Modern, clean style.

## 10. Save and audio

- One autosave slot plus manual save. The initial versioned save snapshot includes the world seed, simulation tick, generated map channels, and ordered town and industry placements; the calendar date is derived from the tick. Later gameplay state is added through explicit save-format migrations. WebGL persistence to be investigated later. No map sharing.
- Audio: sound effects plus **one ambient music track** in V1 (AI-generated or free asset).

## 11. Map grid

- Maps are square: **128-by-128**, **256-by-256**, or **512-by-512** tiles. V1 terrain is flat.
- Core uses zero-based `(x, y)` grid coordinates and separate flat arrays for terrain type IDs, object/entity IDs, and ownership IDs. The row-major tile ID is `y * width + x`; isometric projection stays in presentation.
- Terrain type ID `0` is the default flat terrain. Object ID and owner ID `-1` mean no object and unowned, respectively; non-negative IDs, including `0`, are valid.
- V1 Core generation composes flat terrain with seeded town and industry placement. Town object IDs follow town placement order; industry IDs follow all town IDs in industry placement order. Each placement routine uses its own fixed random stream, and owner IDs remain unassigned.

## Open topics

- Economy balancing is a **dedicated workshop after the game is built**; until then, random but logical placeholder numbers are used (prices, production rates). Loan sizes and interest are deferred with loans to V2.
- WebGL save persistence: decided as part of the technical decisions.
- Tutorial / onboarding (not requested for V1).
- Technical stack decisions: Unity version, render pipeline, architecture, testing, continuous integration (next, as Architecture Decision Records).
