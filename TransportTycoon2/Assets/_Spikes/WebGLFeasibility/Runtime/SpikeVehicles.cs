#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TransportTycoon.Spikes.WebGL
{
    // Pooled sprite vehicles random-walking over the map; stands in for presentation of simulated entities.
    public sealed class SpikeVehicles
    {
        private const float Speed = 3f;
        private readonly Transform _root;
        private readonly Sprite _sprite;
        private readonly Material _material;
        private readonly List<GameObject> _pool = new List<GameObject>();
        private float[] _x = new float[0];
        private float[] _y = new float[0];
        private sbyte[] _dx = new sbyte[0];
        private sbyte[] _dy = new sbyte[0];
        private uint[] _rng = new uint[0];
        private int _active;

        public SpikeVehicles(Material template)
        {
            _root = new GameObject("Vehicles").transform;
            _material = new Material(template);
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _material.mainTexture = texture;
            _sprite = Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 24f);
        }

        public int Count => _active;

        public void SetCount(int count)
        {
            if (count > _pool.Count)
            {
                Grow(count);
            }

            for (int i = 0; i < _pool.Count; i++)
            {
                bool on = i < count;
                if (_pool[i].activeSelf != on)
                {
                    _pool[i].SetActive(on);
                }
            }

            _active = count;
        }

        public void Tick(float dt)
        {
            const float max = SpikeIso.MapSize - 1f;
            for (int i = 0; i < _active; i++)
            {
                float x = _x[i] + _dx[i] * Speed * dt;
                float y = _y[i] + _dy[i] * Speed * dt;
                if (x < 0f || x > max || y < 0f || y > max)
                {
                    _dx[i] = (sbyte)-_dx[i];
                    _dy[i] = (sbyte)-_dy[i];
                    x = Mathf.Clamp(x, 0f, max);
                    y = Mathf.Clamp(y, 0f, max);
                }
                else if ((int)x != (int)_x[i] || (int)y != (int)_y[i])
                {
                    Turn(i);
                }

                _x[i] = x;
                _y[i] = y;
                _pool[i].transform.position = SpikeIso.TileToWorld(x, y) + new Vector3(0f, 0f, -0.1f);
            }
        }

        private void Grow(int count)
        {
            int old = _pool.Count;
            System.Array.Resize(ref _x, count);
            System.Array.Resize(ref _y, count);
            System.Array.Resize(ref _dx, count);
            System.Array.Resize(ref _dy, count);
            System.Array.Resize(ref _rng, count);

            for (int i = old; i < count; i++)
            {
                _rng[i] = (uint)(i * 2654435761u + 12345u) | 1u;
                _x[i] = Next(i) % SpikeIso.MapSize;
                _y[i] = Next(i) % SpikeIso.MapSize;
                Turn(i);

                var go = new GameObject("Vehicle");
                go.transform.SetParent(_root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _material;
                sr.sprite = _sprite;
                sr.sortingOrder = 1;
                sr.color = Color.HSVToRGB((i % 16) / 16f, 0.8f, 1f);
                go.SetActive(false);
                _pool.Add(go);
            }
        }

        private void Turn(int i)
        {
            uint r = Next(i);
            if ((r & 3) != 0 && (_dx[i] != 0 || _dy[i] != 0))
            {
                return;
            }

            switch ((r >> 4) & 3)
            {
                case 0: _dx[i] = 1; _dy[i] = 0; break;
                case 1: _dx[i] = -1; _dy[i] = 0; break;
                case 2: _dx[i] = 0; _dy[i] = 1; break;
                default: _dx[i] = 0; _dy[i] = -1; break;
            }
        }

        private uint Next(int i)
        {
            uint s = _rng[i];
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            _rng[i] = s;
            return s;
        }
    }
}
