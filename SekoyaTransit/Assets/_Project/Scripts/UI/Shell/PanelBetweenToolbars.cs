#nullable enable
using System;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Keeps a side panel the same distance below the top HUD as above the bottom build toolbar (Issue #242). The
    /// toolbars change height with the screen size and font scale, so the insets are measured whenever the layout
    /// changes instead of being fixed in USS.
    /// </summary>
    public sealed class PanelBetweenToolbars : IDisposable
    {
        /// <summary>
        /// Gap in pixels between the panel and each toolbar.
        /// </summary>
        public const float Gap = 12f;

        private readonly VisualElement _root;
        private readonly VisualElement _topBar;
        private readonly VisualElement _bottomBar;
        private readonly VisualElement _panel;
        private readonly EventCallback<GeometryChangedEvent> _onGeometryChanged;

        /// <summary>
        /// Starts tracking the toolbars' layout and placing <paramref name="panel"/> between them.
        /// </summary>
        public PanelBetweenToolbars(
            VisualElement root,
            VisualElement topBar,
            VisualElement bottomBar,
            VisualElement panel)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _topBar = topBar ?? throw new ArgumentNullException(nameof(topBar));
            _bottomBar = bottomBar ?? throw new ArgumentNullException(nameof(bottomBar));
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
            _onGeometryChanged = _ => Apply();
            _root.RegisterCallback(_onGeometryChanged);
            _topBar.RegisterCallback(_onGeometryChanged);
            _bottomBar.RegisterCallback(_onGeometryChanged);
        }

        /// <summary>
        /// Returns the panel's top and bottom insets, in pixels from the root's edges, that leave <see cref="Gap"/>
        /// below the top bar and above the bottom bar.
        /// </summary>
        public static (float Top, float Bottom) ComputeInsets(float rootHeight, float topBarBottom, float bottomBarTop)
        {
            return (topBarBottom + Gap, rootHeight - bottomBarTop + Gap);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _root.UnregisterCallback(_onGeometryChanged);
            _topBar.UnregisterCallback(_onGeometryChanged);
            _bottomBar.UnregisterCallback(_onGeometryChanged);
        }

        private void Apply()
        {
            float rootHeight = _root.layout.height;
            float topBarBottom = _topBar.layout.yMax;
            float bottomBarTop = _bottomBar.layout.yMin;
            if (float.IsNaN(rootHeight) || float.IsNaN(topBarBottom) || float.IsNaN(bottomBarTop) || rootHeight <= 0f)
            {
                return;
            }

            (float top, float bottom) = ComputeInsets(rootHeight, topBarBottom, bottomBarTop);
            _panel.style.top = top;
            _panel.style.bottom = bottom;
        }
    }
}
