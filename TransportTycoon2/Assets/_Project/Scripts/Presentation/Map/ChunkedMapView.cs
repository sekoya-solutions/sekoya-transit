#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Renders a Core <see cref="TileMap"/> as ADR 0017 32×32 mesh chunks with dirty rebuilds.
    /// Each chunk has a ground mesh (flat terrain colors, ADR 0032) and, when map art is supplied, a track mesh that
    /// draws road and rail tiles from the baked atlas. Towns, industries, and stations are drawn by
    /// <see cref="MapObjectView"/>.
    /// </summary>
    public sealed class ChunkedMapView : IDisposable
    {
        // Height of the brown block-side strip along the map's front edges, in world units (14 px at 128 px per tile).
        private const float EdgeStripHeight = 0.11f;

        private readonly GeneratedWorld _world;
        private readonly DirtyChunkTracker _dirtyChunks;
        private readonly int _chunkCountPerSide;
        private readonly GameObject _root;
        private readonly MeshFilter[] _chunkFilters;
        private readonly MeshFilter?[] _trackFilters;
        private readonly Material _material;
        private readonly Material? _trackMaterial;
        private readonly List<int> _drainBuffer = new List<int>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();
        private bool _disposed;

        /// <summary>
        /// Builds every chunk mesh for the given world under a new root GameObject.
        /// Without <paramref name="art"/>, only the ground is drawn (tests and art-less scenes).
        /// </summary>
        public ChunkedMapView(GeneratedWorld world, Transform? parent, MapArtSet? art = null)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            _world = world;
            _dirtyChunks = new DirtyChunkTracker(world.Map.Size);
            _chunkCountPerSide = _dirtyChunks.ChunkCountPerSide;
            _trackMaterial = art != null ? art.TileAtlasMaterial : null;
            _material = CreateGroundMaterial(_trackMaterial);

            _root = new GameObject("ChunkedMap");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }

            int chunkCount = MapChunkGrid.GetChunkCount(world.Map.Size);
            _chunkFilters = new MeshFilter[chunkCount];
            _trackFilters = new MeshFilter?[chunkCount];
            for (int chunkY = 0; chunkY < _chunkCountPerSide; chunkY++)
            {
                for (int chunkX = 0; chunkX < _chunkCountPerSide; chunkX++)
                {
                    int chunkIndex = MapChunkGrid.GetChunkIndex(chunkX, chunkY, _chunkCountPerSide);
                    var chunkObject = new GameObject($"Chunk_{chunkX}_{chunkY}");
                    chunkObject.transform.SetParent(_root.transform, false);
                    _chunkFilters[chunkIndex] = CreateLayer(chunkObject, _material, MapSortingOrder.Ground);
                    if (_trackMaterial != null)
                    {
                        var trackObject = new GameObject("Tracks");
                        trackObject.transform.SetParent(chunkObject.transform, false);
                        _trackFilters[chunkIndex] = CreateLayer(trackObject, _trackMaterial, MapSortingOrder.Tracks);
                    }

                    RebuildChunk(chunkIndex);
                }
            }
        }

        public Transform Root
        {
            get
            {
                return _root.transform;
            }
        }

        /// <summary>
        /// Marks a tile and its four neighbors dirty: a road or rail change also changes the neighbors' shapes.
        /// </summary>
        public void MarkTileDirty(int tileX, int tileY)
        {
            ThrowIfDisposed();
            MarkIfInside(tileX, tileY);
            MarkIfInside(tileX + 1, tileY);
            MarkIfInside(tileX - 1, tileY);
            MarkIfInside(tileX, tileY + 1);
            MarkIfInside(tileX, tileY - 1);
        }

        public int RebuildDirtyChunks()
        {
            ThrowIfDisposed();
            int count = _dirtyChunks.DrainDirtyChunkIndexes(_drainBuffer);
            for (int index = 0; index < count; index++)
            {
                RebuildChunk(_drainBuffer[index]);
            }

            return count;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int index = 0; index < _chunkFilters.Length; index++)
            {
                DestroyMesh(_chunkFilters[index]);
                DestroyMesh(_trackFilters[index]);
            }

            if (_root != null)
            {
                DestroyUnityObject(_root);
            }

            DestroyUnityObject(_material);
        }

        private void MarkIfInside(int tileX, int tileY)
        {
            if (_world.Map.Contains(new TileCoordinate(tileX, tileY)))
            {
                _dirtyChunks.MarkTileDirty(tileX, tileY);
            }
        }

        private static MeshFilter CreateLayer(GameObject layerObject, Material material, int sortingOrder)
        {
            var filter = layerObject.AddComponent<MeshFilter>();
            var renderer = layerObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return filter;
        }

        private void RebuildChunk(int chunkIndex)
        {
            int chunkX = chunkIndex % _chunkCountPerSide;
            int chunkY = chunkIndex / _chunkCountPerSide;
            Replace(_chunkFilters[chunkIndex], BuildGroundMesh(chunkX, chunkY));
            MeshFilter? trackFilter = _trackFilters[chunkIndex];
            if (trackFilter != null)
            {
                Replace(trackFilter, BuildTrackMesh(chunkX, chunkY));
            }
        }

        private static void Replace(MeshFilter filter, Mesh mesh)
        {
            Mesh? previous = filter.sharedMesh;
            filter.sharedMesh = mesh;
            if (previous != null)
            {
                DestroyUnityObject(previous);
            }
        }

        private static void DestroyMesh(MeshFilter? filter)
        {
            if (filter != null && filter.sharedMesh != null)
            {
                DestroyUnityObject(filter.sharedMesh);
            }
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

        private Mesh BuildGroundMesh(int chunkX, int chunkY)
        {
            const int n = MapChunkGrid.ChunkSizeTiles;
            ClearBuffers();
            ulong seed = _world.MapSeed;
            for (int localY = 0; localY < n; localY++)
            {
                for (int localX = 0; localX < n; localX++)
                {
                    int tileX = (chunkX * n) + localX;
                    int tileY = (chunkY * n) + localY;
                    Vector3 center = IsometricProjection.TileToWorld(tileX, tileY);
                    Color32 fill = ToVertexColor(MapTerrainLook.ColorAt(seed, tileX, tileY));
                    Vector2 uvCenter = new Vector2(0.5f, 0.5f);

                    // Diamond: bottom, right, top, left vertices.
                    AddQuad(
                        new Vector3(center.x, center.y - 0.25f, 0f),
                        new Vector3(center.x + 0.5f, center.y, 0f),
                        new Vector3(center.x, center.y + 0.25f, 0f),
                        new Vector3(center.x - 0.5f, center.y, 0f),
                        fill,
                        uvCenter,
                        uvCenter,
                        uvCenter,
                        uvCenter);

                    // Front edges of the map: row y = 0 shows its lower-right side, column x = 0 its lower-left side.
                    if (tileY == 0)
                    {
                        AddEdgeStrip(
                            new Vector3(center.x + 0.5f, center.y, 0f),
                            new Vector3(center.x, center.y - 0.25f, 0f),
                            ToVertexColor(MapTerrainLook.EdgeRight));
                    }

                    if (tileX == 0)
                    {
                        AddEdgeStrip(
                            new Vector3(center.x, center.y - 0.25f, 0f),
                            new Vector3(center.x - 0.5f, center.y, 0f),
                            ToVertexColor(MapTerrainLook.EdgeLeft));
                    }
                }
            }

            return ToMesh($"ChunkMesh_{chunkX}_{chunkY}");
        }

        private Mesh BuildTrackMesh(int chunkX, int chunkY)
        {
            const int n = MapChunkGrid.ChunkSizeTiles;
            ClearBuffers();
            TileMap map = _world.Map;
            for (int localY = 0; localY < n; localY++)
            {
                for (int localX = 0; localX < n; localX++)
                {
                    var coordinate = new TileCoordinate((chunkX * n) + localX, (chunkY * n) + localY);
                    bool hasRoad = map.GetRoadTypeId(coordinate) != TileMap.NoRoadTypeId;
                    bool hasRail = map.GetRailTypeId(coordinate) != TileMap.NoRailTypeId;
                    if (hasRoad)
                    {
                        AddAtlasTile(coordinate, MapTileShapes.RoadCell(MapTileShapes.GetRoadMask(map, coordinate)));
                    }

                    if (hasRail)
                    {
                        int railMask = MapTileShapes.GetRailMask(map, coordinate);
                        AddAtlasTile(
                            coordinate,
                            hasRoad ? MapTileShapes.LevelCrossingRailCell(railMask) : MapTileShapes.RailCell(railMask));
                    }
                }
            }

            return ToMesh($"TrackMesh_{chunkX}_{chunkY}");
        }

        // The cell covers the diamond's bounding box (1 x 0.5 world units); its corners are transparent.
        private void AddAtlasTile(TileCoordinate coordinate, int cell)
        {
            Vector3 center = IsometricProjection.TileToWorld(coordinate.X, coordinate.Y);
            Rect uv = MapTileAtlas.GetUvRect(cell);
            AddQuad(
                new Vector3(center.x - 0.5f, center.y - 0.25f, 0f),
                new Vector3(center.x + 0.5f, center.y - 0.25f, 0f),
                new Vector3(center.x + 0.5f, center.y + 0.25f, 0f),
                new Vector3(center.x - 0.5f, center.y + 0.25f, 0f),
                new Color32(255, 255, 255, 255),
                new Vector2(uv.xMin, uv.yMin),
                new Vector2(uv.xMax, uv.yMin),
                new Vector2(uv.xMax, uv.yMax),
                new Vector2(uv.xMin, uv.yMax));
        }

        // A vertical strip hanging below a diamond edge from a to b.
        private void AddEdgeStrip(Vector3 a, Vector3 b, Color32 color)
        {
            var drop = new Vector3(0f, EdgeStripHeight, 0f);
            Vector2 uvCenter = new Vector2(0.5f, 0.5f);
            AddQuad(b - drop, a - drop, a, b, color, uvCenter, uvCenter, uvCenter, uvCenter);
        }

        // Adds a quad from four vertices in counter-clockwise screen order (front-facing for the map camera).
        private void AddQuad(
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Color32 color,
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv2,
            Vector2 uv3)
        {
            int start = _vertices.Count;
            _vertices.Add(v0);
            _vertices.Add(v1);
            _vertices.Add(v2);
            _vertices.Add(v3);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _uvs.Add(uv0);
            _uvs.Add(uv1);
            _uvs.Add(uv2);
            _uvs.Add(uv3);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 1);
            _triangles.Add(start);
            _triangles.Add(start + 3);
            _triangles.Add(start + 2);
        }

        // Terrain colors are sRGB like the art. Sprite renderers convert their color for a linear color space project;
        // raw mesh vertex colors are not converted, so the ground does it here to match the textures beside it.
        private static Color32 ToVertexColor(Color32 srgb)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? (Color32)((Color)srgb).linear : srgb;
        }

        private void ClearBuffers()
        {
            _vertices.Clear();
            _colors.Clear();
            _uvs.Clear();
            _triangles.Clear();
        }

        private Mesh ToMesh(string name)
        {
            var mesh = new Mesh
            {
                name = name,
            };
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // The ground is vertex-colored. With map art it uses the atlas material's sprite shader on a white texture
        // (that shader is in the build because the atlas material references it); without art it keeps the
        // original placeholder shader lookup.
        private static Material CreateGroundMaterial(Material? trackMaterial)
        {
            Shader? shader = trackMaterial != null ? trackMaterial.shader : null;
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("No suitable Unlit/Sprites shader was found for map chunks.");
            }

            var material = new Material(shader)
            {
                mainTexture = Texture2D.whiteTexture,
                color = Color.white,
                name = "ChunkedMapGround",
            };

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            return material;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChunkedMapView));
            }
        }
    }
}
