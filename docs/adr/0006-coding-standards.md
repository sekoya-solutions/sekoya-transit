# ADR 0006: Coding standards

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
The owner reviews most of the code and expects state-of-the-art practices ([AGENTS.md](../../AGENTS.md)). Standards must be enforced automatically, not by convention alone.

## Options considered
1. **`.editorconfig` + .NET analyzers + nullable reference types in Core + `dotnet format` check in CI.**
2. Add StyleCop analyzers: stricter style, but noisy and low value beyond `.editorconfig`.
3. No enforced tooling: relies on discipline, rejected.

## Decision
Option 1:
- A committed `.editorconfig` defines formatting and naming (Microsoft C# conventions).
- .NET (Roslyn) analyzers enabled; **warnings treated as errors in the Core assemblies**, warnings tolerated but tracked in Unity-facing assemblies.
- **Nullable reference types enabled in Core.**
- XML documentation comments on the public API of Core.
- Formatting verified in CI with `dotnet format --verify-no-changes`.
- Code comments only where intent is not obvious from the code.

## Consequences
- Unity 6.3 compiles **C# 9**, so Core and its tests are written in C# 9 (block-scoped namespaces; `#nullable enable` per file because asmdefs cannot enable nullable globally). The plain .NET project pins `LangVersion 9.0` to guarantee the same code compiles in both toolchains.
- Analyzers run through the plain .NET build (`AnalysisMode Recommended`, warnings as errors). Running them inside Unity is deferred until needed.
- CA1707 (underscores in names) is disabled for test files only.
- Some Unity-generated or third-party code needs analyzer exclusions.
- Exact analyzer rule severities are tuned when the project is scaffolded and recorded in `.editorconfig`.
