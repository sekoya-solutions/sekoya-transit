#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Contracts panel (Issue #66): the open contracts; clicking a row selects its target and centers the camera.
    /// </summary>
    public sealed class ContractsPanelController : ShellPanelController
    {
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<ContractListItem>> _provider;
        private readonly Action<int, int> _onRowSelected;
        private readonly List<RowBinding> _bindings = new List<RowBinding>();
        private IReadOnlyList<ContractRow> _lastRows = Array.Empty<ContractRow>();

        /// <summary>
        /// Initializes the controller over the contracts panel elements. <paramref name="onRowSelected"/> receives the
        /// target tile column and row of the clicked contract.
        /// </summary>
        public ContractsPanelController(
            VisualElement panel,
            Label title,
            ScrollView list,
            Label empty,
            Func<IReadOnlyList<ContractListItem>> provider,
            Action<int, int> onRowSelected,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "contracts-panel--hidden", title, GoalsUiKeys.ContractsTitle, text, culture)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _onRowSelected = onRowSelected ?? throw new ArgumentNullException(nameof(onRowSelected));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            ClearRows();
            _lastRows = Array.Empty<ContractRow>();
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            IReadOnlyList<ContractRow> rows = ContractsViewModel.BuildRows(_provider(), Text, Culture());
            _empty.text = Text.Get(GoalsUiKeys.ContractsEmpty);
            _empty.style.display = rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (!force && SameRows(rows))
            {
                return;
            }

            _lastRows = rows;
            ClearRows();
            for (int index = 0; index < rows.Count; index++)
            {
                ContractRow row = rows[index];
                var label = new Label(row.Text);
                label.AddToClassList("contracts-row");
                int tileX = row.TileX;
                int tileY = row.TileY;
                EventCallback<ClickEvent> handler = _ => _onRowSelected(tileX, tileY);
                label.RegisterCallback(handler);
                _bindings.Add(new RowBinding(label, handler));
                _list.Add(label);
            }
        }

        private void ClearRows()
        {
            for (int index = 0; index < _bindings.Count; index++)
            {
                _bindings[index].Label.UnregisterCallback(_bindings[index].Handler);
            }

            _bindings.Clear();
            _list.Clear();
        }

        private bool SameRows(IReadOnlyList<ContractRow> rows)
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

        private readonly struct RowBinding
        {
            public RowBinding(Label label, EventCallback<ClickEvent> handler)
            {
                Label = label;
                Handler = handler;
            }

            public Label Label { get; }

            public EventCallback<ClickEvent> Handler { get; }
        }
    }
}
