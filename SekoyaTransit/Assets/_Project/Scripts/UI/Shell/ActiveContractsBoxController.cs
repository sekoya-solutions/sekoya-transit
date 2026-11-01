#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The "Active contracts" box at the top left of the map (Issue #266): one entry per running contract with its
    /// progress and time left, or the claim prompt once its goal is delivered. Clicking an entry centers the camera on
    /// the target town; clicking a completed one opens the contracts window. The box hides when it has no entry.
    /// </summary>
    public sealed class ActiveContractsBoxController : IDisposable
    {
        private const float RefreshIntervalSeconds = 0.25f;
        private const string HiddenClass = "active-contracts-box--hidden";

        private readonly VisualElement _box;
        private readonly Label _title;
        private readonly VisualElement _list;
        private readonly Func<IReadOnlyList<ActiveContractItem>> _provider;
        private readonly Action<int, int> _onRowSelected;
        private readonly Action _onCompletedSelected;
        private readonly ICargoIconSource? _cargoIcons;
        private readonly ShellTextCache _text;
        private readonly Func<CultureInfo> _culture;
        private readonly List<CardBinding> _bindings = new List<CardBinding>();
        private IReadOnlyList<ActiveContractRow> _lastRows = Array.Empty<ActiveContractRow>();
        private int _lastTextVersion = int.MinValue;
        private float _nextRefreshSeconds;

        /// <summary>
        /// Initializes the controller over the box elements. <paramref name="onRowSelected"/> receives the target tile
        /// column and row of a clicked running contract; <paramref name="onCompletedSelected"/> runs when a completed
        /// one is clicked.
        /// </summary>
        public ActiveContractsBoxController(
            VisualElement box,
            Label title,
            VisualElement list,
            Func<IReadOnlyList<ActiveContractItem>> provider,
            Action<int, int> onRowSelected,
            Action onCompletedSelected,
            ICargoIconSource? cargoIcons,
            ShellTextCache text,
            Func<CultureInfo> culture)
        {
            _box = box ?? throw new ArgumentNullException(nameof(box));
            _title = title ?? throw new ArgumentNullException(nameof(title));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _onRowSelected = onRowSelected ?? throw new ArgumentNullException(nameof(onRowSelected));
            _onCompletedSelected = onCompletedSelected ?? throw new ArgumentNullException(nameof(onCompletedSelected));
            _cargoIcons = cargoIcons;
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _culture = culture ?? throw new ArgumentNullException(nameof(culture));
            _box.AddToClassList(HiddenClass);
        }

        /// <summary>
        /// Gets the box root.
        /// </summary>
        public VisualElement Box => _box;

        /// <summary>
        /// Repaints the box when the localized texts changed or the refresh interval passed; the rows are rebuilt only
        /// when their content changed.
        /// </summary>
        public void Refresh(float nowSeconds, bool force)
        {
            bool textChanged = _lastTextVersion != _text.Version;
            if (!force && !textChanged && nowSeconds < _nextRefreshSeconds)
            {
                return;
            }

            _nextRefreshSeconds = nowSeconds + RefreshIntervalSeconds;
            _lastTextVersion = _text.Version;
            _title.text = _text.Get(GoalsUiKeys.ActiveContractsTitle);
            IReadOnlyList<ActiveContractRow> rows = ActiveContractsViewModel.BuildRows(_provider(), _text, _culture());
            _box.EnableInClassList(HiddenClass, rows.Count == 0);
            if (SameRows(rows))
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

        /// <inheritdoc />
        public void Dispose()
        {
            ClearRows();
            _lastRows = Array.Empty<ActiveContractRow>();
        }

        private void AddCard(ActiveContractRow row)
        {
            var card = new VisualElement { pickingMode = PickingMode.Position };
            card.AddToClassList("active-contracts-card");
            if (row.Completed)
            {
                card.AddToClassList("active-contracts-card--completed");
            }

            card.Add(CreateLine(row.Description, "active-contracts-description"));
            card.Add(CreateLine(row.Progress, "active-contracts-detail"));
            if (!row.TimeLeft.IsEmpty)
            {
                card.Add(CreateLine(row.TimeLeft, "active-contracts-detail"));
            }

            int tileX = row.TileX;
            int tileY = row.TileY;
            bool completed = row.Completed;
            EventCallback<ClickEvent> select = _ =>
            {
                if (completed)
                {
                    _onCompletedSelected();
                }
                else
                {
                    _onRowSelected(tileX, tileY);
                }
            };
            card.RegisterCallback(select);
            _list.Add(card);
            _bindings.Add(new CardBinding(card, select));
        }

        private TextLineView CreateLine(TextLine line, string className)
        {
            var view = new TextLineView(_cargoIcons, _text) { pickingMode = PickingMode.Ignore };
            view.SetLine(line);
            view.AddToClassList(className);
            return view;
        }

        private void ClearRows()
        {
            for (int index = 0; index < _bindings.Count; index++)
            {
                _bindings[index].Card.UnregisterCallback(_bindings[index].Select);
            }

            _bindings.Clear();
            _list.Clear();
        }

        private bool SameRows(IReadOnlyList<ActiveContractRow> rows)
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

        private readonly struct CardBinding
        {
            public CardBinding(VisualElement card, EventCallback<ClickEvent> select)
            {
                Card = card;
                Select = select;
            }

            public VisualElement Card { get; }

            public EventCallback<ClickEvent> Select { get; }
        }
    }
}
