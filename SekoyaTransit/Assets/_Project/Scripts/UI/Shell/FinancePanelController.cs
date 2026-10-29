#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Finance recap panel (Issue #66): this year and last year by category with totals and the net result.
    /// </summary>
    public sealed class FinancePanelController : ShellPanelController
    {
        private readonly ScrollView _list;
        private readonly Func<FinanceRecap> _provider;
        private IReadOnlyList<FinanceRecapRow> _lastRows = Array.Empty<FinanceRecapRow>();

        /// <summary>
        /// Initializes the controller over the finance panel elements.
        /// </summary>
        public FinancePanelController(
            VisualElement panel,
            Label title,
            ScrollView list,
            Func<FinanceRecap> provider,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "finance-panel--hidden", title, GoalsUiKeys.FinanceTitle, text, culture)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _list.Clear();
            _lastRows = Array.Empty<FinanceRecapRow>();
        }

        /// <inheritdoc />
        protected override void RefreshContent(bool force)
        {
            IReadOnlyList<FinanceRecapRow> rows = FinanceRecapViewModel.BuildRows(_provider(), Text, Culture());
            if (!force && SameRows(rows))
            {
                return;
            }

            _lastRows = rows;
            _list.Clear();
            for (int index = 0; index < rows.Count; index++)
            {
                FinanceRecapRow row = rows[index];
                var element = new VisualElement();
                element.AddToClassList("finance-row");
                element.AddToClassList(RowClass(row.Kind));
                var label = new Label(row.Label);
                label.AddToClassList("finance-row-label");
                var value = new Label(row.Value);
                value.AddToClassList("finance-row-value");
                element.Add(label);
                element.Add(value);
                _list.Add(element);
            }
        }

        private bool SameRows(IReadOnlyList<FinanceRecapRow> rows)
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

        private static string RowClass(FinanceRecapRowKind kind)
        {
            switch (kind)
            {
                case FinanceRecapRowKind.Heading:
                    return "finance-row--heading";
                case FinanceRecapRowKind.TotalIncome:
                case FinanceRecapRowKind.TotalSpending:
                    return "finance-row--total";
                case FinanceRecapRowKind.NetResult:
                    return "finance-row--net";
                default:
                    return "finance-row--line";
            }
        }
    }
}
