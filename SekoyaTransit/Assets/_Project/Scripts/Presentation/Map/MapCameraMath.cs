#nullable enable
using UnityEngine;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Pure camera helpers for orthographic pan/zoom clamps (Presentation-only).
    /// The playable map is an isometric diamond (rhombus); clamps use that shape, not its AABB.
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
        /// Computes the axis-aligned world bounds of a square isometric map (AABB of the diamond).
        /// </summary>
        public static void GetMapWorldBounds(
            int sideLengthTiles,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            GetMapDiamond(sideLengthTiles, out float centerX, out float centerY, out float halfWidth, out float halfHeight);
            minX = centerX - halfWidth;
            maxX = centerX + halfWidth;
            minY = centerY - halfHeight;
            maxY = centerY + halfHeight;
        }

        /// <summary>
        /// Computes the isometric map diamond (center + half-extents) including half-tile padding.
        /// </summary>
        public static void GetMapDiamond(
            int sideLengthTiles,
            out float centerX,
            out float centerY,
            out float halfWidth,
            out float halfHeight)
        {
            if (sideLengthTiles < 1)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(sideLengthTiles),
                    sideLengthTiles,
                    "Map side length must be at least 1.");
            }

            float centerExtent = (sideLengthTiles - 1) * 0.5f;
            centerX = 0f;
            centerY = centerExtent * 0.5f;
            halfWidth = centerExtent + TileHalfWidth;
            halfHeight = centerExtent * 0.5f + TileHalfHeight;
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
        /// Soft-clamps the camera center to the isometric map diamond (L1 manhattan ≤ 1).
        /// This blocks escapes into empty AABB pockets along diamond sides, while still allowing
        /// the center to reach tips and edges. Requiring the whole viewport inside the diamond
        /// is too strict and pins the camera at normal zoom levels.
        /// When the viewport fully contains the diamond, the center is pinned to the map center.
        /// </summary>
        public static Vector2 ClampCameraCenter(
            Vector2 center,
            float diamondCenterX,
            float diamondCenterY,
            float diamondHalfWidth,
            float diamondHalfHeight,
            float viewportHalfWidth,
            float viewportHalfHeight)
        {
            if (diamondHalfWidth <= 0f || diamondHalfHeight <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    diamondHalfWidth <= 0f ? nameof(diamondHalfWidth) : nameof(diamondHalfHeight),
                    "Diamond half-extents must be positive.");
            }

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

            // Zoomed out: map fits inside the view — keep framed on center.
            if (viewportHalfWidth >= diamondHalfWidth && viewportHalfHeight >= diamondHalfHeight)
            {
                return new Vector2(diamondCenterX, diamondCenterY);
            }

            return ClampPointToDiamond(
                center,
                diamondCenterX,
                diamondCenterY,
                diamondHalfWidth,
                diamondHalfHeight,
                1f);
        }

        /// <summary>
        /// Returns the diamond (L1) manhattan radius of a point, 0 at center, 1 on the diamond edge.
        /// </summary>
        public static float GetDiamondManhattan(
            Vector2 point,
            float diamondCenterX,
            float diamondCenterY,
            float diamondHalfWidth,
            float diamondHalfHeight)
        {
            float nx = (point.x - diamondCenterX) / diamondHalfWidth;
            float ny = (point.y - diamondCenterY) / diamondHalfHeight;
            return Mathf.Abs(nx) + Mathf.Abs(ny);
        }

        /// <summary>
        /// True when all four orthographic viewport corners lie inside the map diamond (manhattan ≤ 1).
        /// </summary>
        public static bool AreViewportCornersInsideDiamond(
            Vector2 center,
            float diamondCenterX,
            float diamondCenterY,
            float diamondHalfWidth,
            float diamondHalfHeight,
            float viewportHalfWidth,
            float viewportHalfHeight)
        {
            Vector2[] corners =
            {
                new Vector2(center.x - viewportHalfWidth, center.y - viewportHalfHeight),
                new Vector2(center.x - viewportHalfWidth, center.y + viewportHalfHeight),
                new Vector2(center.x + viewportHalfWidth, center.y - viewportHalfHeight),
                new Vector2(center.x + viewportHalfWidth, center.y + viewportHalfHeight),
            };

            const float epsilon = 0.0001f;
            for (int index = 0; index < corners.Length; index++)
            {
                float manhattan = GetDiamondManhattan(
                    corners[index],
                    diamondCenterX,
                    diamondCenterY,
                    diamondHalfWidth,
                    diamondHalfHeight);
                if (manhattan > 1f + epsilon)
                {
                    return false;
                }
            }

            return true;
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

        private static Vector2 ClampPointToDiamond(
            Vector2 point,
            float diamondCenterX,
            float diamondCenterY,
            float diamondHalfWidth,
            float diamondHalfHeight,
            float maxManhattan)
        {
            if (maxManhattan <= 0f)
            {
                return new Vector2(diamondCenterX, diamondCenterY);
            }

            float nx = (point.x - diamondCenterX) / diamondHalfWidth;
            float ny = (point.y - diamondCenterY) / diamondHalfHeight;
            float manhattan = Mathf.Abs(nx) + Mathf.Abs(ny);
            if (manhattan <= maxManhattan || manhattan < 0.000001f)
            {
                return point;
            }

            float scale = maxManhattan / manhattan;
            return new Vector2(
                diamondCenterX + (nx * scale * diamondHalfWidth),
                diamondCenterY + (ny * scale * diamondHalfHeight));
        }
    }
}
