# Content format and validation

Implements the content decisions of [ADR 0010](../adr/0010-data-driven-content-format.md). Balance values and definitions live in JSON files; Core never reads files and never validates schemas.

## Layout

| Path (under `TransportTycoon2/Assets/_Project/Content/`) | Purpose |
| --- | --- |
| `Data/cargo.json` | Cargo definitions: `id`, `nameKey`, `class` (`passengers` or `cargo`), `pricePerUnit`. |
| `Data/industries.json` | Industries: `id`, `nameKey`, `level`, and fixed-ratio `inputs` and `outputs` per production cycle. |
| `Schemas/*.schema.json` | JSON Schema (draft 2020-12), one per content family; data files reference them with `$schema` for editor autocompletion. |

The sample data (wood, planks, metal, furniture, and four industries) exists to exercise the harness. It is **not** balanced; real values come from the economy balancing work.

Display strings never appear in content: `nameKey` is a localization key ([ADR 0011](../adr/0011-localization.md)).

## Validation harness

The tests live in `dotnet/TransportTycoon.Core.Tests/Content/` and run with the rest of the suite (`dotnet test dotnet/TransportTycoon.Core.Tests`), so CI enforces them. The `JsonSchema.Net` package is referenced by that test project only; the Unity player and WebGL build do not depend on it.

Checks performed:

1. **Schema conformity** of every data file (types, required fields, id and key patterns, `additionalProperties: false`).
2. **Referential integrity** across files: unique cargo and industry ids; every input and output references an existing cargo; no cargo listed twice in one recipe.
3. **Recipe consistency**: level 1 industries have no inputs; higher levels have at least one input, and every input cargo is produced by at least one industry at a strictly lower level; every non-passenger cargo is produced by at least one industry.
4. **No production cycles**: the cargo dependency graph (inputs to outputs) must be acyclic.

Negative tests feed deliberately broken content to the same validator and assert that each problem is reported.

## Adding a content family

1. Add `Schemas/<family>.schema.json` and `Data/<family>.json` (Unity generates the `.meta` files; commit them).
2. Add a conformity test and extend the integrity checks in `ContentValidator` for any cross-file references.
3. Add negative tests for the new rules.

Validation covers the data files at test time only; loading definitions into immutable Core objects is a separate task. Industry placement receives its immutable industry rules from its caller and does not read JSON or depend on the validation harness.
