#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Finance full-screen window (Issues #66 and #296): a table of vehicle costs, benefits and profits, purchases,
    /// sales, unlocks and contract profit for last month, current month, last year and current year.
    /// </summary>
    public sealed class FinancePanelController : ShellWindowController
    {
        private readonly VisualElement _header;
        private readonly ScrollView _list;
        private readonly Func<FinanceTable> _provider;
        private IReadOnlyList<FinanceTableRow> _lastRows = Array.Empty<FinanceTableRow>();

        /// <summary>
        /// Initializes the controller over the finance window elements.
        /// </summary>
        public FinancePanelController(
            VisualElement panel,
            Label title,
            Button close,
            VisualElement header,
            ScrollView list,
            Func<FinanceTable> provider,
            Action onCloseRequested,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(
                panel,
                "finance-panel--hidden",
                title,
                GoalsUiKeys.FinanceTitle,
                close,
                RoutesUiKeys.Close,
                onCloseRequested,
                text,
                culture)
        {
            _header = header ?? throw new ArgumentNullException(nameof(header));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc />
        protected override void DisposeContent()
        {
            _header.Clear();
            _list.Clear();
            _lastRows = Array.Empty<FinanceTableRow>();
        }

        /// <inheritdoc />
        protected override void RefreshWindowContent(bool force)
        {
            IReadOnlyList<FinanceTableRow> rows = FinanceTableViewModel.BuildRows(_provider(), Text, Culture());
            if (!force && SameRows(rows))
            {
                return;
            }

            _lastRows = rows;
            BuildHeader();
            _list.Clear();
            for (int index = 0; index < rows.Count; index++)
            {
                _list.Add(BuildRow(rows[index]));
            }
        }

        private void BuildHeader()
        {
            _header.Clear();
            _header.AddToClassList("finance-row");
            _header.Add(new Label(string.Empty));
            _header[0].AddToClassList("finance-row-label");
            string[] headers = FinanceTableViewModel.BuildHeaders(Text);
            for (int index = 0; index < headers.Length; index++)
            {
                var heading = new Label(headers[index]);
                heading.AddToClassList("finance-cell");
                heading.AddToClassList("finance-cell--heading");
                if (index == 2)
                {
                    heading.AddToClassList("finance-cell--group-start");
                }

                _header.Add(heading);
            }
        }

        private static VisualElement BuildRow(FinanceTableRow row)
        {
            var element = new VisualElement();
            element.AddToClassList("finance-row");
            if (row.StartsSection)
            {
                element.AddToClassList("finance-row--section");
            }

            if (row.IsTotal)
            {
                element.AddToClassList("finance-row--total");
            }

            var label = new Label(row.Label);
            label.AddToClassList("finance-row-label");
            element.Add(label);
            for (int index = 0; index < row.Cells.Count; index++)
            {
                FinanceCell cell = row.Cells[index];
                var container = new VisualElement();
                container.AddToClassList("finance-cell");
                if (index == 2)
                {
                    container.AddToClassList("finance-cell--group-start");
                }

                var value = new Label(cell.Text);
                value.AddToClassList("finance-cell-value");
                value.AddToClassList(ToneClass(cell.Tone));
                container.Add(value);
                if (cell.ShowsCashIcon)
                {
                    var icon = new VisualElement();
                    icon.AddToClassList("finance-cell-icon");
                    icon.pickingMode = PickingMode.Ignore;
                    container.Add(icon);
                }

                element.Add(container);
            }

            return element;
        }

        private bool SameRows(IReadOnlyList<FinanceTableRow> rows)
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

        private static string ToneClass(FinanceCellTone tone)
        {
            switch (tone)
            {
                case FinanceCellTone.Positive:
                    return "finance-cell-value--positive";
                case FinanceCellTone.Negative:
                    return "finance-cell-value--negative";
                case FinanceCellTone.Unavailable:
                    return "finance-cell-value--unavailable";
                default:
                    return "finance-cell-value--neutral";
            }
        }
    }
}
