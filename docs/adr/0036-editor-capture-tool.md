# ADR 0036: Editor capture tool for promotional screenshots and clips

- Status: Accepted
- Date: 2026-11-18
- Deciders: Owner

## Context

The README and the portfolio page need screenshots and a short clip of a game in progress (Issue [#352](https://github.com/sekoya-solutions/sekoya-transit/issues/352)). The V1 screenshots were taken by hand in Chrome; they go stale after every gameplay or art change and cannot be made again the same way. A game benefits from a tool that makes its promotional images from a fixed description, as often as needed. The simulation is deterministic and advances by `Time.deltaTime` ([ADR 0003](0003-simulation-architecture.md)), and save files restore a game exactly ([ADR 0009](0009-save-format-and-persistence.md)), so a shot can be replayed frame for frame. The images also need a game worth showing: many routes and grown towns, which takes hours to play by hand.

## Options considered

### Capture

1. **Screenshots by hand in the owner's browser:** best image quality and no code, but about half an hour of the owner's time per refresh, never quite the same framing twice, and no repeatable clip.
2. **Custom frame capture plus ffmpeg:** Editor code saves each frame as a PNG at a fixed frame rate, and ffmpeg (a free command-line video encoder) turns the frames into a video. No new Unity package, but the project owns the capture, timing and audio code, and audio needs separate work.
3. **Unity Recorder (`com.unity.recorder`):** Unity's own Editor-only package records the Game view to PNG or to MP4 with the game's audio, and it sets a fixed capture frame rate itself. It is a new direct dependency and brings its own dependencies: `com.unity.timeline` (removed in [ADR 0019](0019-unity-package-selection.md) for having no use), `com.unity.collections` and `com.unity.bindings.openimageio` (image encoding, Editor-only); the tool uses the Recorder's scripting API, not Timeline tracks.

### The game shown

1. **The owner plays a game and exports it:** looks like a human player's game, but costs the owner's time and cannot be made again after a content change.
2. **The balance tool's contract-only player writes its game as a save file** ([ADR 0033](0033-headless-balance-simulation.md)): a game into the 1930s with many routes and grown towns, made again by one command. The game's save writer uses Unity's `JsonUtility`, which the plain .NET tool replaces with a small stand-in, so the written file must be proved identical to what the game writes.
3. **Run the contract-only player inside the Unity Editor:** uses the real `JsonUtility`, but the player lives in the .NET tool and would have to move into the Unity project, a larger change for no other gain.

## Decision

Capture option 3 and game option 2.

- **Shot files:** each shot is a JSON file under `SekoyaTransit/Capture/Shots/` (outside `Assets/`, so Unity never imports or ships it). It names the save to load (under `SekoyaTransit/Capture/Saves/`), the output kind (a PNG screenshot, or a clip), the resolution, the game speed, the length of a clip, the camera keyframes (map center and zoom over time, interpolated between keyframes), whether the heads-up display (HUD) is shown, and which panels are open. A shot file is validated when read: a missing or wrong field stops that shot with an error naming the file and the field.
- **The capture command:** an Editor menu item and an `-executeMethod` entry make every shot or one named shot. It enters Play mode in the `MapPlay` scene, loads the shot's save through the game's normal load path, applies the shot, and records with the Recorder: a PNG after the scene has settled, or an MP4 with audio for the clip's length. Outputs go under ignored `Builds/Capture/`. The Recorder records a rendered Game view, so the command runs with the Editor drawing (started from the command line without `-batchmode`); continuous integration (CI) never runs it. Only an Editor assembly of the project references the Recorder, so no Recorder code runs in the Windows or WebGL player. The Recorder 5.1.8 package still has two small runtime assemblies (three source files, no start-up hook) that Unity may compile into a player unless code stripping removes them; the first player build after this change checks its assembly list.
- **Same game on every run:** the Recorder's fixed capture frame rate makes the game advance exactly one frame step per recorded frame, so a slower machine records more slowly but never drops or speeds up frames. A shot's save loads with the game paused in the same step that starts it, and the effects timed in real time (toasts, cash popups, goals) follow a presentation clock that steps with the recorded frames during a capture. Two runs then show the same game state and map; a few hundred to about 1,400 scattered pixels per 1920x1080 image still differ slightly, a cause not traced further because the images are made once for the README (owner decision, 2026-11-18; details in [capture.md](../capture.md)).
- **README animation:** a script under `scripts/` turns a clip into a looping animated WebP of a few seconds without sound, with a portable ffmpeg in the ignored `.tools/` folder (GitHub shows an animated image from the repository inside a README; it does not play a video file from the repository there).
- **The save:** the balance tool gains `--save <file>`, which writes the game it played as a `.sav` with the game's own save writer, linked into the tool, and an extended `JsonUtility` stand-in. The proof that the file matches the game's own: the save file report command ([AGENTS.md](../../AGENTS.md), Issue [#320](https://github.com/sekoya-solutions/sekoya-transit/issues/320)) loads it in Unity, saves the restored game again with Unity's real `JsonUtility`, and that JSON must equal the JSON in the tool's file byte for byte. The committed shot save comes from one contract-only run into the 1930s.
- **Committed outputs:** the shot files and the shot save are committed (`.sav` through Git Large File Storage (LFS)); the images and the WebP chosen for the README go under `docs/media/` through LFS. The MP4 clip (8.4 MB for 8 seconds at 1080p) is not committed: the owner keeps it outside the repository, and any machine can make it again from the shot files (owner decision, 2026-11-18).

## Consequences

- Promotional images and clips can be made again with one command after any change, from the same save and framing; [capture.md](../capture.md) explains how.
- Camera keyframe interpolation and shot file validation are plain code with EditMode tests; the recording itself needs a rendered Editor and is checked by running it, not by CI.
- The project gains the Recorder package (5.1.8) and its dependencies Timeline, Collections and OpenImageIO bindings, used only by Editor code; [ADR 0019](0019-unity-package-selection.md)'s removal of Timeline no longer holds while the Recorder needs it.
- The balance tool now depends on the save writer's code and stand-in; a save format change that the stand-in does not reproduce fails the byte-for-byte check rather than producing a silently different file.
- The shot save is made by a scripted player, so its network shows that player's habits rather than a human's layout.
- ffmpeg stays a local, ignored developer tool, downloaded only with the owner's approval.
