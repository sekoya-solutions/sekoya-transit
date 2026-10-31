#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Owns the routes window and its station and vehicle windows (Issue #229). Opening a side window hides the
    /// full-screen window so the map is visible; closing it brings the routes window back. Only the full-screen window
    /// reports <see cref="WindowOpenChanged"/>, which pauses the game.
    /// </summary>
    public sealed class RoutesShellPanels
    {
        private readonly ShellTextCache _text = new ShellTextCache();
        private readonly HashSet<string> _requestedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Action _onBeforeOpen;
        private readonly Button _toggle;
        private readonly RoutesWindowController _window;
        private readonly RouteStationsPanelController _stations;
        private readonly RouteVehiclesPanelController _vehicles;
        private readonly Action _toggleHandler;
        private RoutesBindings? _bindings;
        private int _lastToggleTextVersion = int.MinValue;
        private bool _windowWasOpen;

        public RoutesShellPanels(
            VisualElement root,
            Func<CultureInfo> culture,
            Action onBeforeOpen,
            ICargoIconSource? icons)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (culture is null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            _onBeforeOpen = onBeforeOpen ?? throw new ArgumentNullException(nameof(onBeforeOpen));
            _toggle = Require<Button>(root, "routes-toggle");
            _window = new RoutesWindowController(
                Require<VisualElement>(root, "routes-panel"),
                Require<Label>(root, "routes-title"),
                Require<Button>(root, "routes-close"),
                Require<VisualElement>(root, "routes-header"),
                Require<ScrollView>(root, "routes-list"),
                Require<Label>(root, "routes-empty"),
                Require<Button>(root, "routes-create"),
                new RoutesDialogElements(
                    Require<VisualElement>(root, "routes-dialog"),
                    Require<Label>(root, "routes-dialog-title"),
                    Require<TextField>(root, "routes-dialog-field"),
                    Require<Label>(root, "routes-dialog-message"),
                    Require<Button>(root, "routes-dialog-ok"),
                    Require<Button>(root, "routes-dialog-cancel")),
                () => _bindings,
                OpenStations,
                OpenVehicles,
                () => SetRoutesOpen(false),
                icons,
                _text,
                culture);
            _stations = new RouteStationsPanelController(
                Require<VisualElement>(root, "route-stations-panel"),
                Require<Label>(root, "route-stations-title"),
                Require<Button>(root, "route-stations-close"),
                Require<Label>(root, "route-stations-header"),
                Require<ScrollView>(root, "route-stations-list"),
                Require<Label>(root, "route-stations-empty"),
                Require<Label>(root, "route-stations-hint"),
                Require<Label>(root, "route-stations-warning"),
                Require<VisualElement>(root, "route-stations-confirm"),
                Require<Label>(root, "route-stations-confirm-text"),
                Require<Button>(root, "route-stations-confirm-yes"),
                Require<Button>(root, "route-stations-confirm-no"),
                () => _bindings,
                BackToWindow,
                BackToWindow,
                _text,
                culture);
            _vehicles = new RouteVehiclesPanelController(
                Require<VisualElement>(root, "route-vehicles-panel"),
                Require<Label>(root, "route-vehicles-title"),
                Require<Button>(root, "route-vehicles-close"),
                Require<Label>(root, "route-vehicles-header"),
                Require<ScrollView>(root, "route-vehicles-list"),
                Require<Label>(root, "route-vehicles-empty"),
                Require<Label>(root, "route-vehicles-invalid"),
                Require<Button>(root, "route-vehicles-add-toggle"),
                Require<VisualElement>(root, "route-vehicles-add-section"),
                Require<Label>(root, "route-vehicles-add-header"),
                Require<ScrollView>(root, "route-vehicles-add-list"),
                Require<Label>(root, "route-vehicles-none-available"),
                () => _bindings,
                BackToWindow,
                BackToWindow,
                _text,
                culture);
            _window.Panel.pickingMode = PickingMode.Position;
            _stations.Panel.pickingMode = PickingMode.Position;
            _vehicles.Panel.pickingMode = PickingMode.Position;
            _toggleHandler = ToggleRoutes;
            _toggle.clicked += _toggleHandler;
        }

        /// <summary>
        /// Raised when the full-screen routes window opens or closes (the game pauses while it is open).
        /// </summary>
        public event Action<bool>? WindowOpenChanged;

        /// <summary>
        /// Gets whether the full-screen routes window is open.
        /// </summary>
        public bool IsWindowOpen => _window.IsOpen;

        /// <summary>
        /// Gets whether the routes window or one of its side windows is open.
        /// </summary>
        public bool IsAnyOpen => _window.IsOpen || _stations.IsOpen || _vehicles.IsOpen;

        /// <summary>
        /// Gets the route whose station window is open, so a map click on a station appends to it; otherwise null.
        /// </summary>
        public int? EditingStationsRouteId => _stations.IsOpen ? _stations.RouteId : null;

        public void Bind(RoutesBindings bindings)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        }

        public void ToggleRoutes()
        {
            if (IsAnyOpen)
            {
                CloseAll();
                return;
            }

            OpenRoutes(Array.Empty<int>());
        }

        /// <summary>
        /// Opens the routes window with <paramref name="highlightedRouteIds"/> drawn highlighted.
        /// </summary>
        public void OpenRoutes(IReadOnlyCollection<int> highlightedRouteIds)
        {
            _onBeforeOpen();
            CloseAll();
            _window.SetHighlightedRoutes(highlightedRouteIds);
            _window.SetOpen(true);
            NotifyWindowOpenChanged();
        }

        public void SetRoutesOpen(bool open)
        {
            if (open)
            {
                OpenRoutes(Array.Empty<int>());
            }
            else
            {
                CloseAll();
            }
        }

        /// <summary>
        /// Shows the outcome of a map click on a station in the open station window.
        /// </summary>
        public void ShowStopResult(RouteStopAddResult result)
        {
            _stations.ShowStopResult(result);
        }

        public void CloseAll()
        {
            _window.CloseDialog();
            _window.SetOpen(false);
            _stations.SetOpen(false);
            _vehicles.SetOpen(false);
            NotifyWindowOpenChanged();
        }

        public bool IsPanel(VisualElement? element)
        {
            return element == _window.Panel || element == _stations.Panel || element == _vehicles.Panel;
        }

        public void ForEachPanel(Action<VisualElement> register)
        {
            register(_window.Panel);
            register(_stations.Panel);
            register(_vehicles.Panel);
        }

        public void LoadTexts()
        {
            _requestedKeys.Clear();
            RequestKeys(RoutesUiKeys.StaticKeys);
        }

        public void Update()
        {
            _window.Refresh(force: false);
            _stations.Refresh(force: false);
            _vehicles.Refresh(force: false);
            if (_lastToggleTextVersion != _text.Version)
            {
                _lastToggleTextVersion = _text.Version;
                ShellTooltip.SetIconButtonName(_toggle, _text.Get(RoutesUiKeys.Toggle));
            }
        }

        public void Dispose()
        {
            _toggle.clicked -= _toggleHandler;
            _window.Dispose();
            _stations.Dispose();
            _vehicles.Dispose();
            _bindings = null;
        }

        private static T Require<T>(VisualElement root, string name)
            where T : VisualElement
        {
            return root.Q<T>(name) ?? throw new InvalidOperationException(
                $"MapPlayShell UXML must define the routes element '{name}'.");
        }

        private void OpenStations(int routeId)
        {
            _window.CloseDialog();
            _window.SetOpen(false);
            _vehicles.SetOpen(false);
            _stations.ShowRoute(routeId);
            _stations.SetOpen(true);
            NotifyWindowOpenChanged();
        }

        private void OpenVehicles(int routeId)
        {
            _window.CloseDialog();
            _window.SetOpen(false);
            _stations.SetOpen(false);
            _vehicles.ShowRoute(routeId);
            RequestVehicleNameKeys(routeId);
            _vehicles.SetOpen(true);
            NotifyWindowOpenChanged();
        }

        private void BackToWindow()
        {
            _stations.SetOpen(false);
            _vehicles.SetOpen(false);
            _window.SetOpen(true);
            NotifyWindowOpenChanged();
        }

        private void NotifyWindowOpenChanged()
        {
            if (_windowWasOpen == _window.IsOpen)
            {
                return;
            }

            _windowWasOpen = _window.IsOpen;
            WindowOpenChanged?.Invoke(_windowWasOpen);
        }

        // Vehicle type names come from the catalog keys of the vehicles listed in the open vehicle window.
        private void RequestVehicleNameKeys(int? routeId)
        {
            if (_bindings == null || !routeId.HasValue)
            {
                return;
            }

            RouteDetail? detail = _bindings.Detail(routeId.Value);
            if (detail == null)
            {
                return;
            }

            for (int index = 0; index < detail.Vehicles.Count; index++)
            {
                RequestKey(detail.Vehicles[index].TypeNameKey);
            }

            for (int index = 0; index < detail.UnassignedVehicles.Count; index++)
            {
                RequestKey(detail.UnassignedVehicles[index].TypeNameKey);
            }

            for (int index = 0; index < detail.BuyOptions.Count; index++)
            {
                RequestKey(detail.BuyOptions[index].NameKey);
            }
        }

        private void RequestKeys(IEnumerable<string> keys)
        {
            foreach (string key in keys)
            {
                RequestKey(key);
            }
        }

        private void RequestKey(string key)
        {
            if (!_requestedKeys.Add(key))
            {
                return;
            }

            var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(GameUiKeys.Table, key);
            operation.Completed += handle =>
            {
                if (!string.IsNullOrEmpty(handle.Result))
                {
                    _text.Set(key, handle.Result);
                }
            };
        }
    }
}
