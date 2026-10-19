#nullable enable
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TransportTycoon.Spikes.WebGL
{
    // Candidate B: a single Unity Tilemap on an isometric grid (Unity chunks and culls it internally).
    public sealed class SpikeTilemapMap
    {
        public SpikeTilemapMap(Material template, Texture2D texture)
        {
            Root = new GameObject("TilemapMap");
            // Unity anchors isometric tiles at the cell centre plus half a cell; compensate to match TileToWorld.
            Root.transform.position = new Vector3(0f, -0.25f, 0f);

            var grid = Root.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);

            var tilemapObject = new GameObject("Tilemap");
            tilemapObject.transform.SetParent(Root.transform, false);
            var tilemap = tilemapObject.AddComponent<Tilemap>();
            var renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.sharedMaterial = new Material(template);

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, SpikeIso.TextureWidth, SpikeIso.TextureHeight),
                new Vector2(0.5f, 0.5f),
                SpikeIso.PixelsPerUnit);

            var tiles = new TileBase[SpikeIso.Palette.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.color = SpikeIso.Palette[i];
                tiles[i] = tile;
            }

            int size = SpikeIso.MapSize;
            var block = new TileBase[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    block[y * size + x] = tiles[SpikeIso.TerrainAt(x, y)];
                }
            }

            tilemap.SetTilesBlock(new BoundsInt(0, 0, 0, size, size, 1), block);
        }

        public GameObject Root { get; }
    }
}
