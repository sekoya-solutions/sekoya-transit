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
| Cargo prices | `cargo.json` | Issue [#336](https://github.com/sekoya-solutions/sekoya-transit/issues/336): $5 per raw good, $10 per processed good, $20 per finished good; passengers and mail $5 (half of the Issue [#318](https://github.com/sekoya-solutions/sekoya-transit/issues/318) prices) | No chain pays more than another at the same level; each processing step doubles the value. |
| Starting cash | `economy.json` `startingCash` | $500,000 (Issue [#336](https://github.com/sekoya-solutions/sekoya-transit/issues/336); $200,000 from Issue [#334](https://github.com/sekoya-solutions/sekoya-transit/issues/334), $100,000 before) | With half cargo pay, the first contract routes never wait for money in the simulation. |
| Distance pay | `economy.json` `deliveryPay` | Issue [#342](https://github.com/sekoya-solutions/sekoya-transit/issues/342): 7 % of the price per delivery plus 3 % per tile (3.33 % per tile and no start before); at least 4 tiles | Every tile adds the same amount and a vehicle on a longer route earns more per year. The start must stay below the percent per tile × capacity × speed of every vehicle (2.5 × the percent per tile for the Mk1 truck), and that limit caps how much long routes can lose: on a one-truck test, a 180-tile route earns 8.8 % less than under the old rule and a 30-tile route 3 % less. |
| Primary industry growth | `economy.json` `industryGrowth` | Output and stock x2 per level after 12 months in a row with at least 80 % delivered; 5 levels (x1, x2, x4, x8, x16; Issue [#328](https://github.com/sekoya-solutions/sekoya-transit/issues/328)) | Served industries keep producing more, so routes keep needing vehicles. |
| Vehicle prices | `vehicles.json` | Mk1 truck $10,000, Mk1 locomotive $20,000, wagon $4,000; Issue [#336](https://github.com/sekoya-solutions/sekoya-transit/issues/336): trucks Mk2 to Mk5 $20,000 / $40,000 / $70,000 / $110,000, locomotives Mk2 to Mk5 $50,000 / $110,000 / $220,000 / $380,000 | Newer models cost more to buy per unit of capacity, so a large late fleet burns cash. For Mk1: A route repays its vehicles in about a year instead of six months (measured with the upkeep in place before Issue [#314](https://github.com/sekoya-solutions/sekoya-transit/issues/314)). |
| Vehicle upkeep | `vehicles.json` | Issue #314: a rate per unit of capacity from $10 (MK1) to $6 (MK5) a month, plus a locomotive fee from $200 (MK1) to $400 (MK5); `truck_mk1` costs $100 a month, a `loco_mk1` train $320 with 1 wagon and $680 with 4 | Much lower than before (`truck_mk1` paid $400) so money comes in faster; newer models and fuller trains cost less per unit. |
| Vehicle timeline | `vehicles.json` | Trucks 1904 / 1910 / 1918 / 1926, locomotives 1907 / 1914 / 1922 / 1930; Issue [#336](https://github.com/sekoya-solutions/sekoya-transit/issues/336) unlocks: trucks Mk2 to Mk5 $1M / $5M / $25M / $45M, locomotives Mk2 to Mk5 $2.5M / $12M / $40M / $60M | Unlocks need some saving: a contract-driven simulated player (Small, 10 towns, seeds 1 to 3) bought them 0 to 24 months after they appeared. Each newer model carries more per vehicle and costs less upkeep per unit of capacity than the one before. |
| Passenger and mail generation | `towns.json` | 1 passenger per pulse per 400 inhabitants, 1 mail per 1,000 | Passengers stay a good business but no longer out-earn every freight chain. |
| Town needs | `towns.json` tiers | First level 2 need at 1,000 inhabitants, first level 3 need at 2,000 | Level 3 goods find buyers within the first decade. |
| Contract reward | `contracts.json` | 10 × the cargo price per unit (5 × before Issue #318) | Contracts are worth chasing. |

## Simulation results

Before the workshop, with the old data, the simulated player had $1M by year 3–4 and $10M by year 10, bought every unlock in the month it appeared, and had no new route to build after year 3.

With the data shipped before Issue [#314](https://github.com/sekoya-solutions/sekoya-transit/issues/314) (Small map, 20 years, `dotnet run --project dotnet/SekoyaTransit.Balance -c Release`). Issue #314 cut vehicle upkeep on purpose to speed up earnings, and Issue #318 set cargo prices by level and doubled the contract reward; neither re-ran the simulation, so these figures are out of date:

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
| `loco_mk4` (1922), `truck_mk5` (1926), `loco_mk5` (1930) | Not reached | Not reached | Not reached |

Known gaps:

- `loco_mk2` can be bought at once on a rich map, and `truck_mk3` waits up to 3.7 years. The simulated player replaces its whole fleet right after each unlock, which delays the next one; a human who upgrades gradually reaches `truck_mk3` sooner.
- Medium maps are richer (more industries in reach): on seed 1 the player has $1M in 1905 and buys every unlock the month it appears. Per-size tuning is follow-up work.
- The simulated player ends with about 400 vehicles on a Small map, above the 200-vehicle load test of Issue [#70](https://github.com/sekoya-solutions/sekoya-transit/issues/70).
