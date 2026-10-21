# ADR 0029: Deterministic industry placement

- Status: Accepted
- Date: 2026-10-21
- Deciders: Owner

## Context

Procedural generation places industries after terrain and towns ([ADR 0014](0014-procedural-generation.md)). Industry positions must preserve content-defined production chains while remaining deterministic across supported runtimes. V1 terrain is flat, and map tiles may already be occupied by town anchors.

The owner selected dispersed placement and requires every configured production chain to be represented. Counts and producer distance limits are supplied as data; no balance defaults are introduced.

## Options considered

1. **Seeded greedy candidate placement:** shuffle row-major tile candidates using PCG32, place industries in dependency order, and accept only positions satisfying spacing and producer-distance constraints. This is deterministic and bounded by the map size, but a candidate order may fail even when another valid arrangement exists.
2. **Constraint search/backtracking:** search for a complete arrangement before returning it. This reduces false failures but can have unpredictable cost as map size, industry counts, and chain dependencies grow.
3. **Unconstrained placement followed by validation/retry:** simple placement, but can require repeated whole-map generation and still has no clear bound on retries.

## Decision

Use the seeded greedy candidate placer:

- Core receives immutable industry placement rules from the content layer; it does not read JSON files or perform schema validation.
- Each rule provides a stable industry ID, level, input/output cargo IDs, exact positive instance count, and maximum producer distance. Settings provide a supported map size, minimum industry spacing, and coordinates already occupied by towns or other map objects.
- Every configured industry rule is represented. Inputs must have at least one configured producer at a strictly lower level. Primary industries have no inputs; higher-level industries have at least one.
- Shuffle every map tile ID in row-major order with PCG32 using a dedicated fixed stream selector. Place in ascending industry level and ordinal industry ID order, returning instances in that order.
- Industry anchors must be on unoccupied tiles. Industry-to-industry spacing and each consumer-to-producer limit use inclusive Chebyshev distance in tiles. For every input cargo, a consumer must be within its configured maximum distance of at least one already placed lower-level producer.
- Reject invalid settings and theoretical capacity overflow before placement. If the deterministic candidate order cannot place the exact configured counts, throw rather than returning partial results.

## Consequences

- Equal settings and seeds produce the same ordered industry anchors. Exact instance counts and spatial limits remain data-driven.
- Existing town or object anchors can be supplied so industry placement cannot overlap them. The flat V1 map adds no terrain-suitability filter.
- The greedy order can fail for some otherwise feasible dense configurations. The error is explicit, but finding another valid arrangement is outside this proposal.
- The candidate order, stream selector, placement order, and distance semantics are part of deterministic generation behavior and should be locked by a known-answer test.
