# Content format and validation

Implements the content decisions of [ADR 0010](../adr/0010-data-driven-content-format.md). Balance values and definitions live in JavaScript Object Notation (JSON) files; Core never reads files and never validates schemas.

## Layout

| Path (under `TransportTycoon2/Assets/_Project/Content/`) | Purpose |
| --- | --- |
| `Data/cargo.json` | Cargo definitions: `id`, `nameKey`, `class` (`passengers` or `cargo`), `pricePerUnit`. |
| `Data/industries.json` | Industries: `id`, `nameKey`, `level`, and fixed-ratio `inputs` and `outputs` per production cycle. |
| `Data/economy.json` | Starting company cash. |
| `Data/towns.json` | Town starting and maximum population, growth tiers (level 1 / 2 / 3 need counts per population threshold), demand and growth knobs, passenger and mail generation, `minTownsPerNeededCargo` (how many towns must plan each freight cargo as a need), and `names`: the unique real-world capital names towns are named from (Issue #193). Loading rejects tiers asking for more needs of a level than there are cargo types of that level, and a name list shorter than the largest town count. |
| `Data/vehicles.json` | Road trucks, rail locomotives, and the shared rail wagon entry (stats, purchase, upkeep, `availableYear` / `unlockCost` / `starting`). Core enforces year and permanent unlock for buy/unlock commands (Issue #63). |
| `Data/achievements.json` | Achievements: `id`, `nameKey`, `kind` (`townPopulation`, `companyCash`, `vehicleCount`, `cargoDelivered`), `goal`, and for `cargoDelivered` only `cargo` plus `destination` (`town`, `industry`, `any`). Permanent milestones with no reward (Issue #65). |
| `Data/contracts.json` | Contract generation and reward knobs: `maxOpenContracts`, `minAmount`, `maxAmount`, `durationMonths`, `rewardMultiplier`, `townPopulationBonus` (Issue #173). Loading rejects a negative maximum, a minimum below 1 or above the maximum, a duration below 1, and a negative multiplier or bonus. |
| `Data/worldgen.json` | World generation per map size (Issue #67): a `sizes` array with exactly one entry for each of `small` (128×128), `medium` (256×256), and `large` (512×512). Each entry has `townCount`, `townSpacing` and `industrySpacing` (minimum Chebyshev distance between anchors, in tiles) and an ordered `industries` list of `id`, `count`, and `maxProducerDistance` (0 for a primary industry, positive for a consumer). An industry's level, inputs, and outputs come from `industries.json`. Loading rejects a missing, duplicate, or unknown size, an unknown industry ID, a count below 1, a negative distance, and any combination the generator cannot place (for example a consumer whose input has no lower-level producer). Every size places all five chains (Issue #193: 3 / 2 / 1 of each level 1 / 2 / 3 industry on `small`, 6 / 4 / 2 on `medium`, 12 / 8 / 4 on `large`), and generation raises `townCount` to the need-plan minimum ([gameplay-systems.md](gameplay-systems.md) section 6). Counts stay placeholders until the balancing workshop ([#73](https://github.com/sekoya-solutions/transport-tycoon-2/issues/73)). |
| `Schemas/*.schema.json` | JSON Schema (draft 2020-12), one per content family; data files reference them with `$schema` for editor autocompletion. |

The initial catalog contains five three-level chains (15 industries total): timber (wood, planks, furniture), baking (wheat, flour, bread), metalworking (iron ore, iron, machinery), livestock/food (livestock, meat, canned food), and textiles (wool, fabric, clothing). Passenger-class cargo (`passengers` and `mail`) remains available as a separate cargo class and is not produced by an industry; towns generate it into waiting stock (Issue #59).

Prices and recipe quantities are deliberately simple, logical **placeholders**, not balanced economy values. They are data-driven so the dedicated economy-balancing work can revise them later without changing code. The sample values exist to exercise the catalog, loader, and validation harness.

Display strings never appear in content: `nameKey` is a localization key ([ADR 0011](../adr/0011-localization.md)).

## Validation harness

The tests live in `dotnet/TransportTycoon.Core.Tests/Content/` and run with the rest of the suite (`dotnet test dotnet/TransportTycoon.Core.Tests`), so continuous integration (CI) enforces them. The `JsonSchema.Net` package is referenced by that test project only; the Unity player and Web Graphics Library (WebGL) build do not depend on it.

Checks performed:

1. **Schema conformity** of every data file (types, required fields, id and key patterns, `additionalProperties: false`).
2. **Referential integrity** across files: unique cargo, industry, vehicle, and achievement ids; every input, output, and achievement cargo reference exists; no cargo listed twice in one recipe; exactly one starting road type, one starting rail locomotive, and one `rail_wagon` entry.
3. **Recipe consistency**: level 1 industries have no inputs; higher levels have at least one input, and every input cargo is produced by at least one industry at a strictly lower level; every non-passenger cargo is produced by at least one industry.
4. **No production cycles**: the cargo dependency graph (inputs to outputs) must be acyclic.

Negative tests feed deliberately broken content to the same validator and assert that each problem is reported.

## Adding a content family

1. Add `Schemas/<family>.schema.json` and `Data/<family>.json` (Unity generates the `.meta` files; commit them).
2. Add a conformity test and extend the integrity checks in `ContentValidator` for any cross-file references.
3. Add negative tests for the new rules.

## Runtime loading

The Unity Content assembly reads the cargo and industry JSON from explicitly supplied `TextAsset` references and uses Unity's `JsonUtility` to map them into immutable definitions in Core. Each industry's `inputs` and `outputs` are represented as fixed-ratio cargo amounts. The resulting catalog supports ordinal lookup by stable definition ID.

The loader rejects missing, malformed, or structurally incomplete content with a `ContentLoadException`; it does not publish a partial catalog. Unity's serializer ignores unknown JSON fields and does not enforce the JSON Schemas. Schema conformity and cross-file referential integrity remain test-time validation responsibilities. Runtime composition must explicitly include and supply the content `TextAsset` references in player builds.

The content loader is tested in Unity EditMode tests. Core definition objects remain Unity-independent and are also covered by the plain .NET test suite. The content loader does not define or constrain the separate save-game serializer ([ADR 0009](../adr/0009-save-format-and-persistence.md)).

Industry placement receives its immutable industry rules from its caller and does not read JSON or depend on the validation harness.
