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
                var label = new Label(_queue[index].Format(_text, _culture()));
                label.AddToClassList("toast");
                label.pickingMode = PickingMode.Ignore;
                _stack.Add(label);
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
