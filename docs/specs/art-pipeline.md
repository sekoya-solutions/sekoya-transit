# AI Art Pipeline Workshop

Status: **ComfyUI feasibility validated by the owner.** This pilot demonstrates that local ComfyUI can generate, curate, and import illustrative assets. The owner is not satisfied with the current visual results and expects substantial future work on workflow quality and in-game placement. Neither the pilot images nor the current workflow are production-ready.

## Direction and scope

The selected direction is **graphic** rather than painted or pixel art: clean, outlined shapes, a warm limited palette, and minimal cel shading. This is an original genre-appropriate direction, not a reproduction of another game's assets or distinctive art. The initial samples and four pilot images are studies; their geometry and details are imperfect.

The bounded pilot contains:

| Asset | Output | Purpose |
| --- | ---: | --- |
| `ground_grass_01.png` | 128 × 64 px | Isometric ground tile |
| `building_town_shop_01.png` | 128 × 128 px | Town building |
| `building_industry_flour_mill_01.png` | 224 × 240 px | Industry building |
| `vehicle_road_cargo_lorry_01.png` | 128 × 128 px | Road vehicle |

The grass tile and lorry use 128 PPU, retaining the provisional 1 × 0.5 and 1 × 1 world-unit sizes while preserving more detail than the earlier 64 px outputs. Buildings remain at 64 PPU. These are preview settings, not a production atlas convention. The preview is a diagnostic scene, not the runtime map renderer. The accepted chunk-mesh approach and its open atlas/sorting work remain as described in [ADR 0017](../adr/0017-map-rendering-approach-and-performance-budgets.md).

## Local generation environment and provenance

- ComfyUI Desktop 1.1.7, ComfyUI 0.42.1; standard core nodes only, custom nodes disabled.
- Checkpoint: Stable Diffusion XL (SDXL) Base 1.0, [official model card and license](https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0), CreativeML Open RAIL++-M license.
- Checkpoint file: `sd_xl_base_1.0.safetensors`, kept outside the repository in ComfyUI's shared model folder. Size: 6,938,078,334 bytes. SHA-256: `31E35C80FC4829D14F90153F4C74CD59C90B779F6AFE05A74CD6120B893F7E5B`.
- Generation host: Windows PC with an NVIDIA GeForce RTX 5090; PyTorch 2.12.1+cu130.
- The model license contains use restrictions and is not a guarantee that a particular generated output is free of third-party rights. Review model terms and each result before portfolio or commercial use.

The ComfyUI workflow was `CheckpointLoaderSimple → CLIPTextEncode` (positive and negative) `→ EmptyLatentImage → KSampler → VAEDecode → SaveImage`. Images were 1024 × 1024, 28 steps, CFG 6.5, DPM++ 2M sampler, Karras scheduler, denoise 1.0. The PNGs embed the ComfyUI prompt/workflow metadata; the prompts below record the selected source images used by the curation script.

Shared negative prompt:

> multiple objects, multiple buildings, multiple vehicles, duplicate, collage, contact sheet, reference sheet, asset sheet, turnaround, lineup, street, city, background scene, scenery, trees, people, person, character, props, extra object, shadow, gradient, vignette, photorealistic, photograph, 3D render, CGI, painterly brush texture, pixel art, blur, text, letters, labels, logo, watermark, frame, cropped, out of frame, roof opening, cutaway, broken perspective, deformed geometry

| Source | Seed | Positive prompt |
| --- | ---: | --- |
| `issue16-pilot-assets-v2/town-building.png` | 16002612 | single isolated sprite for an original 2D isometric strategy game, graphic storybook illustration, bold clean dark-brown ink outlines, simplified large shapes, warm limited palette, flat colors with minimal cel shading, crisp hand-drawn line art, orthographic three-quarter isometric view, centered with generous blank margin, uniform pale cream background, exactly one compact two-story red-orange brick town shop, one building only, complete continuous dark slate pitched roof fully closed over the interior, cream-trimmed windows and one centered door, no attached buildings, no street |
| `issue16-pilot-assets-v2/industry-building.png` | 16002613 | single isolated sprite for an original 2D isometric strategy game, graphic storybook illustration, bold clean dark-brown ink outlines, simplified large shapes, warm limited palette, flat colors with minimal cel shading, crisp hand-drawn line art, orthographic three-quarter isometric view, centered with generous blank margin, uniform pale cream background, exactly one compact red-brick flour mill factory building, one building only, complete closed dark roof, one simple brick chimney, coherent simple architecture, no attached buildings, no street, no surrounding industrial complex |
| `issue16-pilot-assets-v2/truck.png` | 16002614 | single isolated sprite for an original 2D isometric strategy game, graphic storybook illustration, bold clean dark-brown ink outlines, simplified large shapes, warm limited palette, flat colors with minimal cel shading, crisp hand-drawn line art, orthographic three-quarter isometric view, centered with generous blank margin, uniform pale cream background, exactly one complete compact early-1900s red cargo lorry, one cab and one short wooden cargo bed, two axles with four wheels, no trailer, no people, no other vehicles, front three-quarter isometric view |
| `issue16-pilot-assets-v3/grass-texture.png` | 16002615 | seamless repeating top-down 2D illustrated grass ground texture, clean graphic game art matching a hand-inked storybook strategy game, crisp dark-green outline details on a few small blades and tufts, flat mid-green fill with subtle lighter and darker green patches, evenly distributed across the entire image edge to edge, perfectly flat overhead view, no visible objects |

The initial direction comparison used seed `16002026` at the same resolution, steps, CFG, sampler, and scheduler. Its pixel-art prompt did not produce true pixel art; those comparison samples are not production assets.

## Curation and Unity import

Generated source images are local ComfyUI outputs, not repository assets. `tools/art/curate_issue16_pilot.py` reproduces the selected crops and outputs:

| Source image | Crop (left, top, right, bottom) |
| --- | --- |
| Town building | `(560, 0, 1024, 400)` |
| Industry building | `(80, 0, 940, 1024)` |
| Cargo lorry | `(210, 660, 690, 1000)` |
| Grass texture | Center-cropped to 2:1, then masked to a diamond |

The selected grass texture and lorry source already had suitable content; the blur came from reducing their curated outputs to 64 px. The source images were retained, and the ground tile and lorry outputs were raised to 128 × 64 px and 128 × 128 px respectively, with matching 128 PPU import settings to preserve their world scale. Direct-generation retries produced sheet-like compositions and were discarded rather than used to replace the suitable sources.

For building and vehicle sprites, the script removes edge-connected flat background, preserves enclosed areas, scales the remaining content to fit within a 2 px transparent margin, and anchors it at the bottom center of a transparent PNG canvas. The ground image is center-cropped to 2:1, resized with Lanczos filtering, and masked into a 128 × 64 px diamond using a 4× mask before downsampling.

Run the curation step with the ComfyUI output directory as the input root:

```text
python tools/art/curate_issue16_pilot.py --input-dir "<ComfyUI output directory>" --output-dir "TransportTycoon2/Assets/_Project/Art/Pilot"
```

The expected input root contains `issue16-pilot-assets-v2/` and `issue16-pilot-assets-v3/`. Do not add generated source sheets, the checkpoint, or temporary review images to Git. Curated PNGs are stored under `Assets/_Project/Art/Pilot/` and are covered by the repository's Git Large File Storage (LFS) PNG rule.

For future iterations, when a generated image misses the required composition or shape, revise the prompt and regenerate rather than repeatedly cropping or heavily modifying an unsuitable result. The current grass tile remains a study derived from a suitable generated texture and masked into a diamond. Direct grass-tile generation attempts produced sheet-like compositions and were discarded; the existing texture source was retained because the reported blur came from downsampling, not unsuitable source content.

`ArtPilotImportSettings` configures the ground tile and lorry as single sprites at 128 PPU and buildings at 64 PPU, with alpha transparency, no mipmaps, bilinear filtering, clamp wrapping, uncompressed texture import, and no non-power-of-two resizing. The ground tile uses a centered pivot; buildings and vehicle use a bottom-center pivot; the mesh type is Full Rect. Unity generates and maintains the `.meta` files.

`ArtPilotSceneBuilder.CreatePreviewScene` creates `ArtPilotPreview.unity`: an 8 × 8 isometric ground grid plus the three object sprites. A fixed-seed selection (`16002618`) chooses each object from spatially separated candidate cells, keeping the preview repeatable while preventing the buildings and vehicle from overlapping. It uses an orthographic camera and sorting order derived from isometric cell coordinates. `ArtPilotSceneBuilder.BuildWebGLPreview` builds only this scene for browser review, without changing the project's configured WebGL compression settings.

Build the preview with Unity 6.3 and serve `TransportTycoon2/Builds/Issue16/ArtPilotWebGL/` through a loopback-only static server:

```text
Unity.exe -batchmode -nographics -projectPath <TransportTycoon2> -buildTarget WebGL -executeMethod TransportTycoon.Art.Editor.ArtPilotSceneBuilder.BuildWebGLPreview -logFile <log>
```

Open the server's `index.html` URL in the integrated browser. The Unity scene and WebGL player are the visual references; the exact pilot images have not yet received owner approval.

## Pilot output hashes

| Output | SHA-256 |
| --- | --- |
| `ground_grass_01.png` | `261b26f36eff0b65459aecec935512aad235c4fd42dd777666796da07b558ade` |
| `building_town_shop_01.png` | `35db27f80632e5ae9c709fcda908a6ed333da89c55c1c9a488f5f61a5043b8eb` |
| `building_industry_flour_mill_01.png` | `9fff9a895c951a17917e14c65c3d995fb0cd0570d56073e730b78a6259553e82` |
| `vehicle_road_cargo_lorry_01.png` | `8caf577d4737afcd6c5f6dea8c18c99696d122b972968723080d1ec382149fdf` |

## Validation status

- `python tools/art/test_curate_issue16_pilot.py` using ComfyUI's bundled Python: **3 tests passed**.
- Re-running `curate_issue16_pilot.py` from the recorded source images produced byte-identical outputs; all four hashes matched.
- Unity 6000.3.26f1 generated the updated 8 × 8 preview scene; the subsequent WebGL player build compiled the Editor scripts and succeeded at 73 MB.
- Unity EditMode test runner: **6 passed, 0 failed, 0 skipped** on the current pilot revision.
- Updated Unity WebGL player loaded in the integrated browser and displayed the 8 × 8 ground field with the town building, mill, and lorry separated.
- Owner validation: **ComfyUI asset-generation feasibility accepted; visual quality and production placement are not accepted**.

## Pending decisions

- Improve the generation-to-curation workflow and visual output quality.
- Design and validate production-quality in-game art placement/rendering beyond this diagnostic grid.
- Production atlas/packing conventions and any change to the provisional import settings.

Treat the art and workflow as feasibility-study outputs only. No new package, custom node, or production-scale generation is part of this workshop.
