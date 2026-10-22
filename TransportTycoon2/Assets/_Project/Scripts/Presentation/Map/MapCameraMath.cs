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
        /// Default minimum visible overlap (world units) so the map stays partly on screen.
        /// </summary>
        public const float DefaultMinOverlap = 2f;

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
        /// Soft-clamps the camera center so the map AABB stays at least <paramref name="minOverlap"/>
        /// world units inside the orthographic viewport on each axis when possible.
        /// </summary>
        public static Vector2 ClampCameraCenter(
            Vector2 center,
            float mapMinX,
            float mapMaxX,
            float mapMinY,
            float mapMaxY,
            float viewportHalfWidth,
            float viewportHalfHeight,
            float minOverlap)
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

            if (minOverlap < 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(minOverlap),
                    minOverlap,
                    "Min overlap must be non-negative.");
            }

            float minX = mapMinX - viewportHalfWidth + minOverlap;
            float maxX = mapMaxX + viewportHalfWidth - minOverlap;
            float minY = mapMinY - viewportHalfHeight + minOverlap;
            float maxY = mapMaxY + viewportHalfHeight - minOverlap;

            if (minX > maxX)
            {
                float midX = (mapMinX + mapMaxX) * 0.5f;
                minX = midX;
                maxX = midX;
            }

            if (minY > maxY)
            {
                float midY = (mapMinY + mapMaxY) * 0.5f;
                minY = midY;
                maxY = midY;
            }

            return new Vector2(
                Mathf.Clamp(center.x, minX, maxX),
                Mathf.Clamp(center.y, minY, maxY));
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
