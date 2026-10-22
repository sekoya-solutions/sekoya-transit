#nullable enable
using System;
using TransportTycoon.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Plain-sprite overlays for hover, sticky selection, and 1x1 placement ghosts (ADR 0017).
    /// </summary>
    public sealed class MapTileOverlayView : IDisposable
    {
        private const float OverlayWorldZ = -0.1f;

        private readonly GameObject _root;
        private readonly SpriteRenderer _hover;
        private readonly SpriteRenderer _selection;
        private readonly SpriteRenderer _ghost;
        private readonly Texture2D _diamondTexture;
        private readonly Sprite _diamondSprite;
        private bool _disposed;

        /// <summary>
        /// Creates overlay sprites under an optional parent transform.
        /// </summary>
        public MapTileOverlayView(Transform? parent)
        {
            _diamondTexture = CreateDiamondTexture();
            _diamondSprite = Sprite.Create(
                _diamondTexture,
                new Rect(0f, 0f, _diamondTexture.width, _diamondTexture.height),
                new Vector2(0.5f, 0.5f),
                _diamondTexture.width);

            _root = new GameObject("TileOverlays");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }

            _hover = CreateOverlayRenderer("Hover", 10);
            _selection = CreateOverlayRenderer("Selection", 11);
            _ghost = CreateOverlayRenderer("Ghost", 12);
            HideAll();
        }

        /// <summary>
        /// Shows or hides the hover highlight on a tile.
        /// </summary>
        public void SetHover(TileCoordinate? tile)
        {
            ThrowIfDisposed();
            ApplyOverlay(_hover, tile, MapPlaceholderPalette.TileHover);
        }

        /// <summary>
        /// Shows or hides the sticky selection highlight on a tile.
        /// </summary>
        public void SetSelection(TileCoordinate? tile)
        {
            ThrowIfDisposed();
            ApplyOverlay(_selection, tile, MapPlaceholderPalette.TileSelection);
        }

        /// <summary>
        /// Shows or hides the 1x1 placement ghost with valid/invalid tint.
        /// </summary>
        public void SetGhost(TileCoordinate? tile, bool isValid)
        {
            ThrowIfDisposed();
            Color32 color = isValid ? MapPlaceholderPalette.GhostValid : MapPlaceholderPalette.GhostInvalid;
            ApplyOverlay(_ghost, tile, color);
        }

        /// <summary>
        /// Releases Unity objects owned by this view.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_root != null)
            {
                DestroyUnityObject(_root);
            }

            DestroyUnityObject(_diamondSprite);
            DestroyUnityObject(_diamondTexture);
        }

        private SpriteRenderer CreateOverlayRenderer(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _diamondSprite;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        private static void ApplyOverlay(SpriteRenderer renderer, TileCoordinate? tile, Color32 color)
        {
            if (!tile.HasValue)
            {
                renderer.enabled = false;
                return;
            }

            TileCoordinate coordinate = tile.Value;
            Vector3 center = IsometricProjection.TileToWorld(coordinate.X, coordinate.Y);
            renderer.transform.position = new Vector3(center.x, center.y, OverlayWorldZ);
            renderer.color = color;
            renderer.enabled = true;
        }

        private void HideAll()
        {
            _hover.enabled = false;
            _selection.enabled = false;
            _ghost.enabled = false;
        }

        private static Texture2D CreateDiamondTexture()
        {
            const int width = 64;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "TileOverlayDiamond",
            };
            var pixels = new Color32[width * height];
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    float dx = Mathf.Abs(px + 0.5f - (width * 0.5f)) / (width * 0.5f);
                    float dy = Mathf.Abs(py + 0.5f - (height * 0.5f)) / (height * 0.5f);
                    bool inside = dx + dy <= 1f;
                    pixels[(py * width) + px] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void DestroyUnityObject(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(unityObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(unityObject);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MapTileOverlayView));
            }
        }
    }
}
