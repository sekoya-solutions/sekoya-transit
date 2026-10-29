#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SekoyaTransit.Presentation.Map
{
    /// <summary>
    /// A world-space map label made of text runs and cargo icons on one line, centered on its root and resting on it
    /// (Issue #193). Parts are pooled: rebuild the content only when it changes (<see cref="Begin"/>, the Add calls,
    /// then <see cref="End"/>), because each text assignment rebuilds a TextMesh mesh (Issue #70).
    /// </summary>
    public sealed class MapIconLabel
    {
        private readonly GameObject _root;
        private readonly float _characterSize;
        private readonly float _iconSize;
        private readonly float _gap;
        private readonly List<TextMesh> _texts = new List<TextMesh>();
        private readonly List<SpriteRenderer> _icons = new List<SpriteRenderer>();
        private readonly List<Part> _parts = new List<Part>();
        private readonly List<float> _widths = new List<float>();
        private int _usedTexts;
        private int _usedIcons;

        /// <param name="parent">The transform the label follows.</param>
        /// <param name="name">The root game object name.</param>
        /// <param name="characterSize">TextMesh character size of the text runs.</param>
        /// <param name="iconSize">Icon height in the label's local units.</param>
        public MapIconLabel(Transform parent, string name, float characterSize, float iconSize)
        {
#pragma warning disable CA1510
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }
#pragma warning restore CA1510

            _root = new GameObject(name);
            _root.transform.SetParent(parent, false);
            _characterSize = characterSize;
            _iconSize = iconSize;
            _gap = iconSize * 0.2f;
        }

        /// <summary>
        /// Gets the label root; position, scale and activate it like any game object.
        /// </summary>
        public GameObject GameObject
        {
            get
            {
                return _root;
            }
        }

        /// <summary>
        /// Starts new content; parts added before <see cref="End"/> replace the old ones.
        /// </summary>
        public void Begin()
        {
            _usedTexts = 0;
            _usedIcons = 0;
            _parts.Clear();
        }

        /// <summary>
        /// Appends a text run. Leading and trailing spaces take no room, so parts are separated by a fixed gap.
        /// </summary>
        public void AddText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (_usedTexts == _texts.Count)
            {
                _texts.Add(CreateText());
            }

            TextMesh mesh = _texts[_usedTexts++];
            if (!string.Equals(mesh.text, text, StringComparison.Ordinal))
            {
                mesh.text = text;
            }

            _parts.Add(new Part(mesh, null));
        }

        /// <summary>
        /// Appends an icon; a missing sprite (art not baked) adds nothing.
        /// </summary>
        public void AddIcon(Sprite? sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (_usedIcons == _icons.Count)
            {
                _icons.Add(CreateIcon());
            }

            SpriteRenderer icon = _icons[_usedIcons++];
            icon.sprite = sprite;
            float height = sprite.bounds.size.y;
            float scale = height > 0f ? _iconSize / height : 1f;
            icon.transform.localScale = new Vector3(scale, scale, 1f);
            _parts.Add(new Part(null, icon));
        }

        /// <summary>
        /// Hides unused pooled parts and lays the parts out left to right, centered on the root.
        /// </summary>
        public void End()
        {
            for (int index = 0; index < _texts.Count; index++)
            {
                _texts[index].gameObject.SetActive(index < _usedTexts);
            }

            for (int index = 0; index < _icons.Count; index++)
            {
                _icons[index].gameObject.SetActive(index < _usedIcons);
            }

            // Renderer bounds are empty while inactive, so measure with the root shown (the caller may hide it).
            bool wasActive = _root.activeSelf;
            _root.SetActive(true);
            float rootScale = Mathf.Abs(_root.transform.lossyScale.x);
            float toLocal = rootScale > 0f ? 1f / rootScale : 1f;
            float iconCenterY = _iconSize * 0.5f;
            _widths.Clear();
            float total = 0f;
            for (int index = 0; index < _parts.Count; index++)
            {
                Part part = _parts[index];
                if (part.Text != null)
                {
                    Bounds bounds = part.Text.GetComponent<MeshRenderer>().bounds;
                    _widths.Add(bounds.size.x * toLocal);
                    iconCenterY = Mathf.Max(iconCenterY, bounds.size.y * toLocal * 0.5f);
                }
                else
                {
                    _widths.Add(part.Icon!.sprite.bounds.size.x * part.Icon.transform.localScale.x);
                }

                total += _widths[index] + (index > 0 ? _gap : 0f);
            }

            float x = -total * 0.5f;
            for (int index = 0; index < _parts.Count; index++)
            {
                Part part = _parts[index];
                if (part.Text != null)
                {
                    part.Text.transform.localPosition = new Vector3(x, 0f, 0f);
                }
                else
                {
                    part.Icon!.transform.localPosition = new Vector3(x + (_widths[index] * 0.5f), iconCenterY, 0f);
                }

                x += _widths[index] + _gap;
            }

            _root.SetActive(wasActive);
        }

        private TextMesh CreateText()
        {
            var go = new GameObject("Text");
            go.transform.SetParent(_root.transform, false);
            var text = go.AddComponent<TextMesh>();
            text.anchor = TextAnchor.LowerLeft;
            text.alignment = TextAlignment.Left;
            text.characterSize = _characterSize;
            text.fontSize = 32;
            text.color = Color.white;
            go.GetComponent<MeshRenderer>().sortingOrder = MapSortingOrder.Labels;
            return text;
        }

        private SpriteRenderer CreateIcon()
        {
            var go = new GameObject("Icon");
            go.transform.SetParent(_root.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = MapSortingOrder.Labels;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private readonly struct Part
        {
            public Part(TextMesh? text, SpriteRenderer? icon)
            {
                Text = text;
                Icon = icon;
            }

            public TextMesh? Text { get; }

            public SpriteRenderer? Icon { get; }
        }
    }
}
