# Balance simulation

A headless tool that plays the game on the real Core with the shipped content and a scripted player, to check the economy progression ([ADR 0033](../../docs/adr/0033-headless-balance-simulation.md), targets and results in [economy-progression.md](../../docs/specs/economy-progression.md)). It is a developer tool and is not shipped. Continuous integration (CI) builds it and checks its formatting on every pull request; after a merge to `main` that can change the economy, the `Balance simulation` workflow plays all three map sizes and shows the targets as pass / fail tables in the run summary (`--markdown` and `--overview` below).

## Run

From the repository root:

```bash
dotnet run --project dotnet/TransportTycoon.Balance -c Release
```

By default it plays Small maps with seeds 1, 2 and 3 for 20 game years each, on parallel threads (about two minutes on a desktop; `--verbose` plays them one after the other). Options:

| Option | Default | Meaning |
| --- | --- | --- |
| `--data <folder>` | the shipped `TransportTycoon2/Assets/_Project/Content/Data` | Content JSON to play with; copy the folder to try other values. |
| `--size small\|medium\|large` | `small` | Map size. |
| `--seeds 1,2,3` | `1,2,3` | One game per seed. |
| `--years <n>` | `20` | Game years per game. |
| `--no-rail` | rail on | Road vehicles only. |
| `--rail-min-distance <tiles>` | `35` | Freight routes at least this long are served by rail. |
| `--max-route-distance <tiles>` | `70` | Longest route the player considers. |
| `--routes-per-month <n>` | `1` | New routes opened per game month at most (human building pace). |
| `--no-replace` | replace on | Keep old vehicles after an unlock. |
| `--markdown <file>` | none | Write the progression targets as a Markdown pass / fail table, one column per seed. |
| `--overview <file>` | none | Write one Markdown table row (map, seeds, checks passed, failed, bankruptcies) for a recap of several map sizes. |
| `--verbose` | off | Print every route, unlock and rejected command, and the routes by income. |

## What the scripted player does

Once per game month, through the same commands as the game UI:

1. Saves for the oldest available locked vehicle (while saving it holds back half of the unlock cost from spending) and buys the unlock when cash covers the cost plus a reserve of three months of upkeep.
2. Replaces vehicles that the best available type outperforms by 15 % or more, while that leaves a margin.
3. Adds a vehicle to each route whose source has cargo piling up (25 units of freight or 35 passengers waiting).
4. Opens at most one new route: from a producing industry (primary, or processing once it is fed) to the industry that consumes its output, or to a town that needs it and gets no supply yet; and passenger and mail lines between nearby towns. It builds the stations and an A* road or rail path; freight routes of 35 tiles or more use rail.

## Output

For each game: a yearly table (cash, score, income including contract bonuses, road and rail fleet, routes and new routes, vehicles added, open route opportunities, town population minimum / average / maximum, towns over 2,000 and 5,000 inhabitants, share of town needs met, grown industries), the unlocks (year available, cost, cash when it appeared, unlock date, wait), and a summary: bankruptcy, first $1M and $10M, years with new route work, and the first level 3 delivery to a town.
