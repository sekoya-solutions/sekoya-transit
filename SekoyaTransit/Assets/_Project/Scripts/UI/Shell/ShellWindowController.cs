#nullable enable
using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// A near full-screen shell window (Issue #223, the first of the panel reworks): a dimmed backdrop over the whole
    /// screen with a framed window, a title, and a close cross in the top-right corner. The UXML follows the
    /// <c>shell-window</c> classes in <c>MapPlayShell.uss</c>. The close cross asks the owner to close the window, so
    /// the owner can react (pause, resume) the same way as for its keyboard shortcut.
    /// </summary>
    public abstract class ShellWindowController : ShellPanelController
    {
        private readonly Button _close;
        private readonly string _closeKey;
        private readonly Action _closeHandler;

        /// <summary>
        /// Initializes a window over its UXML elements. <paramref name="onCloseRequested"/> runs when the close cross
        /// is clicked; <paramref name="closeKey"/> is the localization key of the cross tooltip.
        /// </summary>
        protected ShellWindowController(
            VisualElement panel,
            string hiddenClass,
            Label title,
            string titleKey,
            Button close,
            string closeKey,
            Action onCloseRequested,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, hiddenClass, title, titleKey, text, culture)
        {
            _close = close ?? throw new ArgumentNullException(nameof(close));
            _closeKey = closeKey ?? throw new ArgumentNullException(nameof(closeKey));
            if (onCloseRequested is null)
            {
                throw new ArgumentNullException(nameof(onCloseRequested));
            }

            _closeHandler = onCloseRequested;
            _close.clicked += _closeHandler;
        }

        /// <inheritdoc />
        public sealed override void Dispose()
        {
            _close.clicked -= _closeHandler;
            DisposeContent();
        }

        /// <inheritdoc />
        protected sealed override void RefreshContent(bool force)
        {
            if (force)
            {
                ShellTooltip.SetIconButtonName(_close, Text.Get(_closeKey));
            }

            RefreshWindowContent(force);
        }

        /// <summary>
        /// Releases the window body.
        /// </summary>
        protected abstract void DisposeContent();

        /// <summary>
        /// Rebuilds the window body when <paramref name="force"/> is set or the data changed.
        /// </summary>
        protected abstract void RefreshWindowContent(bool force);
    }
}
