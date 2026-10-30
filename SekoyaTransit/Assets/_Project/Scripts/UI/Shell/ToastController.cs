#nullable enable
using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Paints the toast stack (Issue #66). Toasts never take pointer input, never pause the game, and hide after a
    /// placeholder number of real-time seconds.
    /// </summary>
    public sealed class ToastController
    {
        private readonly VisualElement _stack;
        private readonly ToastQueue _queue;
        private readonly ShellTextCache _text;
        private readonly Func<CultureInfo> _culture;
        private int _lastQueueVersion = int.MinValue;
        private int _lastTextVersion = int.MinValue;

        /// <summary>
        /// Initializes the controller over the toast stack element.
        /// </summary>
        public ToastController(
            VisualElement stack,
            ToastQueue queue,
            ShellTextCache text,
            Func<CultureInfo> culture)
        {
            _stack = stack ?? throw new ArgumentNullException(nameof(stack));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _culture = culture ?? throw new ArgumentNullException(nameof(culture));
            _stack.pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// Shows a toast at <paramref name="nowSeconds"/> (real time).
        /// </summary>
        public void Show(ToastMessage message, float nowSeconds)
        {
            _queue.Enqueue(message, nowSeconds);
        }

        /// <summary>
        /// Hides expired toasts and repaints the stack when it changed.
        /// </summary>
        public void Refresh(float nowSeconds)
        {
            _queue.Expire(nowSeconds);
            if (_lastQueueVersion == _queue.Version && _lastTextVersion == _text.Version)
            {
                return;
            }

            _lastQueueVersion = _queue.Version;
            _lastTextVersion = _text.Version;
            _stack.Clear();
            for (int index = 0; index < _queue.Count; index++)
            {
                ToastMessage message = _queue[index];
                var toast = new VisualElement { pickingMode = PickingMode.Ignore };
                toast.AddToClassList("toast");
                string? iconClass = IconClass(message.Icon);
                if (iconClass != null)
                {
                    var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                    icon.AddToClassList("toast__icon");
                    icon.AddToClassList(iconClass);
                    toast.Add(icon);
                }

                var label = new Label(message.Format(_text, _culture())) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("toast__text");
                toast.Add(label);
                _stack.Add(toast);
            }
        }

        /// <summary>
        /// Returns the USS modifier class that draws <paramref name="icon"/>, or <see langword="null"/> for no icon.
        /// </summary>
        public static string? IconClass(ToastIcon icon)
        {
            switch (icon)
            {
                case ToastIcon.TrophyBronze:
                    return "toast__icon--trophy-bronze";
                case ToastIcon.TrophySilver:
                    return "toast__icon--trophy-silver";
                case ToastIcon.TrophyGold:
                    return "toast__icon--trophy-gold";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Clears the painted toasts.
        /// </summary>
        public void Dispose()
        {
            _stack.Clear();
        }
    }
}
