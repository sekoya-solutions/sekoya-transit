#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using TransportTycoon.Core.Content;
using TransportTycoon.Core.World;
using UnityEngine;

namespace TransportTycoon.Presentation.Map
{
    /// <summary>
    /// World-space label above each industry (Issue #193): "Sawmill ([wood] => [planks]) | 10 [planks]", or
    /// "Forest | 10 [wood]" for an industry without inputs. Square brackets stand for cargo icons; the stock is the
    /// output stock only.
    /// </summary>
    public sealed class MapIndustryCargoLabelView : IDisposable
    {
        private const float LabelWorldZ = -1.3f;

        // Above the industry building sprite (ADR 0032), whose roof reaches about 0.8 world units over the tile center.
        private const float LabelYOffset = 0.85f;
        private const float CharacterSize = 0.12f;
        private const float IconSize = 0.3f;

        private readonly GeneratedWorld _world;
        private readonly ContentCatalog? _catalog;
        private readonly MapArtSet? _art;
        private readonly IMapLabelText? _text;
        private readonly GameObject _root;
        // Label and last shown stock and text version per industry index. Content is rebuilt only when they change,
        // because TextMesh rebuilds its mesh on every text assignment (Issue #70).
        private readonly List<MapIconLabel> _labels = new List<MapIconLabel>();
        private readonly List<int> _shownAmounts = new List<int>();
        private readonly List<int> _shownTextVersions = new List<int>();
        private bool _disposed;

        public MapIndustryCargoLabelView(
            GeneratedWorld world,
            Transform? parent,
            ContentCatalog? catalog = null,
            MapArtSet? art = null,
            IMapLabelText? text = null)
        {
#pragma warning disable CA1510
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }
#pragma warning restore CA1510

            _world = world;
            _catalog = catalog;
            _art = art;
            _text = text;
            _root = new GameObject("MapIndustryCargoLabels");
            if (parent != null)
            {
                _root.transform.SetParent(parent, false);
            }
        }

        public void Sync()
        {
            ThrowIfDisposed();

            int textVersion = _text?.Version ?? 0;
            int industryCount = _world.Industries.Count;
            for (int index = 0; index < industryCount; index++)
            {
                PlacedIndustry industry = _world.Industries[index];
                if (index == _labels.Count)
                {
                    _labels.Add(CreateLabel(index));
                    _shownAmounts.Add(-1);
                    _shownTextVersions.Add(-1);
                }

                MapIconLabel label = _labels[index];
                Vector3 world = IsometricProjection.TileToWorld(industry.Coordinate.X, industry.Coordinate.Y);
                label.GameObject.transform.position = new Vector3(world.x, world.y + LabelYOffset, LabelWorldZ);
                int amount = industry.Cargo.TotalAmount;
                if (_shownAmounts[index] != amount || _shownTextVersions[index] != textVersion)
                {
                    _shownAmounts[index] = amount;
                    _shownTextVersions[index] = textVersion;
                    Rebuild(label, industry, amount);
                }
            }

            // Industries are indexed by list position, so labels past the current count are stale.
            for (int index = _labels.Count - 1; index >= industryCount; index--)
            {
                DestroyUnityObject(_labels[index].GameObject);
                _labels.RemoveAt(index);
                _shownAmounts.RemoveAt(index);
                _shownTextVersions.RemoveAt(index);
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
            _shownAmounts.Clear();
            _shownTextVersions.Clear();
            DestroyUnityObject(_root);
            _disposed = true;
        }

        private void Rebuild(MapIconLabel label, PlacedIndustry industry, int amount)
        {
            string stock = amount.ToString(CultureInfo.InvariantCulture);
            label.Begin();
            IndustryDefinition? definition = null;
            if (_catalog != null && _catalog.TryGetIndustry(industry.IndustryId, out definition) && definition != null)
            {
                string name = _text != null ? _text.Get(definition.NameKey) : definition.NameKey;
                if (definition.Inputs.Count == 0)
                {
                    label.AddText(name + " |");
                }
                else
                {
                    label.AddText(name + " (");
                    for (int index = 0; index < definition.Inputs.Count; index++)
                    {
                        label.AddIcon(_art?.GetCargoIcon(definition.Inputs[index].CargoId));
                    }

                    label.AddText("=>");
                    AddOutputIcons(label, definition);
                    label.AddText(") |");
                }
            }

            label.AddText(stock);
            if (definition != null)
            {
                AddOutputIcons(label, definition);
            }

            label.End();
        }

        private void AddOutputIcons(MapIconLabel label, IndustryDefinition definition)
        {
            for (int index = 0; index < definition.Outputs.Count; index++)
            {
                label.AddIcon(_art?.GetCargoIcon(definition.Outputs[index].CargoId));
            }
        }

        private MapIconLabel CreateLabel(int industryIndex)
        {
            return new MapIconLabel(_root.transform, $"IndustryCargoLabel_{industryIndex}", CharacterSize, IconSize);
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
