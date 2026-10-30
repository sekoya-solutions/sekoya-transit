#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Contracts panel (Issues #66 and #227): one card per contract, colored by status, with the buttons its status
    /// offers; clicking the sentence selects the target town and centers the camera on it.
    /// </summary>
    public sealed class ContractsPanelController : ShellPanelController
    {
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Label _footer;
        private readonly Func<ContractsPanelData> _provider;
        private readonly Action<int, int> _onRowSelected;
        private readonly Action<ContractUiAction, int> _onAction;
        private readonly ICargoIconSource? _cargoIcons;
        private readonly List<CardBinding> _bindings = new List<CardBinding>();
        private IReadOnlyList<ContractRow> _lastRows = Array.Empty<ContractRow>();
        private ContractsPanelData _data = ContractsPanelData.Empty;

        /// <summary>
        /// Initializes the controller over the contracts panel elements. <paramref name="onRowSelected"/> receives the
        /// target tile column and row of the clicked contract; <paramref name="onAction"/> the button pressed and the
        /// contract id.
        /// </summary>
        public ContractsPanelController(
            VisualElement panel,
            Label title,
            ScrollView list,
            Label empty,
            Label footer,
            Func<ContractsPanelData> provider,
            Action<int, int> onRowSelected,
            Action<ContractUiAction, int> onAction,
            ICargoIconSource? cargoIcons,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "contracts-panel--hidden", title, GoalsUiKeys.ContractsTitle, text, culture)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _footer = footer ?? throw new ArgumentNullException(nameof(footer));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _onRowSelected = onRowSelected ?? throw new ArgumentNullException(nameof(onRowSelected));
            _onAction = onAction ?? throw new ArgumentNullException(nameof(onAction));
            _cargoIcons = cargoIcons;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            ClearRows();
            _lastRows = Array.Empty<ContractRow>();
            _data = ContractsPanelData.Empty;
        }

        /// <inheritdoc />
        protected override string ResolveTitle(string title)
        {
            // The title is resolved first on every refresh, so this is also where the panel data is read once.
            _data = _provider();
            return ContractsViewModel.FormatTitle(_data, Text, Culture());
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            IReadOnlyList<ContractRow> rows = ContractsViewModel.BuildRows(_data.Items, Text, Culture());
            _empty.text = Text.Get(GoalsUiKeys.ContractsEmpty);
            _empty.style.display = rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _footer.text = ContractsViewModel.FormatFooter(_data, Text);
            if (!force && SameRows(rows))
            {
                return;
            }

            _lastRows = rows;
            ClearRows();
            for (int index = 0; index < rows.Count; index++)
            {
                AddCard(rows[index]);
            }
        }

        private void AddCard(ContractRow row)
        {
            var card = new VisualElement();
            card.AddToClassList("contracts-card");
            card.AddToClassList("contracts-card--" + StatusClass(row.Status));
            if (row.Locked)
            {
                card.AddToClassList("contracts-card--locked");
            }

            var description = new TextLineView(_cargoIcons) { pickingMode = PickingMode.Position };
            description.SetLine(row.Description);
            description.AddToClassList("contracts-description");
            int tileX = row.TileX;
            int tileY = row.TileY;
            EventCallback<ClickEvent> select = _ => _onRowSelected(tileX, tileY);
            description.RegisterCallback(select);
            card.Add(description);

            var detail = new Label(row.Detail) { pickingMode = PickingMode.Ignore };
            detail.AddToClassList("contracts-detail");
            card.Add(detail);

            var buttons = new VisualElement();
            buttons.AddToClassList("contracts-buttons");
            var actions = new List<ButtonBinding>(row.Buttons.Count);
            for (int index = 0; index < row.Buttons.Count; index++)
            {
                ContractRowButton definition = row.Buttons[index];
                ContractUiAction action = definition.Action;
                int contractId = row.ContractId;
                var button = new Button { text = definition.Label };
                button.AddToClassList("contracts-button");
                button.SetEnabled(definition.Enabled);
                Action handler = () =>
                {
                    _onAction(action, contractId);
                    Refresh(force: true);
                };
                button.clicked += handler;
                buttons.Add(button);
                actions.Add(new ButtonBinding(button, handler));
            }

            card.Add(buttons);
            _list.Add(card);
            _bindings.Add(new CardBinding(description, select, actions));
        }

        private static string StatusClass(ContractUiStatus status)
        {
            switch (status)
            {
                case ContractUiStatus.Proposal:
                    return "proposal";
                case ContractUiStatus.Accepted:
                    return "accepted";
                case ContractUiStatus.Completed:
                    return "completed";
                default:
                    return "failed";
            }
        }

        private void ClearRows()
        {
            for (int index = 0; index < _bindings.Count; index++)
            {
                CardBinding binding = _bindings[index];
                binding.Description.UnregisterCallback(binding.Select);
                for (int button = 0; button < binding.Buttons.Count; button++)
                {
                    binding.Buttons[button].Button.clicked -= binding.Buttons[button].Handler;
                }
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

        private readonly struct ButtonBinding
        {
            public ButtonBinding(Button button, Action handler)
            {
                Button = button;
                Handler = handler;
            }

            public Button Button { get; }

            public Action Handler { get; }
        }

        private readonly struct CardBinding
        {
            public CardBinding(TextLineView description, EventCallback<ClickEvent> select, List<ButtonBinding> buttons)
            {
                Description = description;
                Select = select;
                Buttons = buttons;
            }

            public TextLineView Description { get; }

            public EventCallback<ClickEvent> Select { get; }

            public List<ButtonBinding> Buttons { get; }
        }
    }
}
