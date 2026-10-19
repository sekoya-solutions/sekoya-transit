# ADR 0001: Unity version

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
The engine is Unity (owner's choice, see [product-vision.md](../specs/product-vision.md)). The project spans months to years, targets Windows and WebGL, and needs stability.

## Options considered
1. **Unity 6.3 LTS (6000.3)**: Long-Term Support (LTS), supported until about December 2027, strong 2D URP improvements, WebGL supported.
2. A newer non-LTS "Update" release: newer features, short support window, higher risk.
3. Older LTS (2022/2023): stable but nearing end of support and missing recent 2D and UI Toolkit improvements.

## Decision
Use **Unity 6.3 LTS**. Before creating the project, verify whether a newer LTS has been released and ask the owner if so. Pin the exact editor version in `ProjectSettings/ProjectVersion.txt` and in continuous integration.

## Consequences
- Upgrading to the next LTS is a deliberate, separate decision (new ADR).
- Package versions must be those verified for this editor version.
