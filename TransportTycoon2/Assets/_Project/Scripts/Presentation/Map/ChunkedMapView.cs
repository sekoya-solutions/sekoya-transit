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
    /// </summary>
    public sealed class ChunkedMapView : IDisposable
    {
        private readonly GeneratedWorld _world;
        private readonly DirtyChunkTracker _dirtyChunks;
        private readonly int _chunkCountPerSide;
        private readonly GameObject _root;
        private readonly MeshFilter[] _chunkFilters;
        private readonly Material _material;
        private readonly Texture2D _diamondTexture;
        private readonly List<int> _drainBuffer = new List<int>();
        private bool _disposed;

        /// <summary>
        /// Builds every chunk mesh for the given world under a new root GameObject.
        /// </summary>
        public ChunkedMapView(GeneratedWorld world, Transform? parent)
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

            _diamondTexture = CreateDiamondTexture();
            Shader? shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("No suitable Unlit/Sprites shader was found for map chunks.");
            }

            _material = new Material(shader)
            {
                mainTexture = _diamondTexture,
                color = Color.white,
                name = "ChunkedMapPlaceholder",
            };

            if (_material.HasProperty("_BaseMap"))
            {
                _material.SetTexture("_BaseMap", _diamondTexture);
            }

            if (_material.HasProperty("_BaseColor"))
            {
                _material.SetColor("_BaseColor", Color.white);
            }

            _root = new GameObject("ChunkedMap");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }

            int chunkCount = MapChunkGrid.GetChunkCount(world.Map.Size);
            _chunkFilters = new MeshFilter[chunkCount];
            for (int chunkY = 0; chunkY < _chunkCountPerSide; chunkY++)
            {
                for (int chunkX = 0; chunkX < _chunkCountPerSide; chunkX++)
                {
                    int chunkIndex = MapChunkGrid.GetChunkIndex(chunkX, chunkY, _chunkCountPerSide);
                    var chunkObject = new GameObject($"Chunk_{chunkX}_{chunkY}");
                    chunkObject.transform.SetParent(_root.transform, false);
                    var filter = chunkObject.AddComponent<MeshFilter>();
                    var renderer = chunkObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = _material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    _chunkFilters[chunkIndex] = filter;
                    RebuildChunk(chunkIndex);
                }
            }
        }

        /// <summary>
        /// Gets the root transform that owns all chunk meshes.
        /// </summary>
        public Transform Root
        {
            get
            {
                return _root.transform;
            }
        }

        /// <summary>
        /// Marks the chunk containing the tile dirty. Call <see cref="RebuildDirtyChunks"/> to apply.
        /// </summary>
        public void MarkTileDirty(int tileX, int tileY)
        {
            ThrowIfDisposed();
            _dirtyChunks.MarkTileDirty(tileX, tileY);
        }

        /// <summary>
        /// Rebuilds only chunks marked dirty since the last drain.
        /// </summary>
        /// <returns>The number of chunks rebuilt.</returns>
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

        /// <summary>
        /// Releases Unity mesh and material resources created by this view.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (int index = 0; index < _chunkFilters.Length; index++)
            {
                MeshFilter filter = _chunkFilters[index];
                if (filter != null && filter.sharedMesh != null)
                {
                    DestroyUnityObject(filter.sharedMesh);
                }
            }

            if (_root != null)
            {
                DestroyUnityObject(_root);
            }

            DestroyUnityObject(_material);
            DestroyUnityObject(_diamondTexture);
        }

        private void RebuildChunk(int chunkIndex)
        {
            int chunkX = chunkIndex % _chunkCountPerSide;
            int chunkY = chunkIndex / _chunkCountPerSide;
            Mesh mesh = BuildChunkMesh(chunkX, chunkY);
            MeshFilter filter = _chunkFilters[chunkIndex];
            Mesh? previous = filter.sharedMesh;
            filter.sharedMesh = mesh;
            if (previous != null)
            {
                DestroyUnityObject(previous);
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

        private Mesh BuildChunkMesh(int chunkX, int chunkY)
        {
            const int n = MapChunkGrid.ChunkSizeTiles;
            var vertices = new Vector3[n * n * 4];
            var colors = new Color32[n * n * 4];
            var uvs = new Vector2[n * n * 4];
            var triangles = new int[n * n * 6];
            TileMap map = _world.Map;
            int townCount = _world.TownAnchors.Count;

            int v = 0;
            int t = 0;
            for (int localY = 0; localY < n; localY++)
            {
                for (int localX = 0; localX < n; localX++)
                {
                    int tileX = (chunkX * n) + localX;
                    int tileY = (chunkY * n) + localY;
                    var coordinate = new TileCoordinate(tileX, tileY);
                    Vector3 center = IsometricProjection.TileToWorld(tileX, tileY);
                    Color32 fill = ResolveTileColor(map, coordinate, townCount);

                    // Diamond corners; slight inset on the grid edge color is applied per-vertex below.
                    vertices[v] = new Vector3(center.x - 0.5f, center.y - 0.25f, 0f);
                    vertices[v + 1] = new Vector3(center.x + 0.5f, center.y - 0.25f, 0f);
                    vertices[v + 2] = new Vector3(center.x + 0.5f, center.y + 0.25f, 0f);
                    vertices[v + 3] = new Vector3(center.x - 0.5f, center.y + 0.25f, 0f);

                    // Darken diamond tips slightly so tile boundaries read as a grid.
                    Color32 edge = MapPlaceholderPalette.GridLine;
                    colors[v] = BlendToward(fill, edge, 0.35f);
                    colors[v + 1] = BlendToward(fill, edge, 0.35f);
                    colors[v + 2] = BlendToward(fill, edge, 0.35f);
                    colors[v + 3] = BlendToward(fill, edge, 0.35f);

                    uvs[v] = new Vector2(0f, 0f);
                    uvs[v + 1] = new Vector2(1f, 0f);
                    uvs[v + 2] = new Vector2(1f, 1f);
                    uvs[v + 3] = new Vector2(0f, 1f);

                    triangles[t++] = v;
                    triangles[t++] = v + 2;
                    triangles[t++] = v + 1;
                    triangles[t++] = v;
                    triangles[t++] = v + 3;
                    triangles[t++] = v + 2;
                    v += 4;
                }
            }

            var mesh = new Mesh
            {
                name = $"ChunkMesh_{chunkX}_{chunkY}",
            };
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color32 ResolveTileColor(TileMap map, TileCoordinate coordinate, int townCount)
        {
            int objectId = map.GetObjectId(coordinate);
            if (objectId >= 0)
            {
                if (objectId < townCount)
                {
                    return MapPlaceholderPalette.Town;
                }

                return MapPlaceholderPalette.Industry;
            }

            if (map.GetStationId(coordinate) != TileMap.NoStationId)
            {
                return MapPlaceholderPalette.RoadStation;
            }

            if (map.GetRoadTypeId(coordinate) != TileMap.NoRoadTypeId)
            {
                return MapPlaceholderPalette.RoadReserved;
            }

            return MapPlaceholderPalette.TerrainColor(map.GetTerrainTypeId(coordinate));
        }

        private static Color32 BlendToward(Color32 from, Color32 toward, float towardWeight)
        {
            float keep = 1f - towardWeight;
            return new Color32(
                (byte)((from.r * keep) + (toward.r * towardWeight)),
                (byte)((from.g * keep) + (toward.g * towardWeight)),
                (byte)((from.b * keep) + (toward.b * towardWeight)),
                255);
        }

        private static Texture2D CreateDiamondTexture()
        {
            const int width = 64;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "IsoDiamondPlaceholder",
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChunkedMapView));
            }
        }
    }
}
