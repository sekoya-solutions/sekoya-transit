# Capture tool: promotional screenshots and clips

The Editor capture tool makes the game's README and portfolio images again from committed files ([ADR 0036](adr/0036-editor-capture-tool.md), Issue [#352](https://github.com/sekoya-solutions/sekoya-transit/issues/352)). It is an Editor-only developer tool; nothing of it runs in the shipped game.

## Files

| Path | What it is |
| --- | --- |
| `SekoyaTransit/Capture/Shots/*.json` | One shot per file: what to record and how. |
| `SekoyaTransit/Capture/Saves/*.sav` | The saves the shots load (Git LFS). |
| `Builds/Capture/` | The outputs: `<shot name>.png` or `<shot name>.mp4` (ignored by Git). |

## Make the shots

From the Unity Editor: **Sekoya Transit > Capture > Make All Shots**. From the command line, with the Editor drawing (the Recorder records a rendered Game view, so not with `-batchmode`):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -projectPath "<absolute path>\SekoyaTransit" -logFile "<absolute path>\Builds\Capture\capture.log" -executeMethod SekoyaTransit.Presentation.Editor.Capture.CaptureCommand.RunFromCommandLine
```

Add `-shot <name>` to make one shot. The Editor opens, makes the shots one after another in Play mode and quits with exit code 0 when every shot was written, 1 otherwise; the log lists each file written. Six shots take about a minute on the owner's machine.

For each shot, the tool loads the save through the game's normal load path with the game paused, applies the shot, starts the Unity Recorder at the shot's fixed frame rate and resumes the game on the first recorded frame. A fixed frame rate makes every frame one fixed step of game time, so a slow machine records more slowly but never drops or speeds up frames.

## Shot file

```json
{
  "name": "delivery-clip",
  "save": "showcase-1935.sav",
  "kind": "clip",
  "width": 1920,
  "height": 1080,
  "hud": "hidden",
  "panels": [],
  "speed": 1,
  "frameRate": 30,
  "seconds": 8,
  "camera": [
    { "time": 0, "tileX": 80, "tileY": 104, "zoom": 14 },
    { "time": 8, "tileX": 76, "tileY": 99, "zoom": 9 }
  ]
}
```

| Field | Meaning |
| --- | --- |
| `name` | Output file name: letters, digits and dashes; unique across shot files. |
| `save` | A `.sav` file name in `SekoyaTransit/Capture/Saves/`. |
| `kind` | `screenshot` (one PNG) or `clip` (an MP4 with sound). |
| `width`, `height` | Output size in pixels, 16 to 7680; even for a clip. |
| `hud` | `shown` or `hidden`: the heads-up display (toolbars, money, panels). |
| `panels` | Panels opened before recording: `routes`, `contracts`, `vehicles`, `unlocks`, `finance`, `achievements`, `industries`, `towns`. |
| `speed` | Game speed: 1, 2 or 3. |
| `frameRate` | Recorded frames per second, 1 to 120. |
| `seconds` | A clip's length; for a screenshot, how long the game runs before the frame is taken (vehicles move, panels fill). |
| `camera` | Keyframes: `time` in seconds from the start (the first is 0), the map point in tiles (`tileX`, `tileY`) and the `zoom` (orthographic size, 4 to 140; smaller is closer). A screenshot has exactly one keyframe. Between keyframes the camera moves with an ease-in-ease-out curve, and the zoom changes evenly in scale. |

A missing or wrong field stops the run before Play mode with an error that names the file and the field.

## Repeatability

The game state and the map are the same on every run of a shot: the save loads paused and the game advances one fixed step per recorded frame. The effects timed in real time (toasts, cash popups, goals) follow the same steps during a capture. Two runs still differ in a few hundred to about 1,400 scattered pixels of a 1920x1080 image, mostly by at most 10 of 255 levels, with one area near the toast in the Routes window differing more; their cause was not traced further, since the images are made once for the README (owner decision, 2026-11-18). MP4 files also differ byte for byte between runs.

## The showcase save

`showcase-1935.sav` is a game played by the balance tool's contract-only player on a Medium map, seed 1, from 1900 to 1934-12-31:

```bash
dotnet run --project dotnet/SekoyaTransit.Balance -c Release -- --player contracts --size medium --seeds 1 --years 35 --save SekoyaTransit/Capture/Saves/showcase-1935.sav
```

A balance simulation needs the owner's go ([AGENTS.md](../AGENTS.md)). The [balance tool README](../dotnet/SekoyaTransit.Balance/README.md) describes the check that proves the file is the one the game would write.

## README animation

GitHub shows an animated image from the repository inside a README, but not a video file. [`scripts/capture-to-webp.ps1`](../scripts/capture-to-webp.ps1) turns a clip into a looping animated WebP without sound (12 frames per second, 800 pixels wide; about 3 MB for 8 seconds), with the portable ffmpeg in the ignored `.tools/ffmpeg/` folder:

```powershell
powershell -NoProfile -File scripts/capture-to-webp.ps1 -Clip Builds/Capture/delivery-clip.mp4 -Output Builds/Capture/delivery-clip.webp
```
