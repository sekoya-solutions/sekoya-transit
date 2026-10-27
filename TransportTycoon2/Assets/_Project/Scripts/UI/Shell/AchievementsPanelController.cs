#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Achievements panel (Issue #66): every achievement with its progress and completion date.
    /// </summary>
    public sealed class AchievementsPanelController : ShellPanelController
    {
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<AchievementListItem>> _provider;
        private IReadOnlyList<AchievementRow> _lastRows = Array.Empty<AchievementRow>();

        /// <summary>
        /// Initializes the controller over the achievements panel elements.
        /// </summary>
        public AchievementsPanelController(
            VisualElement panel,
            Label title,
            ScrollView list,
            Label empty,
            Func<IReadOnlyList<AchievementListItem>> provider,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "achievements-panel--hidden", title, GoalsUiKeys.AchievementsTitle, text, culture)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _list.Clear();
            _lastRows = Array.Empty<AchievementRow>();
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            IReadOnlyList<AchievementRow> rows = AchievementsViewModel.BuildRows(_provider(), Text, Culture());
            _empty.text = Text.Get(GoalsUiKeys.AchievementsEmpty);
            _empty.style.display = rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (!force && SameRows(rows))
            {
                return;
            }

            _lastRows = rows;
            _list.Clear();
            for (int index = 0; index < rows.Count; index++)
            {
                var label = new Label(rows[index].Text);
                label.AddToClassList("achievements-row");
                if (rows[index].IsCompleted)
                {
                    label.AddToClassList("achievements-row--completed");
                }

                _list.Add(label);
            }
        }

        private bool SameRows(IReadOnlyList<AchievementRow> rows)
        {
            if (rows.Count != _lastRows.Count)
            {
                return false;
            }

            for (int index = 0; index < rows.Count; index++)
            {
                if (!rows[index].Equals(_lastRows[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
