#nullable enable
using System;
using System.Collections.Generic;
using TransportTycoon.Core.World;
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// Temporary MapPlay-only world-space cargo totals above road stations.
    /// </summary>
    public sealed class MapStationCargoLabelView : IDisposable
    {
        private const float LabelWorldZ = -1.3f;
        private const float LabelYOffset = 0.28f;

        private readonly GeneratedWorld _world;
        private readonly GameObject _root;
        private readonly Dictionary<int, TextMesh> _labels = new Dictionary<int, TextMesh>();
        private bool _disposed;

        /// <summary>
        /// Initializes a station cargo label view bound to a Core world.
        /// </summary>
        public MapStationCargoLabelView(GeneratedWorld world, Transform? parent)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            _world = world;
            _root = new GameObject("MapStationCargoLabels");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }
        }

        /// <summary>
        /// Syncs label GameObjects to the current Core station list and cargo totals.
        /// </summary>
        public void Sync()
        {
            ThrowIfDisposed();

            var seen = new HashSet<int>();
            for (int index = 0; index < _world.RoadStations.Count; index++)
            {
                RoadStation station = _world.RoadStations[index];
                seen.Add(station.Id);
                if (!_labels.TryGetValue(station.Id, out TextMesh? label) || label == null)
                {
                    label = CreateLabel(station.Id);
                    _labels[station.Id] = label;
                }

                Vector3 world = IsometricProjection.TileToWorld(station.Coordinate.X, station.Coordinate.Y);
                label.transform.position = new Vector3(world.x, world.y + LabelYOffset, LabelWorldZ);
                label.text = station.Cargo.TotalAmount.ToString();
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

            _disposed = true;
            DestroyUnityObject(_root);
            _labels.Clear();
        }

        private TextMesh CreateLabel(int stationId)
        {
            var go = new GameObject("StationCargo_" + stationId);
            go.transform.SetParent(_root.transform, false);
            var textMesh = go.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.LowerCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.08f;
            textMesh.fontSize = 32;
            textMesh.color = Color.white;
            return textMesh;
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
                throw new ObjectDisposedException(nameof(MapStationCargoLabelView));
            }
        }
    }
}
