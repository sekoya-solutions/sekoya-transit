#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SekoyaTransit.Core.World;
using SekoyaTransit.UI.Shell;
using UnityEngine;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// World-space label above each station (Issue #276): "Road station #1" or "Rail station #1". Station ids start at
    /// 0 in the simulation; the label counts from 1. The text is as small as the vehicle cargo label.
    /// </summary>
    public sealed class MapStationLabelView : IDisposable
    {
        private const float LabelWorldZ = -1.3f;

        // The label's lower edge sits this far above the top of the station sprite (ADR 0032).
        private const float LabelGap = 0.03f;
        private const float FallbackLabelYOffset = 0.4f;
        private const float CharacterSize = 0.07f;
        private const float IconSize = 0.18f;

        private readonly GeneratedWorld _world;
        private readonly MapArtSet? _art;
        private readonly IMapLabelText? _text;
        private readonly GameObject _root;
        // Labels are indexed by list position (road stations, then rail stations); content is rebuilt only when the
        // station there or the loaded text changes, because TextMesh rebuilds its mesh on every assignment (Issue #70).
        private readonly List<MapIconLabel> _labels = new List<MapIconLabel>();
        private readonly List<Shown> _shown = new List<Shown>();
        private bool _disposed;

        public MapStationLabelView(GeneratedWorld world, Transform? parent, MapArtSet? art = null, IMapLabelText? text = null)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            _world = world;
            _art = art;
            _text = text;
            _root = new GameObject("MapStationLabels");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }
        }

        public void Sync()
        {
            ThrowIfDisposed();

            int textVersion = _text?.Version ?? 0;
            int roadCount = _world.RoadStations.Count;
            int total = roadCount + _world.RailStations.Count;
            for (int index = 0; index < total; index++)
            {
                bool isRail = index >= roadCount;
                int stationId;
                TileCoordinate coordinate;
                if (isRail)
                {
                    RailStation station = _world.RailStations[index - roadCount];
                    stationId = station.Id;
                    coordinate = station.Coordinate;
                }
                else
                {
                    RoadStation station = _world.RoadStations[index];
                    stationId = station.Id;
                    coordinate = station.Coordinate;
                }

                if (index == _labels.Count)
                {
                    _labels.Add(new MapIconLabel(_root.transform, $"StationLabel_{index}", CharacterSize, IconSize));
                    _shown.Add(Shown.None);
                }

                MapIconLabel label = _labels[index];
                Vector3 world = IsometricProjection.TileToWorld(coordinate.X, coordinate.Y);
                label.GameObject.transform.position = new Vector3(world.x, world.y + LabelHeight(isRail), LabelWorldZ);
                var shown = new Shown(isRail, stationId, textVersion);
                if (!shown.Equals(_shown[index]))
                {
                    _shown[index] = shown;
                    Rebuild(label, isRail, stationId);
                }
            }

            for (int index = _labels.Count - 1; index >= total; index--)
            {
                DestroyUnityObject(_labels[index].GameObject);
                _labels.RemoveAt(index);
                _shown.RemoveAt(index);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            for (int index = 0; index < _labels.Count; index++)
            {
                DestroyUnityObject(_labels[index].GameObject);
            }

            _labels.Clear();
            _shown.Clear();
            DestroyUnityObject(_root);
            _disposed = true;
        }

        private float LabelHeight(bool isRail)
        {
            Sprite? sprite = isRail ? _art?.RailStation : _art?.RoadStation;
            return sprite != null ? _art!.GetTopHeight(sprite) + LabelGap : FallbackLabelYOffset;
        }

        private void Rebuild(MapIconLabel label, bool isRail, int stationId)
        {
            RouteUiMode mode = isRail ? RouteUiMode.Rail : RouteUiMode.Road;
            string key = RoutesViewModel.StationNameKey(mode);
            string template = _text != null ? _text.Get(key) : key;
            label.Begin();
            label.AddText(RoutesViewModel.FormatStationName(stationId, template, CultureInfo.InvariantCulture));
            label.End();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MapStationLabelView));
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

        private readonly struct Shown : IEquatable<Shown>
        {
            public static readonly Shown None = new Shown(false, -1, -1);

            public Shown(bool isRail, int stationId, int textVersion)
            {
                IsRail = isRail;
                StationId = stationId;
                TextVersion = textVersion;
            }

            public bool IsRail { get; }

            public int StationId { get; }

            public int TextVersion { get; }

            public bool Equals(Shown other)
            {
                return IsRail == other.IsRail && StationId == other.StationId && TextVersion == other.TextVersion;
            }

            public override bool Equals(object? obj)
            {
                return obj is Shown other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (StationId * 397) ^ (TextVersion * 31) ^ (IsRail ? 1 : 0);
            }
        }
    }
}
