#nullable enable
using System;
using System.Collections.Generic;
using SekoyaTransit.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// Draws the map objects of ADR 0032 as depth-sorted sprites: a house per town and the industry's building (generic
    /// fallback for ids without art), each one sprite over its 2x2 block (Issue #219), and the one-tile road or rail
    /// station building.
    /// </summary>
    public sealed class MapObjectView : IDisposable
    {
        private readonly GeneratedWorld _world;
        private readonly MapArtSet _art;
        private readonly GameObject _root;
        private readonly Dictionary<int, SpriteRenderer> _renderers = new Dictionary<int, SpriteRenderer>();
        private bool _disposed;

        public MapObjectView(GeneratedWorld world, Transform? parent, MapArtSet art)
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
            _root = new GameObject("MapObjects");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }

            TileMap map = world.Map;
            for (int tileId = 0; tileId < map.TileCount; tileId++)
            {
                RefreshTile(map.GetCoordinate(tileId));
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
            Sprite? sprite = ResolveSprite(map, coordinate, out bool isSite);
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
                Vector3 center = isSite
                    ? IsometricProjection.SiteCenterToWorld(coordinate)
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

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _renderers.Clear();
            DestroyUnityObject(_root);
        }

        private Sprite? ResolveSprite(TileMap map, TileCoordinate coordinate, out bool isSite)
        {
            isSite = false;
            int objectId = map.GetObjectId(coordinate);
            if (objectId != TileMap.NoObjectId)
            {
                // Only the anchor tile of a site draws; its other three tiles share the same object ID.
                int townCount = _world.TownAnchors.Count;
                if (objectId < townCount)
                {
                    isSite = true;
                    return _world.TownAnchors[objectId].Equals(coordinate) ? _art.GetHouse(objectId) : null;
                }

                int industryIndex = objectId - townCount;
                if (industryIndex >= _world.Industries.Count)
                {
                    return null;
                }

                PlacedIndustry industry = _world.Industries[industryIndex];
                isSite = true;
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
