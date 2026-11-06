# Balance simulation

A headless tool that plays the game on the real Core with the shipped content and a scripted player, to check the economy progression ([ADR 0033](../../docs/adr/0033-headless-balance-simulation.md), targets and results in [economy-progression.md](../../docs/specs/economy-progression.md)). It is a developer tool and is not shipped. Continuous integration (CI) builds it and checks its formatting on every pull request but never runs a simulation.

## Run

From the repository root:

```bash
dotnet run --project dotnet/SekoyaTransit.Balance -c Release
```

By default it plays all three simulated players on all 3 map sizes with seeds 1, 2 and 3 for 50 game years each, at the same time. On the owner's 32-thread machine, a single 40-year game took 59, 69 and 76 seconds on Small, Medium and Large. Options:

| Option | Default | Meaning |
| --- | --- | --- |
| `--data <folder>` | the shipped `SekoyaTransit/Assets/_Project/Content/Data` | Content JSON to play with; copy the folder to try other values. |
| `--sizes small,medium,large` | all three | Map sizes. |
| `--profiles casual,steady,veteran` | all three | Simulated players. |
| `--seeds 1,2,3` | `1,2,3` | One game per seed. |
| `--years <n>` | `50` | Game years per game. |
| `--no-rail` | rail on | Road vehicles only. |
| `--rail-min-distance <tiles>` | `35` | Freight routes at least this long are served by rail. |
| `--max-route-distance <tiles>` | 70, 100 or 130 by map size | Longest route the player considers. |
| `--months-per-route <n>` | the player's pace | Game months between two new routes. |
| `--tables` | off | Print each game's yearly table, unlocks and summary. |
| `--verbose` | off | Print every route, unlock and rejected command, and the routes by income. Verbose games run one at a time so their lines do not mix. |
| `--parallel <n>` | one per processor thread, up to one per game | Games played at the same time. Results match a sequential run. |
| `--state-hash` | off | Hash the full saved game state every game month and print the rolling hash by year. Two builds with equal hashes played the same game, so this proves that a Core change leaves gameplay unchanged. |

## What the scripted player does

There are three simulated players:

| Player | New route every | Contracts | Share of a source's output it transports |
| --- | --- | --- | --- |
| Casual | 3 months | Accepts only what a route already serves | 50 % |
| Steady | 2 months | Accepts every second proposal, declines the others | 80 % |
| Veteran | Month | Accepts every proposal | 100 % |

Once per game month, in this order, through the same commands as the game UI:

1. Claims finished contract rewards, removes failed contracts and answers proposals. Casual also drops its oldest proposal when the list is full.
2. Buys the oldest available locked vehicle if cash covers its price plus a reserve of $15,000 or three months of upkeep, whichever is larger. Nothing is held back for an unlock, and old vehicles are never replaced: only new purchases use the new type.
3. Adds one vehicle per source and cargo, to its route with the fewest vehicles, if last month's transported share is below the player's target and cargo for one more vehicle is waiting.
4. Opens a new route when one is due, from what it has left above the reserve. A route it cannot afford stays due the next month. A new route comes with the vehicles estimated to carry 50 % of the source's monthly output, less what the source already ships, and at least one; the check covers those vehicles, not the road, rail or stations. Freight routes of 35 tiles or more use rail.

Steady and Veteran open the routes an accepted contract needs first, including the chain behind its cargo. Other routes are the best-scoring available, not random. A route goes from a producing industry (primary, or processing once it is fed) to the industry that consumes its output, or to a town that needs it and gets no supply yet; and passengers and mail run between nearby towns. A source has at most 5 routes, and routes are at most 70, 100 or 130 tiles long on Small, Medium or Large.

## Output

For each map size and player: bankrupt games, when the best town reaches each level, and contracts seen, claimed, failed and dropped; then Steady's median wait per unlock and whether each target passes: every player always has a route left when one is due, at most 50 % of towns at level 2 or more on 1 January 1910, level 3 or more in 1920, level 4 or more in 1930, and level 5 in 1940 (in each player's worst game), Steady buying each unlock after 1 to 4 years, and no bankruptcy.

With `--tables`, for each game: a yearly table (cash, score, income including contract bonuses, road and rail fleet, routes and new routes, vehicles added, open route opportunities, town population minimum / average / maximum, towns over 2,000 and 5,000 inhabitants, share of town needs met, grown industries), the unlocks (year available, cost, cash when it appeared, unlock date, wait), and a summary: bankruptcy, first $1M and $10M, years with new route work, and the first level 3 delivery to a town.
