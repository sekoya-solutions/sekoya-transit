#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
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
        // Label and last shown amount per industry index. The amount is cached because TextMesh rebuilds its mesh on
        // every text assignment, and formatting it every frame allocated a string per industry (Issue #70).
        private readonly List<TextMesh> _labels = new List<TextMesh>();
        private readonly List<int> _shownAmounts = new List<int>();
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

            int industryCount = _world.Industries.Count;
            for (int index = 0; index < industryCount; index++)
            {
                PlacedIndustry industry = _world.Industries[index];
                if (index == _labels.Count)
                {
                    _labels.Add(CreateLabel(index));
                    _shownAmounts.Add(-1);
                }

                TextMesh label = _labels[index];
                if (label == null)
                {
                    label = CreateLabel(index);
                    _labels[index] = label;
                    _shownAmounts[index] = -1;
                }

                Vector3 world = IsometricProjection.TileToWorld(industry.Coordinate.X, industry.Coordinate.Y);
                label.transform.position = new Vector3(world.x, world.y + LabelYOffset, LabelWorldZ);
                int amount = industry.Cargo.TotalAmount;
                if (_shownAmounts[index] != amount)
                {
                    _shownAmounts[index] = amount;
                    label.text = amount.ToString(CultureInfo.InvariantCulture);
                }
            }

            // Industries are indexed by list position, so labels past the current count are stale.
            for (int index = _labels.Count - 1; index >= industryCount; index--)
            {
                if (_labels[index] != null)
                {
                    DestroyUnityObject(_labels[index].gameObject);
                }

                _labels.RemoveAt(index);
                _shownAmounts.RemoveAt(index);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            for (int index = 0; index < _labels.Count; index++)
            {
                if (_labels[index] != null)
                {
                    DestroyUnityObject(_labels[index].gameObject);
                }
            }

            _labels.Clear();
            _shownAmounts.Clear();
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
