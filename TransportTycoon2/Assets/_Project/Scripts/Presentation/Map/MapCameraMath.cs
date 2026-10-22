#nullable enable
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Pure camera helpers for orthographic pan/zoom clamps (Presentation-only).
    /// Map world AABB matches <see cref="IsometricProjection"/> tile centers, expanded by half a diamond tile.
    /// </summary>
    public static class MapCameraMath
    {
        /// <summary>
        /// Half-width of an isometric tile diamond in world units.
        /// </summary>
        public const float TileHalfWidth = 0.5f;

        /// <summary>
        /// Half-height of an isometric tile diamond in world units.
        /// </summary>
        public const float TileHalfHeight = 0.25f;

        /// <summary>
        /// Computes the axis-aligned world bounds of a square isometric map.
        /// </summary>
        public static void GetMapWorldBounds(
            int sideLengthTiles,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            if (sideLengthTiles < 1)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(sideLengthTiles),
                    sideLengthTiles,
                    "Map side length must be at least 1.");
            }

            float centerExtent = (sideLengthTiles - 1) * 0.5f;
            minX = -centerExtent - TileHalfWidth;
            maxX = centerExtent + TileHalfWidth;
            minY = 0f - TileHalfHeight;
            maxY = ((sideLengthTiles - 1) * 0.5f) + TileHalfHeight;
        }

        /// <summary>
        /// Clamps orthographic size to the configured zoom range.
        /// </summary>
        public static float ClampZoom(float orthographicSize, float minSize, float maxSize)
        {
            if (minSize <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(minSize), minSize, "Min zoom must be positive.");
            }

            if (maxSize < minSize)
            {
                throw new System.ArgumentOutOfRangeException(nameof(maxSize), maxSize, "Max zoom must be >= min zoom.");
            }

            return Mathf.Clamp(orthographicSize, minSize, maxSize);
        }

        /// <summary>
        /// Applies a scroll-wheel zoom step. Positive scroll zooms in (smaller orthographic size).
        /// </summary>
        public static float ApplyScrollZoom(
            float orthographicSize,
            float scrollY,
            float zoomFactor,
            float minSize,
            float maxSize)
        {
            if (zoomFactor <= 0f || zoomFactor >= 1f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(zoomFactor),
                    zoomFactor,
                    "Zoom factor must be in (0, 1).");
            }

            if (scrollY == 0f)
            {
                return ClampZoom(orthographicSize, minSize, maxSize);
            }

            float next = scrollY > 0f
                ? orthographicSize * zoomFactor
                : orthographicSize / zoomFactor;
            return ClampZoom(next, minSize, maxSize);
        }

        /// <summary>
        /// Soft-clamps the camera center so that, when the map is larger than the viewport,
        /// the entire orthographic viewport stays inside the map AABB. When the viewport is
        /// larger than the map, the center is pinned to the map center (map stays fully framed).
        /// </summary>
        public static Vector2 ClampCameraCenter(
            Vector2 center,
            float mapMinX,
            float mapMaxX,
            float mapMinY,
            float mapMaxY,
            float viewportHalfWidth,
            float viewportHalfHeight)
        {
            if (viewportHalfWidth <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(viewportHalfWidth),
                    viewportHalfWidth,
                    "Viewport half-width must be positive.");
            }

            if (viewportHalfHeight <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(viewportHalfHeight),
                    viewportHalfHeight,
                    "Viewport half-height must be positive.");
            }

            float mapWidth = mapMaxX - mapMinX;
            float mapHeight = mapMaxY - mapMinY;
            float viewportWidth = viewportHalfWidth * 2f;
            float viewportHeight = viewportHalfHeight * 2f;

            float minX;
            float maxX;
            if (viewportWidth >= mapWidth)
            {
                float midX = (mapMinX + mapMaxX) * 0.5f;
                minX = midX;
                maxX = midX;
            }
            else
            {
                minX = mapMinX + viewportHalfWidth;
                maxX = mapMaxX - viewportHalfWidth;
            }

            float minY;
            float maxY;
            if (viewportHeight >= mapHeight)
            {
                float midY = (mapMinY + mapMaxY) * 0.5f;
                minY = midY;
                maxY = midY;
            }
            else
            {
                minY = mapMinY + viewportHalfHeight;
                maxY = mapMaxY - viewportHalfHeight;
            }

            return new Vector2(
                Mathf.Clamp(center.x, minX, maxX),
                Mathf.Clamp(center.y, minY, maxY));
        }

        /// <summary>
        /// Returns the fraction of the viewport AABB that intersects the map AABB (0..1).
        /// Used by tests to assert clamp quality without running Play Mode input.
        /// </summary>
        public static float GetViewportMapOverlapFraction(
            Vector2 center,
            float mapMinX,
            float mapMaxX,
            float mapMinY,
            float mapMaxY,
            float viewportHalfWidth,
            float viewportHalfHeight)
        {
            if (viewportHalfWidth <= 0f || viewportHalfHeight <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    viewportHalfWidth <= 0f ? nameof(viewportHalfWidth) : nameof(viewportHalfHeight),
                    "Viewport half-extents must be positive.");
            }

            float viewMinX = center.x - viewportHalfWidth;
            float viewMaxX = center.x + viewportHalfWidth;
            float viewMinY = center.y - viewportHalfHeight;
            float viewMaxY = center.y + viewportHalfHeight;

            float overlapW = Mathf.Max(0f, Mathf.Min(viewMaxX, mapMaxX) - Mathf.Max(viewMinX, mapMinX));
            float overlapH = Mathf.Max(0f, Mathf.Min(viewMaxY, mapMaxY) - Mathf.Max(viewMinY, mapMinY));
            float viewportArea = (viewportHalfWidth * 2f) * (viewportHalfHeight * 2f);
            return (overlapW * overlapH) / viewportArea;
        }

        /// <summary>
        /// Keyboard/game-style pan delta in world units for one frame.
        /// </summary>
        public static Vector2 KeyboardPanDelta(Vector2 moveInput, float orthographicSize, float panSpeed, float deltaTime)
        {
            if (panSpeed < 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(panSpeed), panSpeed, "Pan speed must be non-negative.");
            }

            if (deltaTime < 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Delta time must be non-negative.");
            }

            Vector2 direction = moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;
            return direction * (orthographicSize * panSpeed * deltaTime);
        }

        /// <summary>
        /// Converts a mouse-pixel drag into a world-space pan delta for an orthographic camera.
        /// </summary>
        public static Vector2 MouseDragPanDelta(Vector2 pixelDelta, float orthographicSize, float screenHeightPixels)
        {
            if (screenHeightPixels <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(screenHeightPixels),
                    screenHeightPixels,
                    "Screen height must be positive.");
            }

            // Match spike feel: one screen-height of drag moves 2 * orthographicSize world units.
            float worldPerPixel = (2f * orthographicSize) / screenHeightPixels;
            return -pixelDelta * worldPerPixel;
        }
    }
}
