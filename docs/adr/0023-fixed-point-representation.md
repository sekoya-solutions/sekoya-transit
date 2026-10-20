# ADR 0023: Fixed-point representation

- Status: Accepted
- Date: 2026-10-20
- Deciders: Owner

## Context

The deterministic simulation uses integer or fixed-point arithmetic rather than floating-point state ([ADR 0003](0003-simulation-architecture.md)). Core needs a shared numeric type with explicit precision, range, rounding, and exceptional arithmetic behavior.

## Options considered

1. **Q32.32 in a signed 64-bit integer:** 32 fractional bits, range [-2^31, 2^31 - 2^-32] with 2^-32 resolution.
2. **Q48.16 in a signed 64-bit integer:** wider range but lower fractional precision.
3. **Decimal fixed scale:** a chosen integer number of subunits per whole value; intuitive decimal precision but a separate scale must fit all domains.

## Decision

Use a signed Q32.32 representation, approved by the owner for Issue #22:

- Store the two's-complement raw value in a signed 64-bit integer; one whole unit is `1L << 32`.
- Addition, subtraction, integer conversion, and negation use checked arithmetic and throw `OverflowException` when the result is not representable.
- Multiplication and division scale their exact integer intermediate result and truncate toward zero. Intermediate computations must not wrap; an out-of-range result throws `OverflowException`.
- Division by zero throws `DivideByZeroException`.
- Core calculations use raw/integer values only; floating-point conversions are not part of the initial type.

## Consequences

- The type has deterministic, portable arithmetic and approximately 2.3e-10 resolution, with exact raw range [-2^31, 2^31 - 2^-32].
- Multiplication and division require careful wide-intermediate arithmetic; relying on a wrapping 64-bit intermediate would be incorrect.
- Domain-specific money scales and serialization policies may require follow-up decisions; the raw integer value is the stable representation.
