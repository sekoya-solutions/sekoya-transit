# ADR 0031: UI icons as hand-authored SVG

- Status: Accepted
- Date: 2026-10-27
- Deciders: Owner

## Context

Issue [#182](https://github.com/sekoya-solutions/transport-tycoon-2/issues/182) replaces the text of the tool, speed, and panel buttons with icons, adds an icon for every cargo and a dollar icon for cash, and asks whether such a set can be produced as code or needs an Artificial Intelligence (AI) image generator. The set has 36 icons today (6 tools, 3 speeds, 8 panel buttons, 17 cargos, dollar, and a generic cargo icon), will grow with content, and must stay one consistent style, legible at 20–36 pixels (px) on the dark shell buttons, and importable into UI Toolkit. Cargo is data-driven ([gameplay-systems.md](../specs/gameplay-systems.md)), so a new cargo must not break the UI when it has no icon yet.

## Options considered

The owner set the order of attempts: Scalable Vector Graphics (SVG) authored as code first, then ComfyUI, then Grok Imagine. The first option was accepted, so the other two were assessed on known facts only and not tried.

1. **SVG authored by the coding agent.** Visual consistency: high, since every icon shares one 24 × 24 grid, one ink stroke, and one palette by construction. Legibility: checked by the owner on a contact sheet at 24 and 32 px on the button colors. Unity import: Unity 6.3 ships the Vector Graphics module, which imports SVG as a UI Toolkit `VectorImage` that stays sharp at any size. Licensing: original work, no model terms. Cost: no tool or service. Repeatability: a new icon is a new small text file reviewed like code. Limits: simple flat shapes only, so it fits icons, not illustrated sprites.
2. **ComfyUI with Stable Diffusion XL (SDXL).** Local and free, but the [Issue #16 pilot](../specs/art-pipeline.md) showed that keeping many small images consistent needs substantial workflow work. Output is raster, so every icon needs background removal and downscaling, and the model license carries use restrictions.
3. **Grok Imagine.** Hosted generator with raster output and the same curation steps; per-image cost or a subscription, and an API key the agent must never handle, so the owner would run the prompts. Consistency across a growing set is not guaranteed.

## Decision

UI icons are hand-authored SVG files under `Assets/_Project/Art/Icons/` (`Tools`, `Speed`, `Panels`, `Common`, `Cargo`), following the style rules in [art-pipeline.md](../specs/art-pipeline.md#svg-icon-set-issue-182). An editor postprocessor imports every SVG there as a UI Toolkit vector image. Buttons get their icon from USS by element name. Cargo icons are found by cargo id (`Cargo/<cargo id>.svg`) through a generated `CargoIconSet` asset, and a cargo without a file shows the generic cargo icon. In the list panels, a cargo name is shown as icon elements next to text: the localized format string is split at its `{n}` placeholders (`TextLine`), so the translated word order is kept (the owner chose this over inline rich-text sprites).

## Consequences

- Positive: one consistent, resolution-independent set with no generated binary assets, no licensing questions, and plain-text diffs; adding a cargo stays data-only.
- Negative: the style is limited to flat shapes; illustrated art such as map sprites still needs another pipeline ([#69](https://github.com/sekoya-solutions/transport-tycoon-2/issues/69)).
- Follow-up: re-run the UI bootstrap (Transport Tycoon / UI / Ensure Game UI Assets) after adding a cargo icon so the generated icon set lists it; an EditMode test fails until it does.
