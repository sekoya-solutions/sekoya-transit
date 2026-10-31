#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// The elements of the routes window's dialog: a name field for create and rename, or a yes/no question for delete
    /// (Issue #229).
    /// </summary>
    public sealed class RoutesDialogElements
    {
        public RoutesDialogElements(VisualElement root, Label title, TextField field, Label message, Button ok, Button cancel)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Field = field ?? throw new ArgumentNullException(nameof(field));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Ok = ok ?? throw new ArgumentNullException(nameof(ok));
            Cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
        }

        public VisualElement Root { get; }

        public Label Title { get; }

        public TextField Field { get; }

        public Label Message { get; }

        public Button Ok { get; }

        public Button Cancel { get; }
    }

    /// <summary>
    /// The full-screen routes window (Issue #229): one row per route with its last month money, and buttons to rename,
    /// delete, and open the station and vehicle windows; a button creates a route after asking for its name.
    /// </summary>
    public sealed class RoutesWindowController : ShellWindowController
    {
        private const string DialogHiddenClass = "routes-dialog--hidden";
        private const string DialogConfirmClass = "routes-dialog--confirm";

        private readonly VisualElement _header;
        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Button _create;
        private readonly RoutesDialogElements _dialog;
        private readonly Func<RoutesBindings?> _bindings;
        private readonly Action<int> _openStations;
        private readonly Action<int> _openVehicles;
        private readonly ICargoIconSource? _icons;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private readonly HashSet<int> _highlighted = new HashSet<int>();
        private readonly Action _createHandler;
        private readonly Action _okHandler;
        private readonly Action _cancelHandler;
        private readonly EventCallback<KeyDownEvent> _fieldKeyHandler;
        private int _lastFingerprint = int.MinValue;
        private DialogKind _dialogKind;
        private int _dialogRouteId;

        public RoutesWindowController(
            VisualElement panel,
            Label title,
            Button close,
            VisualElement header,
            ScrollView list,
            Label empty,
            Button create,
            RoutesDialogElements dialog,
            Func<RoutesBindings?> bindings,
            Action<int> openStations,
            Action<int> openVehicles,
            Action onCloseRequested,
            ICargoIconSource? icons,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(panel, "routes-panel--hidden", title, RoutesUiKeys.Title, close, RoutesUiKeys.Close, onCloseRequested, text, culture)
        {
            _header = header ?? throw new ArgumentNullException(nameof(header));
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _openStations = openStations ?? throw new ArgumentNullException(nameof(openStations));
            _openVehicles = openVehicles ?? throw new ArgumentNullException(nameof(openVehicles));
            _icons = icons;
            _dialog.Field.maxLength = RoutesViewModel.MaxNameLength;
            _dialog.Root.AddToClassList(DialogHiddenClass);
            _createHandler = OpenCreateDialog;
            _okHandler = ConfirmDialog;
            _cancelHandler = CloseDialog;
            _fieldKeyHandler = OnFieldKeyDown;
            _create.clicked += _createHandler;
            _dialog.Ok.clicked += _okHandler;
            _dialog.Cancel.clicked += _cancelHandler;
            _dialog.Field.RegisterCallback(_fieldKeyHandler, TrickleDown.TrickleDown);
        }

        private enum DialogKind
        {
            None,
            Create,
            Rename,
            Delete
        }

        /// <summary>
        /// Sets the routes drawn highlighted (the routes that stop at the station the player came from).
        /// </summary>
        public void SetHighlightedRoutes(IReadOnlyCollection<int> routeIds)
        {
            _highlighted.Clear();
            foreach (int routeId in routeIds)
            {
                _highlighted.Add(routeId);
            }

            _lastFingerprint = int.MinValue;
        }

        /// <inheritdoc />
        protected override void DisposeContent()
        {
            _create.clicked -= _createHandler;
            _dialog.Ok.clicked -= _okHandler;
            _dialog.Cancel.clicked -= _cancelHandler;
            _dialog.Field.UnregisterCallback(_fieldKeyHandler, TrickleDown.TrickleDown);
            ClearRows();
        }

        /// <inheritdoc />
        protected override void RefreshWindowContent(bool force)
        {
            _create.text = Text.Get(RoutesUiKeys.Create);
            _empty.text = Text.Get(RoutesUiKeys.Empty);
            if (force)
            {
                RebuildHeader();
                RefreshDialogTexts();
            }

            IReadOnlyList<RouteListItem> routes = Routes();
            _empty.style.display = routes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            int fingerprint = RoutesViewModel.ComputeFingerprint(routes);
            if (!force && fingerprint == _lastFingerprint)
            {
                return;
            }

            _lastFingerprint = fingerprint;
            ClearRows();
            CultureInfo culture = Culture();
            for (int index = 0; index < routes.Count; index++)
            {
                AddRow(routes[index], culture);
            }
        }

        private IReadOnlyList<RouteListItem> Routes()
        {
            RoutesBindings? bindings = _bindings();
            return bindings != null ? bindings.Routes() : Array.Empty<RouteListItem>();
        }

        private void RebuildHeader()
        {
            _header.Clear();
            AddHeaderCell(RoutesUiKeys.ColumnName, "routes-cell--name");
            AddHeaderCell(RoutesUiKeys.ColumnRunningCost, "routes-cell--money");
            AddHeaderCell(RoutesUiKeys.ColumnRevenue, "routes-cell--money");
            AddHeaderCell(RoutesUiKeys.ColumnProfit, "routes-cell--money");
            AddHeaderCell(RoutesUiKeys.ColumnStations, "routes-cell--button");
            AddHeaderCell(RoutesUiKeys.ColumnVehicles, "routes-cell--button");
            AddHeaderCell(null, "routes-cell--delete");
        }

        private void AddHeaderCell(string? key, string cellClass)
        {
            var label = new Label(key == null ? string.Empty : Text.Get(key)) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("routes-cell");
            label.AddToClassList(cellClass);
            label.AddToClassList("routes-header-cell");
            _header.Add(label);
        }

        private void AddRow(RouteListItem item, CultureInfo culture)
        {
            int routeId = item.RouteId;
            var row = new VisualElement();
            row.AddToClassList("routes-row");
            row.EnableInClassList("routes-row--highlighted", _highlighted.Contains(routeId));
            row.EnableInClassList("routes-row--invalid", !item.IsValid);

            var nameCell = new VisualElement();
            nameCell.AddToClassList("routes-cell");
            nameCell.AddToClassList("routes-cell--name");
            var nameLine = new VisualElement();
            nameLine.AddToClassList("routes-name-line");
            var name = new Label(item.Name);
            name.AddToClassList("routes-name");
            nameLine.Add(name);
            var rename = new Button { text = Text.Get(RoutesUiKeys.Rename) };
            rename.AddToClassList("routes-button");
            Action renameHandler = () => OpenRenameDialog(routeId, item.Name);
            rename.clicked += renameHandler;
            nameLine.Add(rename);
            nameCell.Add(nameLine);
            if (!item.IsValid)
            {
                var warning = new Label(Text.Get(RoutesUiKeys.NeedsFix));
                warning.AddToClassList("routes-warning");
                nameCell.Add(warning);
            }

            row.Add(nameCell);
            row.Add(MoneyCell(RoutesViewModel.FormatRunningCost(item, Text, culture), null));
            row.Add(MoneyCell(RoutesViewModel.FormatRevenue(item, Text, culture), null));
            row.Add(MoneyCell(RoutesViewModel.FormatProfit(item, Text, culture), RoutesViewModel.ProfitClass(item)));

            Button stations = CellButton(
                RoutesViewModel.FormatEditButton(RoutesUiKeys.EditStations, item.StopCount, Text, culture));
            Action stationsHandler = () => _openStations(routeId);
            stations.clicked += stationsHandler;
            row.Add(Cell(stations, "routes-cell--button"));

            Button vehicles = CellButton(
                RoutesViewModel.FormatEditButton(RoutesUiKeys.EditVehicles, item.VehicleCount, Text, culture));
            Action vehiclesHandler = () => _openVehicles(routeId);
            vehicles.clicked += vehiclesHandler;
            row.Add(Cell(vehicles, "routes-cell--button"));

            Button delete = CellButton(Text.Get(RoutesUiKeys.Delete));
            Action deleteHandler = () => OpenDeleteDialog(routeId, item.Name);
            delete.clicked += deleteHandler;
            row.Add(Cell(delete, "routes-cell--delete"));

            _list.Add(row);
            _rows.Add(new RowBinding(
                new[] { rename, stations, vehicles, delete },
                new[] { renameHandler, stationsHandler, vehiclesHandler, deleteHandler }));
        }

        private VisualElement MoneyCell(TextLine line, string? modifier)
        {
            var view = new TextLineView(_icons);
            view.SetLine(line);
            view.AddToClassList("routes-money");
            if (modifier != null)
            {
                view.AddToClassList(modifier);
            }

            return Cell(view, "routes-cell--money");
        }

        private static Button CellButton(string text)
        {
            var button = new Button { text = text };
            button.AddToClassList("routes-button");
            return button;
        }

        private static VisualElement Cell(VisualElement content, string cellClass)
        {
            var cell = new VisualElement();
            cell.AddToClassList("routes-cell");
            cell.AddToClassList(cellClass);
            cell.Add(content);
            return cell;
        }

        private void ClearRows()
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                RowBinding binding = _rows[index];
                for (int button = 0; button < binding.Buttons.Length; button++)
                {
                    binding.Buttons[button].clicked -= binding.Handlers[button];
                }
            }

            _rows.Clear();
            _list.Clear();
        }

        private void OpenCreateDialog()
        {
            OpenDialog(DialogKind.Create, 0, RoutesViewModel.SuggestName(Routes(), Text, Culture()));
        }

        private void OpenRenameDialog(int routeId, string name)
        {
            OpenDialog(DialogKind.Rename, routeId, name);
        }

        private void OpenDeleteDialog(int routeId, string name)
        {
            OpenDialog(DialogKind.Delete, routeId, name);
        }

        private void OpenDialog(DialogKind kind, int routeId, string value)
        {
            _dialogKind = kind;
            _dialogRouteId = routeId;
            _dialog.Root.RemoveFromClassList(DialogHiddenClass);
            _dialog.Root.EnableInClassList(DialogConfirmClass, kind == DialogKind.Delete);
            _dialog.Field.SetValueWithoutNotify(kind == DialogKind.Delete ? string.Empty : value);
            _dialog.Message.text = kind == DialogKind.Delete
                ? string.Format(Culture(), Text.Get(RoutesUiKeys.DeleteConfirm), value)
                : string.Empty;
            RefreshDialogTexts();
            if (kind != DialogKind.Delete)
            {
                _dialog.Field.Focus();
                _dialog.Field.SelectAll();
            }
        }

        private void RefreshDialogTexts()
        {
            switch (_dialogKind)
            {
                case DialogKind.Create:
                    _dialog.Title.text = Text.Get(RoutesUiKeys.NameCreateTitle);
                    _dialog.Ok.text = Text.Get(RoutesUiKeys.NameOk);
                    break;
                case DialogKind.Rename:
                    _dialog.Title.text = Text.Get(RoutesUiKeys.NameRenameTitle);
                    _dialog.Ok.text = Text.Get(RoutesUiKeys.NameOk);
                    break;
                case DialogKind.Delete:
                    _dialog.Title.text = Text.Get(RoutesUiKeys.Delete);
                    _dialog.Ok.text = Text.Get(RoutesUiKeys.DeleteYes);
                    break;
            }

            _dialog.Cancel.text = Text.Get(_dialogKind == DialogKind.Delete ? RoutesUiKeys.ConfirmNo : RoutesUiKeys.NameCancel);
        }

        private void ConfirmDialog()
        {
            RoutesBindings? bindings = _bindings();
            if (bindings == null || _dialogKind == DialogKind.None)
            {
                CloseDialog();
                return;
            }

            if (_dialogKind == DialogKind.Delete)
            {
                bindings.DeleteRoute(_dialogRouteId);
                _highlighted.Remove(_dialogRouteId);
                CloseDialog();
                Refresh(force: true);
                return;
            }

            string name = _dialog.Field.value ?? string.Empty;
            RouteNameError error = _dialogKind == DialogKind.Create
                ? bindings.CreateRoute(name)
                : bindings.RenameRoute(_dialogRouteId, name);
            string? errorKey = RoutesViewModel.NameErrorKey(error);
            if (errorKey != null)
            {
                _dialog.Message.text = Text.Get(errorKey);
                return;
            }

            CloseDialog();
            Refresh(force: true);
        }

        /// <summary>
        /// Hides the name or delete dialog without applying it.
        /// </summary>
        public void CloseDialog()
        {
            _dialogKind = DialogKind.None;
            _dialog.Root.AddToClassList(DialogHiddenClass);
            _dialog.Field.Blur();
        }

        private void OnFieldKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == UnityEngine.KeyCode.Return || evt.keyCode == UnityEngine.KeyCode.KeypadEnter)
            {
                ConfirmDialog();
                evt.StopPropagation();
            }
            else if (evt.keyCode == UnityEngine.KeyCode.Escape)
            {
                CloseDialog();
                evt.StopPropagation();
            }
        }

        private readonly struct RowBinding
        {
            public RowBinding(Button[] buttons, Action[] handlers)
            {
                Buttons = buttons;
                Handlers = handlers;
            }

            public Button[] Buttons { get; }

            public Action[] Handlers { get; }
        }
    }
}
