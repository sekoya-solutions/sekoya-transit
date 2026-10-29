# Economy Progression (V1)

Status: **Draft from the economy balancing workshop (Issue [#73](https://github.com/sekoya-solutions/sekoya-transit/issues/73)). Pending owner review.**
Rules live in [gameplay-systems.md](gameplay-systems.md); every number below lives in the content JSON files ([content-format.md](content-format.md)). The numbers are checked with the balance simulation ([ADR 0033](../adr/0033-headless-balance-simulation.md), [tool README](../../dotnet/SekoyaTransit.Balance/README.md)).

## Goals

One game year is 30 real minutes, so a two-hour session covers about four years and the whole vehicle timeline (1900–1926) about 13 hours.

| Phase | Game years | What the player does |
| --- | --- | --- |
| Start | 0–2 | Opens the first truck routes and bus lines; cash dips, then recovers by the end of year 1. Each truck pays for itself in about a year. |
| Growth | 2–8 | Feeds processing industries, serves the town needs that appear as towns grow, adds rail for long freight routes, sees the first unlocks and saves for them. |
| Expansion | 8–20 | Serves level 3 needs, adds vehicles as primary industries grow, replaces old vehicles with unlocked models. |

Targets checked on Small maps (seeds 1–3, 20 game years each):

- No bankruptcy for a steady player.
- Each vehicle unlock is bought 6 months to 3 years after it appears; the last models may stay out of reach.
- Company cash first reaches $1M after year 5 and $10M after year 15.
- A new route opportunity exists, or a new route is opened, in most years up to year 20.
- A town receives its first level 3 good by year 10.

## Levers

| Lever | File | Value | Why |
| --- | --- | --- | --- |
| Distance pay | `economy.json` `deliveryPay` | Price paid at 30 tiles; at least 4 tiles | Long routes and trains are worth building; short hops no longer win by default. |
| Primary industry growth | `economy.json` `industryGrowth` | +25 % output per level after 12 months with at least 60 % shipped; up to level 6 (2.5 × base) | Served industries keep producing more, so routes keep needing vehicles. |
| Vehicle prices | `vehicles.json` | Truck $10,000 and $400 a month; locomotive $20,000 and $400 a month | A route repays its vehicles in about a year instead of six months. |
| Vehicle timeline | `vehicles.json` | Trucks 1904 / 1910 / 1918 / 1926, locomotives 1907 / 1914 / 1922; unlocks $250k to $4M | Unlocks appear during a normal session; each newer model carries more per dollar of price and upkeep than the one before. |
| Passenger and mail generation | `towns.json` | 1 passenger per pulse per 400 inhabitants, 1 mail per 1,000 | Passengers stay a good business but no longer out-earn every freight chain. |
| Town needs | `towns.json` tiers | Level 2 need from 800 inhabitants, level 3 from 3,000 | Level 3 goods find buyers within the first decade. |
| Contract reward | `contracts.json` | 5 × the cargo price per unit | Contracts are worth chasing. |

## Simulation results

Before the workshop, with the old data, the simulated player had $1M by year 3–4 and $10M by year 10, bought every unlock in the month it appeared, and had no new route to build after year 3.

With the shipped data (Small map, 20 years, `dotnet run --project dotnet/SekoyaTransit.Balance -c Release`):

| Check | Seed 1 | Seed 2 | Seed 3 |
| --- | --- | --- | --- |
| Bankruptcy | No | No | No |
| First $1M | 1912 | 1906 | 1912 |
| First $10M | Not reached (score $8.0M) | Not reached (score $7.6M) | Not reached (score $7.4M) |
| Years with new route work | 17 of 20 | 17 of 20 | 17 of 20 |
| First level 3 delivery to a town | 1910 | 1908 | 1907 |
| Wait for `truck_mk2` (1904) | 8 months | 6 months | 8 months |
| Wait for `loco_mk2` (1907) | 5 months | 0 months | 15 months |
| Wait for `truck_mk3` (1910) | 44 months | 31 months | 39 months |
| Wait for `loco_mk3` (1914) | 28 months | 18 months | 24 months |
| Wait for `truck_mk4` (1918) | 7 months | 10 months | 8 months |
| `loco_mk4` (1922), `truck_mk5` (1926) | Not reached | Not reached | Not reached |

Known gaps:

- `loco_mk2` can be bought at once on a rich map, and `truck_mk3` waits up to 3.7 years. The simulated player replaces its whole fleet right after each unlock, which delays the next one; a human who upgrades gradually reaches `truck_mk3` sooner.
- Medium maps are richer (more industries in reach): on seed 1 the player has $1M in 1905 and buys every unlock the month it appears. Per-size tuning is follow-up work.
- The simulated player ends with about 400 vehicles on a Small map, above the 200-vehicle load test of Issue [#70](https://github.com/sekoya-solutions/sekoya-transit/issues/70).
