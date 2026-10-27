# ADR 0018: V1 audio approach

- Status: Accepted
- Date: 2026-10-20
- Deciders: Owner (amended 2026-10-27: owner-selected CC0 assets instead of AI-generated audio; volumes set on audio sources, no mixer asset)

## Context
V1 includes sound effects and one ambient music track ([product-vision.md](../specs/product-vision.md), [gameplay-systems.md](../specs/gameplay-systems.md)). The Unity presentation layer needs to play audio on Windows and WebGL without coupling audio behavior to the deterministic simulation.

## Options considered
1. Use Unity's built-in audio APIs and mixer: no additional dependency and sufficient for the stated V1 scope.
2. Add third-party audio middleware: richer authoring and runtime features, but adds package, integration, and learning costs without a current requirement.
3. Defer audio implementation: avoids early work but does not satisfy the V1 audio scope.

## Decision
Use Unity's built-in audio APIs for V1 sound effects and one ambient music track. Do not add third-party audio middleware unless a concrete requirement is identified and approved. Keep audio entirely in the Unity presentation layer; the deterministic Core must not reference Unity audio APIs or emit presentation-specific audio calls. Presentation reacts to accepted command results and to domain events (for example a paid delivery) to decide when a sound plays.

### Amendment (2026-10-27, Issue #68)

- **Assets:** V1 uses owner-selected open-licensed audio instead of AI-generated audio. Only Creative Commons Zero (CC0) assets are accepted, so no credit is legally required; each asset is still credited in the tracked [CREDITS.md](../../CREDITS.md). Source files keep their original format and file name; Unity import settings handle compression (Vorbis).
- **No `AudioMixer` asset:** music and effects play on separate `AudioSource`s whose volumes the code sets from the player settings. Unity has no public API to create a mixer asset from the scripted scene build, and on WebGL a mixer can only change group volume, so a mixer adds nothing V1 needs.
- **Player settings:** music volume, effects volume and mute are per-player preferences stored with Unity `PlayerPrefs`, not in the save file.

## Consequences
- No additional audio package or middleware integration is needed for V1.
- Audio assets and playback remain outside simulation state, preserving deterministic behavior.
- Validate browser audio activation and playback behavior in a WebGL build before audio is considered production-ready; browsers block playback until the player's first interaction.
- The V1 assets are listed in [CREDITS.md](../../CREDITS.md). Adding a non-CC0 asset needs a new owner decision.
- Mixer-only features (ducking, effects) would need a mixer asset later; they are Windows-only on Unity Web anyway.
