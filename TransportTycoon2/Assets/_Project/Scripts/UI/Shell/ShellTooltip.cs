#nullable enable
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Runtime tooltip for the shell (Issue #182). UI Toolkit only displays <see cref="VisualElement.tooltip"/> in the
    /// Editor, so this shows the tooltip of the hovered element (or its nearest ancestor that has one) in a label
    /// above or below it. Icon-only buttons keep their localized name there.
    /// </summary>
    public sealed class ShellTooltip : IDisposable
    {
        /// <summary>
        /// USS class of the tooltip label.
        /// </summary>
        public const string UssClass = "shell-tooltip";

        /// <summary>
        /// USS class that hides the tooltip label.
        /// </summary>
        public const string HiddenUssClass = "shell-tooltip--hidden";

        private const float Gap = 6f;
        private readonly VisualElement _root;
        private readonly Label _label;
        private VisualElement? _owner;

        /// <summary>
        /// Adds the tooltip label to <paramref name="root"/> and starts tracking the pointer.
        /// </summary>
        public ShellTooltip(VisualElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(UssClass);
            _label.AddToClassList(HiddenUssClass);
            _root.Add(_label);
            _root.RegisterCallback<PointerOverEvent>(OnPointerOver, TrickleDown.TrickleDown);
            _root.RegisterCallback<PointerOutEvent>(OnPointerOut, TrickleDown.TrickleDown);
            _label.RegisterCallback<GeometryChangedEvent>(OnLabelGeometryChanged);
        }

        /// <summary>
        /// Turns <paramref name="button"/> into an icon-only button: the visible text is cleared and
        /// <paramref name="name"/> becomes its tooltip (the icon itself comes from USS).
        /// </summary>
        public static void SetIconButtonName(Button button, string name)
        {
            if (button is null)
            {
                throw new ArgumentNullException(nameof(button));
            }

            button.text = string.Empty;
            button.tooltip = name ?? string.Empty;
        }

        /// <summary>
        /// Returns <paramref name="element"/> or its nearest ancestor with a non-empty tooltip, or null.
        /// </summary>
        public static VisualElement? FindTooltipOwner(VisualElement? element)
        {
            for (VisualElement? current = element; current != null; current = current.parent)
            {
                if (!string.IsNullOrEmpty(current.tooltip))
                {
                    return current;
                }
            }

            return null;
        }

        /// <summary>
        /// Removes the label and the pointer callbacks.
        /// </summary>
        public void Dispose()
        {
            _root.UnregisterCallback<PointerOverEvent>(OnPointerOver, TrickleDown.TrickleDown);
            _root.UnregisterCallback<PointerOutEvent>(OnPointerOut, TrickleDown.TrickleDown);
            _label.UnregisterCallback<GeometryChangedEvent>(OnLabelGeometryChanged);
            _label.RemoveFromHierarchy();
            _owner = null;
        }

        private void OnPointerOver(PointerOverEvent evt)
        {
            VisualElement? owner = FindTooltipOwner(evt.target as VisualElement);
            if (owner == null)
            {
                Hide();
                return;
            }

            _owner = owner;
            _label.text = owner.tooltip;
            _label.RemoveFromClassList(HiddenUssClass);
            _label.BringToFront();
            Place();
        }

        // A move onto another element of the same owner raises PointerOver right after, which shows it again.
        private void OnPointerOut(PointerOutEvent evt)
        {
            Hide();
        }

        private void OnLabelGeometryChanged(GeometryChangedEvent evt)
        {
            Place();
        }

        private void Hide()
        {
            _owner = null;
            _label.AddToClassList(HiddenUssClass);
        }

        // Centers the label on the owner, above it when the owner is in the lower half of the screen and below it
        // otherwise, clamped inside the root.
        private void Place()
        {
            if (_owner == null)
            {
                return;
            }

            Rect owner = _root.WorldToLocal(_owner.worldBound);
            Rect root = _root.contentRect;
            float width = _label.resolvedStyle.width;
            float height = _label.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height))
            {
                return;
            }

            float left = owner.center.x - (width / 2f);
            left = Mathf.Clamp(left, 0f, Mathf.Max(0f, root.width - width));
            float top = owner.center.y > root.height / 2f
                ? owner.yMin - height - Gap
                : owner.yMax + Gap;
            top = Mathf.Clamp(top, 0f, Mathf.Max(0f, root.height - height));
            _label.style.left = left;
            _label.style.top = top;
        }
    }
}
