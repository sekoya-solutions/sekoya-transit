# ADR 0015: Unity editor version policy

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner (decision delegated to the assistant)

## Context
[ADR 0001](0001-unity-version.md) chose Unity 6.3 LTS. The owner asked whether the Built-In Render Pipeline deprecation (Unity 6.5 and newer) affects the project, and whether to use 6.5 instead. Findings (web research, October 2026; verify in Unity Hub):
- The project uses 2D URP ([ADR 0002](0002-render-pipeline.md)), so the Built-In deprecation does not apply.
- Unity 6.5 is a Supported (non-LTS) release and reached end of life on 31 August 2026; 6.6 is the current non-LTS release.
- Unity 6.7 LTS is announced for the end of 2026.

## Options considered
1. **Stay on the latest 6.3 LTS patch**, then evaluate 6.7 LTS when it is released.
2. Use 6.5 or 6.6: newer features, but short support windows force mid-project upgrades.
3. Wait for 6.7 LTS before starting: delays the project with no clear benefit.

## Decision
Option 1:
- Create the project on the **latest 6000.3.x LTS patch** (not the older 6000.3.2f1 currently installed, and not 6000.5.x).
- When 6.7 LTS is released and its first patches are out, evaluate a migration as a separate spike and a new ADR. Until then, pin the editor version in `ProjectVersion.txt` and CI.

## Consequences
- Package versions follow the 6.3 LTS verified set.
- A planned LTS-to-LTS upgrade (6.3 to 6.7) will be a good portfolio exercise in controlled engine upgrades, protected by the automated test suite.
