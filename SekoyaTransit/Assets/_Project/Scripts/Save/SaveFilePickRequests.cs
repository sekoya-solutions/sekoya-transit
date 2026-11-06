#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace SekoyaTransit.Save
{
    /// <summary>
    /// Tracks the one open file pick of Load from file (Issue #320) and matches the browser's answers to it. Each answer
    /// is "request id|payload"; an answer for a replaced or finished request, or a malformed one, is dropped. Plain C#,
    /// so the matching is tested without a browser.
    /// </summary>
    public sealed class SaveFilePickRequests
    {
        private TaskCompletionSource<byte[]?>? _pending;
        private int _pendingRequestId;
        private int _nextRequestId = 1;

        /// <summary>
        /// Gets whether a pick waits for an answer.
        /// </summary>
        public bool HasPending => _pending != null;

        /// <summary>
        /// Starts a pick and returns its request id; a pick still open ends as cancelled (null).
        /// </summary>
        public int Begin(out Task<byte[]?> result)
        {
            _pending?.TrySetResult(null);
            var completion = new TaskCompletionSource<byte[]?>();
            _pending = completion;
            _pendingRequestId = _nextRequestId++;
            result = completion.Task;
            return _pendingRequestId;
        }

        /// <summary>
        /// Ends the pick <paramref name="requestId"/> with a failure (the picker could not be opened).
        /// </summary>
        public void Fail(int requestId, Exception exception)
        {
            if (_pending != null && requestId == _pendingRequestId)
            {
                TaskCompletionSource<byte[]?> completion = _pending;
                _pending = null;
                completion.TrySetException(exception);
            }
        }

        /// <summary>
        /// Browser answer: the chosen file, as "request id|Base64".
        /// </summary>
        public void OnPicked(string? message)
        {
            TaskCompletionSource<byte[]?>? completion = Take(message, out string payload);
            if (completion == null)
            {
                return;
            }

            byte[] data;
            try
            {
                data = Convert.FromBase64String(payload);
            }
            catch (FormatException exception)
            {
                completion.TrySetException(new IOException("The browser returned an unreadable file.", exception));
                return;
            }

            completion.TrySetResult(data);
        }

        /// <summary>
        /// Browser answer: the player closed the picker without a file, as "request id|".
        /// </summary>
        public void OnCancelled(string? message)
        {
            Take(message, out _)?.TrySetResult(null);
        }

        /// <summary>
        /// Browser answer: the picker or the file read failed, as "request id|reason".
        /// </summary>
        public void OnFailed(string? message)
        {
            TaskCompletionSource<byte[]?>? completion = Take(message, out string reason);
            completion?.TrySetException(new IOException(reason.Length == 0 ? "The file could not be opened." : reason));
        }

        private TaskCompletionSource<byte[]?>? Take(string? message, out string payload)
        {
            payload = string.Empty;
            if (message == null || _pending == null)
            {
                return null;
            }

            int separator = message.IndexOf('|');
            if (separator <= 0
                || !int.TryParse(
                    message.Substring(0, separator),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int requestId)
                || requestId != _pendingRequestId)
            {
                return null;
            }

            payload = message.Substring(separator + 1);
            TaskCompletionSource<byte[]?> completion = _pending;
            _pending = null;
            return completion;
        }
    }
}
