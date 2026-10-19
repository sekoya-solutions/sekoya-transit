# ADR 0002: Render pipeline

- Status: Accepted
- Date: 2026-10-19
- Deciders: Owner

## Context
The game is 2D isometric with sprites, modern UI, and a WebGL target (see [product-vision.md](../specs/product-vision.md)).

## Options considered
1. **2D Universal Render Pipeline (URP)**: Unity's recommended pipeline, 2D Renderer with 2D lights and sorting, works on WebGL.
2. Built-In Render Pipeline: being deprecated.
3. High Definition Render Pipeline (HDRP): not suitable for 2D or WebGL.

## Decision
Use **URP with the 2D Renderer**.

## Consequences
- Shaders and materials must be URP-compatible (Shader Graph preferred).
- Lighting (day/night, effects) can use 2D lights later.
- WebGL performance budget must be validated early with a prototype.
