#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Draws the map objects of ADR 0032 as depth-sorted sprites: a town sprite per town and the industry's building (generic
    /// fallback for ids without art), each one sprite over its block (2x2 industries, Issue #219; 3x3 towns, Issue #250), and the one-tile road or rail
    /// station building. Each town and industry block also gets a darker ground outline (Issue #258).
    /// </summary>
    public sealed class MapObjectView : IDisposable
    {
        private const int BorderPixelsPerUnit = 64;
        private const float BorderThicknessPixels = 3.5f;
        private const float BorderAlpha = 120f;

        private readonly GeneratedWorld _world;
        private readonly List<SpriteRenderer> _borders = new List<SpriteRenderer>();
        private readonly Dictionary<int, Sprite> _borderSprites = new Dictionary<int, Sprite>();
        private readonly MapArtSet _art;
        private readonly IReadOnlyList<int>? _townTierMinPopulations;
        // Growth tier whose sprite each town currently draws (0 = not drawn yet).
        private readonly int[] _townTiersShown;
        private readonly GameObject _root;
        private readonly Dictionary<int, SpriteRenderer> _renderers = new Dictionary<int, SpriteRenderer>();
        private bool _disposed;

        /// <summary>
        /// Draws the map objects. <paramref name="townTierMinPopulations"/> lists the minimum population of each town
        /// growth tier (Issue #292); a town draws the sprite of its current tier. Without it every town draws tier 1.
        /// </summary>
        public MapObjectView(
            GeneratedWorld world,
            Transform? parent,
            MapArtSet art,
            IReadOnlyList<int>? townTierMinPopulations = null)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (art is null)
            {
                throw new ArgumentNullException(nameof(art));
            }
#pragma warning restore CA1510

            _world = world;
            _art = art;
            _townTierMinPopulations = townTierMinPopulations;
            _townTiersShown = new int[world.TownAnchors.Count];
            _root = new GameObject("MapObjects");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }

            for (int i = 0; i < world.TownAnchors.Count; i++)
            {
                AddFootprintBorder(world.TownAnchors[i], IsometricProjection.TownSizeTiles);
            }

            for (int i = 0; i < world.Industries.Count; i++)
            {
                AddFootprintBorder(world.Industries[i].Coordinate, IsometricProjection.IndustrySizeTiles);
            }

            TileMap map = world.Map;
            for (int tileId = 0; tileId < map.TileCount; tileId++)
            {
                RefreshTile(map.GetCoordinate(tileId));
            }
        }

        /// <summary>
        /// The number of ground borders drawn around towns and industries (Issue #258).
        /// </summary>
        public int FootprintBorderCount
        {
            get
            {
                return _borders.Count;
            }
        }

        public int SpriteCount
        {
            get
            {
                return _renderers.Count;
            }
        }

        /// <summary>
        /// Re-reads one tile after a build or demolish and adds, swaps, or removes its sprite.
        /// </summary>
        public void RefreshTile(TileCoordinate coordinate)
        {
            ThrowIfDisposed();
            TileMap map = _world.Map;
            int tileId = map.GetTileId(coordinate);
            Sprite? sprite = ResolveSprite(map, coordinate, out int siteSize);
            _renderers.TryGetValue(tileId, out SpriteRenderer? renderer);
            if (sprite == null)
            {
                if (renderer != null)
                {
                    DestroyUnityObject(renderer.gameObject);
                }

                _renderers.Remove(tileId);
                return;
            }

            if (renderer == null)
            {
                var go = new GameObject($"Object_{coordinate.X}_{coordinate.Y}");
                go.transform.SetParent(_root.transform, false);
                Vector3 center = siteSize > 0
                    ? IsometricProjection.SiteCenterToWorld(coordinate, siteSize)
                    : IsometricProjection.TileToWorld(coordinate.X, coordinate.Y);
                go.transform.position = new Vector3(center.x, center.y, 0f);
                renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = MapSortingOrder.ForWorldY(center.y);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _renderers[tileId] = renderer;
            }

            renderer.sprite = sprite;
        }

        /// <summary>
        /// Swaps the sprite of every town whose population moved it into another growth tier (Issue #292).
        /// </summary>
        public void RefreshTownTiers()
        {
            ThrowIfDisposed();
            for (int townId = 0; townId < _townTiersShown.Length; townId++)
            {
                if (_townTiersShown[townId] != TownTier(townId))
                {
                    RefreshTile(_world.TownAnchors[townId]);
                }
            }
        }

        private int TownTier(int townId)
        {
            return townId < _world.Towns.Count
                ? MapArtSet.TownTierForPopulation(_world.Towns[townId].Population, _townTierMinPopulations)
                : 1;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _renderers.Clear();
            foreach (Sprite sprite in _borderSprites.Values)
            {
                DestroyUnityObject(sprite.texture);
                DestroyUnityObject(sprite);
            }

            _borderSprites.Clear();
            _borders.Clear();
            DestroyUnityObject(_root);
        }

        /// <summary>
        /// Draws a darker outline on the ground around a site's block of tiles, under its building (Issue #258).
        /// </summary>
        private void AddFootprintBorder(TileCoordinate anchor, int sizeTiles)
        {
            if (!_borderSprites.TryGetValue(sizeTiles, out Sprite? sprite))
            {
                sprite = CreateBorderSprite(sizeTiles);
                _borderSprites[sizeTiles] = sprite;
            }

            var go = new GameObject($"Footprint_{anchor.X}_{anchor.Y}");
            go.transform.SetParent(_root.transform, false);
            Vector3 center = IsometricProjection.SiteCenterToWorld(anchor, sizeTiles);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = MapSortingOrder.Tracks + 1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _borders.Add(renderer);
        }

        private static Sprite CreateBorderSprite(int sizeTiles)
        {
            // One tile is a 1 x 0.5 world-unit diamond, so a block is sizeTiles x (sizeTiles / 2) world units.
            int width = BorderPixelsPerUnit * sizeTiles;
            int height = width / 2;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "SiteFootprintBorder",
            };
            var pixels = new Color32[width * height];
            // Perpendicular distance to a diamond edge (w:h = 2:1) is d * w / sqrt(20), d being 1 - (|dx| + |dy|).
            float edgeScale = width / Mathf.Sqrt(20f);
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    float dx = Mathf.Abs(px + 0.5f - (width * 0.5f)) / (width * 0.5f);
                    float dy = Mathf.Abs(py + 0.5f - (height * 0.5f)) / (height * 0.5f);
                    float distance = (1f - (dx + dy)) * edgeScale;
                    float coverage = Mathf.Clamp01(distance + 0.5f) * Mathf.Clamp01(BorderThicknessPixels - distance + 0.5f);
                    pixels[(py * width) + px] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(BorderAlpha * coverage));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                BorderPixelsPerUnit);
        }

        private Sprite? ResolveSprite(TileMap map, TileCoordinate coordinate, out int siteSize)
        {
            siteSize = 0;
            int objectId = map.GetObjectId(coordinate);
            if (objectId != TileMap.NoObjectId)
            {
                // Only the anchor tile of a site draws; its other three tiles share the same object ID.
                int townCount = _world.TownAnchors.Count;
                if (objectId < townCount)
                {
                    siteSize = IsometricProjection.TownSizeTiles;
                    if (!_world.TownAnchors[objectId].Equals(coordinate))
                    {
                        return null;
                    }

                    int tier = TownTier(objectId);
                    _townTiersShown[objectId] = tier;
                    return _art.GetTownTier(tier);
                }

                int industryIndex = objectId - townCount;
                if (industryIndex >= _world.Industries.Count)
                {
                    return null;
                }

                PlacedIndustry industry = _world.Industries[industryIndex];
                siteSize = IsometricProjection.IndustrySizeTiles;
                return industry.Coordinate.Equals(coordinate) ? _art.GetIndustry(industry.IndustryId) : null;
            }

            if (map.GetStationId(coordinate) != TileMap.NoStationId)
            {
                return _art.RoadStation;
            }

            if (map.GetRailStationId(coordinate) != TileMap.NoRailStationId)
            {
                return _art.RailStation;
            }

            return null;
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
                throw new ObjectDisposedException(nameof(MapObjectView));
            }
        }
    }
}
