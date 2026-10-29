#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using SekoyaTransit.Core.World;
using UnityEngine;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// World-space label above each town (Issue #193): "Paris ([wood] [iron ore] [wool]) | 10 [passengers] 5 [mail]".
    /// Square brackets stand for cargo icons; the icons are the town's current (unlocked) needs and the counts are the
    /// passengers and mail waiting at the town.
    /// </summary>
    public sealed class MapTownLabelView : IDisposable
    {
        public const string PassengersCargoId = "passengers";
        public const string MailCargoId = "mail";

        private const float LabelWorldZ = -1.3f;

        // Above the town house sprite (ADR 0032), like the industry labels.
        private const float LabelYOffset = 0.85f;
        private const float CharacterSize = 0.12f;
        private const float IconSize = 0.3f;

        private readonly GeneratedWorld _world;
        private readonly MapArtSet? _art;
        private readonly GameObject _root;
        private readonly List<MapIconLabel> _labels = new List<MapIconLabel>();
        // Last shown content key per town: rebuilt only when needs or waiting counts change (Issue #70).
        private readonly List<Shown> _shown = new List<Shown>();
        private bool _disposed;

        public MapTownLabelView(GeneratedWorld world, Transform? parent, MapArtSet? art = null)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            _world = world;
            _art = art;
            _root = new GameObject("MapTownLabels");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }
        }

        public void Sync()
        {
            ThrowIfDisposed();

            int townCount = _world.Towns.Count;
            for (int index = 0; index < townCount; index++)
            {
                Town town = _world.Towns[index];
                if (index == _labels.Count)
                {
                    _labels.Add(new MapIconLabel(_root.transform, $"TownLabel_{index}", CharacterSize, IconSize));
                    _shown.Add(Shown.None);
                }

                MapIconLabel label = _labels[index];
                Vector3 world = IsometricProjection.TileToWorld(town.Coordinate.X, town.Coordinate.Y);
                label.GameObject.transform.position = new Vector3(world.x, world.y + LabelYOffset, LabelWorldZ);
                var shown = new Shown(
                    town.Needs.Count,
                    town.WaitingCargo.GetAmount(PassengersCargoId),
                    town.WaitingCargo.GetAmount(MailCargoId));
                if (!shown.Equals(_shown[index]))
                {
                    _shown[index] = shown;
                    Rebuild(label, town, shown);
                }
            }

            for (int index = _labels.Count - 1; index >= townCount; index--)
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

        private void Rebuild(MapIconLabel label, Town town, Shown shown)
        {
            label.Begin();
            label.AddText(town.Name + " (");
            for (int index = 0; index < town.Needs.Count; index++)
            {
                label.AddIcon(_art?.GetCargoIcon(town.Needs[index].CargoId));
            }

            label.AddText(") | " + shown.Passengers.ToString(CultureInfo.InvariantCulture));
            label.AddIcon(_art?.GetCargoIcon(PassengersCargoId));
            label.AddText(shown.Mail.ToString(CultureInfo.InvariantCulture));
            label.AddIcon(_art?.GetCargoIcon(MailCargoId));
            label.End();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MapTownLabelView));
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
            public static readonly Shown None = new Shown(-1, -1, -1);

            public Shown(int needCount, int passengers, int mail)
            {
                NeedCount = needCount;
                Passengers = passengers;
                Mail = mail;
            }

            public int NeedCount { get; }

            public int Passengers { get; }

            public int Mail { get; }

            public bool Equals(Shown other)
            {
                return NeedCount == other.NeedCount && Passengers == other.Passengers && Mail == other.Mail;
            }

            public override bool Equals(object? obj)
            {
                return obj is Shown other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (NeedCount * 397) ^ (Passengers * 31) ^ Mail;
            }
        }
    }
}
