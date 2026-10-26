#nullable enable
using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Shared behaviour of the finance, achievements, and contracts panels (Issue #66): a titled panel that is
    /// shown or hidden with a modifier class and repaints its rows only when their content changed.
    /// Controllers never touch simulation state; they only paint data supplied by a provider.
    /// </summary>
    public abstract class GoalsPanelController
    {
        private readonly string _hiddenClass;
        private readonly Label _title;
        private readonly string _titleKey;
        private int _lastTextVersion = int.MinValue;

        /// <summary>
        /// Initializes a controller for an existing UXML panel.
        /// </summary>
        protected GoalsPanelController(
            VisualElement panel,
            string hiddenClass,
            Label title,
            string titleKey,
            ShellTextCache text,
            Func<CultureInfo> culture)
        {
            Panel = panel ?? throw new ArgumentNullException(nameof(panel));
            _hiddenClass = hiddenClass ?? throw new ArgumentNullException(nameof(hiddenClass));
            _title = title ?? throw new ArgumentNullException(nameof(title));
            _titleKey = titleKey ?? throw new ArgumentNullException(nameof(titleKey));
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Culture = culture ?? throw new ArgumentNullException(nameof(culture));
            Panel.AddToClassList(_hiddenClass);
        }

        /// <summary>
        /// Gets the panel root (used for pointer tracking and mutual exclusion).
        /// </summary>
        public VisualElement Panel { get; }

        /// <summary>
        /// Gets whether the panel is open.
        /// </summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        /// Gets the resolved localized strings.
        /// </summary>
        protected ShellTextCache Text { get; }

        /// <summary>
        /// Gets the current culture provider.
        /// </summary>
        protected Func<CultureInfo> Culture { get; }

        /// <summary>
        /// Shows or hides the panel; opening repaints it at once.
        /// </summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (open)
            {
                Panel.RemoveFromClassList(_hiddenClass);
                Refresh(force: true);
            }
            else
            {
                Panel.AddToClassList(_hiddenClass);
            }
        }

        /// <summary>
        /// Repaints the open panel when its content changed. A closed panel is left alone.
        /// </summary>
        public void Refresh(bool force)
        {
            if (!IsOpen)
            {
                return;
            }

            bool textChanged = _lastTextVersion != Text.Version;
            _lastTextVersion = Text.Version;
            _title.text = Text.Get(_titleKey);
            RefreshContent(force || textChanged);
        }

        /// <summary>
        /// Releases rows and callbacks.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Rebuilds the rows when <paramref name="force"/> is set or the data changed.
        /// </summary>
        protected abstract void RefreshContent(bool force);
    }
}
