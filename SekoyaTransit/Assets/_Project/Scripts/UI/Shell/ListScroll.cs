#nullable enable
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Scrolls a list panel so a row is its top row (Issue #219), used when a town or industry is picked on the map.
    /// </summary>
    public static class ListScroll
    {
        private const int RetryIntervalMilliseconds = 16;
        private const int MaximumAttempts = 60;

        /// <summary>
        /// Scrolls <paramref name="list"/> so <paramref name="row"/> is at the top, as far as the content allows. A panel
        /// opened this frame has no layout yet, so the scroll retries each tick until the row has been laid out.
        /// </summary>
        public static void ScrollRowToTop(ScrollView list, VisualElement row)
        {
            int attempts = 0;
            bool done = false;
            list.schedule.Execute(() => done = TryScroll(list, row))
                .Every(RetryIntervalMilliseconds)
                .Until(() => done || ++attempts > MaximumAttempts);
        }

        private static bool TryScroll(ScrollView list, VisualElement row)
        {
            if (row.parent == null || !(row.layout.height > 0f) || float.IsNaN(row.layout.y))
            {
                return false;
            }

            list.scrollOffset = new UnityEngine.Vector2(list.scrollOffset.x, row.layout.y);
            return true;
        }
    }
}
