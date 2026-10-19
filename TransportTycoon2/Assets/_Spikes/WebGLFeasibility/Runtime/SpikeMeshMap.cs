#nullable enable
using UnityEngine;

namespace TransportTycoon.Spikes.WebGL
{
    // Candidate A: one mesh per 32x32-tile chunk, drawn with one shared material.
    public sealed class SpikeMeshMap
    {
        public SpikeMeshMap(Material template, Texture2D texture)
        {
            Root = new GameObject("MeshMap");
            var material = new Material(template) { mainTexture = texture };

            int chunks = SpikeIso.MapSize / SpikeIso.ChunkSize;
            for (int cy = 0; cy < chunks; cy++)
            {
                for (int cx = 0; cx < chunks; cx++)
                {
                    BuildChunk(cx, cy, material);
                }
            }
        }

        public GameObject Root { get; }

        private void BuildChunk(int cx, int cy, Material material)
        {
            const int n = SpikeIso.ChunkSize;
            var vertices = new Vector3[n * n * 4];
            var colors = new Color32[n * n * 4];
            var uvs = new Vector2[n * n * 4];
            var triangles = new int[n * n * 6];

            int v = 0;
            int t = 0;
            for (int ly = 0; ly < n; ly++)
            {
                for (int lx = 0; lx < n; lx++)
                {
                    int x = cx * n + lx;
                    int y = cy * n + ly;
                    Vector3 c = SpikeIso.TileToWorld(x, y);
                    Color32 color = SpikeIso.Palette[SpikeIso.TerrainAt(x, y)];

                    vertices[v] = new Vector3(c.x - 0.5f, c.y - 0.25f, 0f);
                    vertices[v + 1] = new Vector3(c.x + 0.5f, c.y - 0.25f, 0f);
                    vertices[v + 2] = new Vector3(c.x + 0.5f, c.y + 0.25f, 0f);
                    vertices[v + 3] = new Vector3(c.x - 0.5f, c.y + 0.25f, 0f);
                    uvs[v] = new Vector2(0f, 0f);
                    uvs[v + 1] = new Vector2(1f, 0f);
                    uvs[v + 2] = new Vector2(1f, 1f);
                    uvs[v + 3] = new Vector2(0f, 1f);
                    colors[v] = color;
                    colors[v + 1] = color;
                    colors[v + 2] = color;
                    colors[v + 3] = color;

                    triangles[t++] = v;
                    triangles[t++] = v + 2;
                    triangles[t++] = v + 1;
                    triangles[t++] = v;
                    triangles[t++] = v + 3;
                    triangles[t++] = v + 2;
                    v += 4;
                }
            }

            var mesh = new Mesh { name = $"Chunk {cx},{cy}" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            var go = new GameObject(mesh.name);
            go.transform.SetParent(Root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
