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
    /// Floating "+amount" popups with a coin over a station that was just paid for a delivery (Issue #284). A popup
    /// rises and fades out, then goes back to a pool. Popups are only created for stations in the camera view.
    /// </summary>
    public sealed class MapCashPopupView : IDisposable
    {
        /// <summary>Real seconds a popup lives.</summary>
        public const float LifetimeSeconds = 1.4f;

        /// <summary>Most popups alive at once; further payments are dropped (their sound still plays).</summary>
        public const int MaxPopups = 24;

        private const float RiseWorldUnits = 0.9f;
        private const float StartYOffset = 0.9f;
        private const float WorldZ = -1.35f;
        private const float CharacterSize = 0.08f;
        private const float IconSize = 0.2f;

        private readonly Transform _root;
        private readonly Sprite _coin;
        private readonly List<Popup> _active = new List<Popup>();
        private readonly Stack<MapIconLabel> _pool = new Stack<MapIconLabel>();
        private bool _disposed;

        public MapCashPopupView(Transform? parent)
        {
            var rootObject = new GameObject("MapCashPopups");
            if (parent != null)
            {
                rootObject.transform.SetParent(parent, false);
            }

            _root = rootObject.transform;
            _coin = MapCoinSprite.Create();
        }

        /// <summary>
        /// Gets the number of popups currently shown.
        /// </summary>
        public int ActiveCount
        {
            get
            {
                return _active.Count;
            }
        }

        /// <summary>
        /// Starts a popup of <paramref name="income"/> over the station at <paramref name="station"/>.
        /// </summary>
        public void Show(TileCoordinate station, long income)
        {
            if (_disposed || income <= 0 || _active.Count >= MaxPopups)
            {
                return;
            }

            MapIconLabel label = _pool.Count > 0
                ? _pool.Pop()
                : new MapIconLabel(_root, "CashPopup", CharacterSize, IconSize);
            label.Begin();
            label.AddIcon(_coin);
            label.AddText("+" + CashFormat.FormatAmount(income, CultureInfo.InvariantCulture));
            label.End();
            Vector3 world = IsometricProjection.TileToWorld(station.X, station.Y);
            label.GameObject.transform.position = new Vector3(world.x, world.y + StartYOffset, WorldZ);
            label.SetAlpha(1f);
            label.GameObject.SetActive(true);
            _active.Add(new Popup(label, world.y + StartYOffset, 0f));
        }

        /// <summary>
        /// Moves and fades the popups by <paramref name="deltaSeconds"/>; finished ones return to the pool.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            for (int index = _active.Count - 1; index >= 0; index--)
            {
                Popup popup = _active[index];
                float age = popup.Age + deltaSeconds;
                if (age >= LifetimeSeconds)
                {
                    popup.Label.GameObject.SetActive(false);
                    _pool.Push(popup.Label);
                    _active.RemoveAt(index);
                    continue;
                }

                float t = age / LifetimeSeconds;
                Transform transform = popup.Label.GameObject.transform;
                Vector3 position = transform.position;
                transform.position = new Vector3(position.x, popup.StartY + (RiseWorldUnits * t), position.z);
                popup.Label.SetAlpha(Alpha(t));
                _active[index] = new Popup(popup.Label, popup.StartY, age);
            }
        }

        /// <summary>
        /// The popup opacity at <paramref name="t"/> (0 at birth, 1 at the end of its life): opaque for the first half,
        /// then a linear fade to zero.
        /// </summary>
        public static float Alpha(float t)
        {
            return t <= 0.5f ? 1f : Mathf.Clamp01(1f - ((t - 0.5f) * 2f));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_root.gameObject);
            }

            _active.Clear();
            _pool.Clear();

            Texture2D texture = _coin.texture;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_coin);
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_coin);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private readonly struct Popup
        {
            public Popup(MapIconLabel label, float startY, float age)
            {
                Label = label;
                StartY = startY;
                Age = age;
            }

            public MapIconLabel Label { get; }

            public float StartY { get; }

            public float Age { get; }
        }
    }
}
