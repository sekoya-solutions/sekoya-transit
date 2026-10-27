#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.World;
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Temporary MapPlay-only world-space cargo totals above industries (Issue #132).
    /// Labels stay visible at 0; towns are deferred until passengers/mail.
    /// </summary>
    public sealed class MapIndustryCargoLabelView : IDisposable
    {
        private const float LabelWorldZ = -1.3f;

        // Above the industry building sprite (ADR 0032), whose roof reaches about 0.8 world units over the tile center.
        private const float LabelYOffset = 0.85f;

        private readonly GeneratedWorld _world;
        private readonly GameObject _root;
        private readonly Dictionary<int, TextMesh> _labels = new Dictionary<int, TextMesh>();
        private bool _disposed;

        /// <summary>
        /// Initializes an industry cargo label view bound to a Core world.
        /// </summary>
        public MapIndustryCargoLabelView(GeneratedWorld world, Transform? parent)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            _world = world;
            _root = new GameObject("MapIndustryCargoLabels");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }
        }

        /// <summary>
        /// Syncs label GameObjects to the current Core industry list and cargo totals.
        /// </summary>
        public void Sync()
        {
            ThrowIfDisposed();

            var seen = new HashSet<int>();
            for (int index = 0; index < _world.Industries.Count; index++)
            {
                PlacedIndustry industry = _world.Industries[index];
                seen.Add(index);
                if (!_labels.TryGetValue(index, out TextMesh? label) || label == null)
                {
                    label = CreateLabel(index);
                    _labels[index] = label;
                }

                Vector3 world = IsometricProjection.TileToWorld(industry.Coordinate.X, industry.Coordinate.Y);
                label.transform.position = new Vector3(world.x, world.y + LabelYOffset, LabelWorldZ);
                label.text = industry.Cargo.TotalAmount.ToString();
                label.gameObject.SetActive(true);
            }

            var removeIds = new List<int>();
            foreach (KeyValuePair<int, TextMesh> pair in _labels)
            {
                if (!seen.Contains(pair.Key))
                {
                    removeIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < removeIds.Count; index++)
            {
                int id = removeIds[index];
                if (_labels.TryGetValue(id, out TextMesh? label) && label != null)
                {
                    DestroyUnityObject(label.gameObject);
                }

                _labels.Remove(id);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (KeyValuePair<int, TextMesh> pair in _labels)
            {
                if (pair.Value != null)
                {
                    DestroyUnityObject(pair.Value.gameObject);
                }
            }

            _labels.Clear();
            DestroyUnityObject(_root);
            _disposed = true;
        }

        private TextMesh CreateLabel(int industryIndex)
        {
            var go = new GameObject($"IndustryCargoLabel_{industryIndex}");
            go.transform.SetParent(_root.transform, false);
            var text = go.AddComponent<TextMesh>();
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.12f;
            text.fontSize = 32;
            text.color = Color.white;
            go.GetComponent<MeshRenderer>().sortingOrder = MapSortingOrder.Labels;
            return text;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MapIndustryCargoLabelView));
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object obj)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }
}
