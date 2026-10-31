#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Hosts the vehicles, unlocks, industries, and towns panels (Issue #178), so
    /// <see cref="MapPlayShell"/> only wires them in. Owns the localized-text cache the view-models read, the four
    /// toggle buttons, the town/industry selection, and the rule that one list panel is open at a time.
    /// </summary>
    public sealed class ListShellPanels
    {
        private const long WagonPurchaseFallback = 4000L;

        private static readonly string[] StaticKeys =
        {
            GameUiKeys.VehiclesToggle,
            GameUiKeys.UnlocksToggle,
            GameUiKeys.IndustriesToggle,
            GameUiKeys.TownsToggle,
            GameUiKeys.VehiclesTitle,
            GameUiKeys.UnlocksTitle,
            GameUiKeys.IndustriesTitle,
            GameUiKeys.TownsTitle,
            GameUiKeys.VehiclesEmpty,
            GameUiKeys.UnlocksEmpty,
            GameUiKeys.IndustriesEmpty,
            GameUiKeys.TownsEmpty,
            GameUiKeys.VehiclesSell,
            GameUiKeys.VehiclesAddWagon,
            GameUiKeys.VehiclesSellWagon,
            GameUiKeys.VehiclesRow,
            GameUiKeys.VehiclesRowRail,
            GameUiKeys.VehiclesKindRoad,
            GameUiKeys.VehiclesKindRail,
            GameUiKeys.VehiclesStatusParked,
            GameUiKeys.VehiclesStatusMoving,
            GameUiKeys.VehiclesStatusLoading,
            GameUiKeys.VehiclesStatusStuck,
            RoutesUiKeys.VehiclesStatusUnassigned,
            RoutesUiKeys.VehiclesRoute,
            RoutesUiKeys.VehiclesStationHint,
            RoutesUiKeys.VehiclesManageRoutes,
            GameUiKeys.UnlocksRowFuture,
            GameUiKeys.UnlocksRowUnlockable,
            GameUiKeys.UnlocksRowUnlocked,
            GameUiKeys.UnlocksAction,
            GameUiKeys.UnlocksActionUnlocked,
            GameUiKeys.IndustriesRow,
            GameUiKeys.TownsSummary,
            GameUiKeys.TownsNeedLine,
            GameUiKeys.TownsPassengerLine,
            GameUiKeys.TownsMailLine,
        };

        private readonly ShellTextCache _text = new ShellTextCache();
        private readonly ShellTextLoader _loader;
        private readonly ListSelection _selection = new ListSelection();
        private readonly Func<CultureInfo> _culture;
        private readonly Action _onBeforeOpen;
        private readonly Button _vehiclesToggle;
        private readonly Button _unlocksToggle;
        private readonly Button _industriesToggle;
        private readonly Button _townsToggle;
        private readonly VehiclesPanelController _vehicles;
        private readonly UnlocksPanelController _unlocks;
        private readonly IndustriesPanelController _industries;
        private readonly TownsPanelController _towns;
        private readonly ShellPanelController[] _panels;
        private readonly Action _vehiclesToggleHandler;
        private readonly Action _unlocksToggleHandler;
        private readonly Action _industriesToggleHandler;
        private readonly Action _townsToggleHandler;
        private ListPanelBindings? _bindings;
        private bool _textsLoaded;
        private int _lastToggleTextVersion = int.MinValue;

        /// <summary>
        /// Queries the panel elements from the UXML root. <paramref name="onBeforeOpen"/> runs before a panel opens so
        /// the shell can close its other panels. <paramref name="cargoIcons"/> supplies the cargo icons in the rows
        /// (null shows the cargo names only).
        /// </summary>
        public ListShellPanels(
            VisualElement root,
            Func<CultureInfo> culture,
            Action onBeforeOpen,
            ICargoIconSource? cargoIcons)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            _culture = culture ?? throw new ArgumentNullException(nameof(culture));
            _onBeforeOpen = onBeforeOpen ?? throw new ArgumentNullException(nameof(onBeforeOpen));
            _loader = new ShellTextLoader(_text);
            Button? vehiclesToggle = root.Q<Button>("vehicles-toggle");
            Button? unlocksToggle = root.Q<Button>("unlocks-toggle");
            Button? industriesToggle = root.Q<Button>("industries-toggle");
            Button? townsToggle = root.Q<Button>("towns-toggle");
            VisualElement? vehiclesPanel = root.Q<VisualElement>("vehicles-panel");
            VisualElement? unlocksPanel = root.Q<VisualElement>("unlocks-panel");
            VisualElement? industriesPanel = root.Q<VisualElement>("industries-panel");
            VisualElement? townsPanel = root.Q<VisualElement>("towns-panel");
            Label? vehiclesTitle = root.Q<Label>("vehicles-title");
            Label? unlocksTitle = root.Q<Label>("unlocks-title");
            Label? industriesTitle = root.Q<Label>("industries-title");
            Label? townsTitle = root.Q<Label>("towns-title");
            Label? vehiclesStationHint = root.Q<Label>("vehicles-station-hint");
            Button? vehiclesManageRoutes = root.Q<Button>("vehicles-manage-routes");
            ScrollView? vehiclesList = root.Q<ScrollView>("vehicles-list");
            ScrollView? unlocksList = root.Q<ScrollView>("unlocks-list");
            ScrollView? industriesList = root.Q<ScrollView>("industries-list");
            ScrollView? townsList = root.Q<ScrollView>("towns-list");
            Label? vehiclesEmpty = root.Q<Label>("vehicles-empty");
            Label? unlocksEmpty = root.Q<Label>("unlocks-empty");
            Label? industriesEmpty = root.Q<Label>("industries-empty");
            Label? townsEmpty = root.Q<Label>("towns-empty");
            if (vehiclesToggle == null
                || unlocksToggle == null
                || industriesToggle == null
                || townsToggle == null
                || vehiclesPanel == null
                || unlocksPanel == null
                || industriesPanel == null
                || townsPanel == null
                || vehiclesTitle == null
                || unlocksTitle == null
                || industriesTitle == null
                || townsTitle == null
                || vehiclesStationHint == null
                || vehiclesManageRoutes == null
                || vehiclesList == null
                || unlocksList == null
                || industriesList == null
                || townsList == null
                || vehiclesEmpty == null
                || unlocksEmpty == null
                || industriesEmpty == null
                || townsEmpty == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define the vehicles (including the station hint and Manage routes button), unlocks, industries, and towns panels and toggles.");
            }

            _vehiclesToggle = vehiclesToggle;
            _unlocksToggle = unlocksToggle;
            _industriesToggle = industriesToggle;
            _townsToggle = townsToggle;
            vehiclesPanel.pickingMode = PickingMode.Position;
            unlocksPanel.pickingMode = PickingMode.Position;
            industriesPanel.pickingMode = PickingMode.Position;
            townsPanel.pickingMode = PickingMode.Position;
            _vehicles = new VehiclesPanelController(
                vehiclesPanel,
                vehiclesTitle,
                vehiclesStationHint,
                vehiclesManageRoutes,
                vehiclesList,
                vehiclesEmpty,
                () => _bindings != null ? _bindings.Vehicles() : Array.Empty<VehicleListItem>(),
                ResolveVehiclesState,
                new VehiclesPanelActions(
                    (kind, id) => _bindings?.SellVehicle(kind, id),
                    id => _bindings?.AddRailWagon(id),
                    id => _bindings?.SellRailWagon(id),
                    () => _bindings?.ManageRoutes()),
                cargoIcons,
                _text,
                _culture);
            _unlocks = new UnlocksPanelController(
                unlocksPanel,
                unlocksTitle,
                unlocksList,
                unlocksEmpty,
                () => _bindings != null ? _bindings.Unlocks() : Array.Empty<VehicleUnlockListItem>(),
                typeId => _bindings?.UnlockVehicleType(typeId),
                () => _vehicles.Refresh(force: true),
                _text,
                _culture);
            _industries = new IndustriesPanelController(
                industriesPanel,
                industriesTitle,
                industriesList,
                industriesEmpty,
                () => _bindings != null ? _bindings.Industries() : Array.Empty<IndustryListItem>(),
                _selection,
                (industryId, instanceIndex) => _bindings?.IndustryRowSelected(industryId, instanceIndex),
                RefreshSelectionHighlights,
                cargoIcons,
                _text,
                _culture);
            _towns = new TownsPanelController(
                townsPanel,
                townsTitle,
                townsList,
                townsEmpty,
                () => _bindings != null ? _bindings.Towns() : Array.Empty<TownListItem>(),
                _selection,
                townId => _bindings?.TownRowSelected(townId),
                RefreshSelectionHighlights,
                cargoIcons,
                _text,
                _culture);
            _panels = new ShellPanelController[] { _vehicles, _unlocks, _industries, _towns };
            _vehiclesToggleHandler = () => Toggle(_vehicles);
            _unlocksToggleHandler = () => Toggle(_unlocks);
            _industriesToggleHandler = () => Toggle(_industries);
            _townsToggleHandler = () => Toggle(_towns);
            _vehiclesToggle.clicked += _vehiclesToggleHandler;
            _unlocksToggle.clicked += _unlocksToggleHandler;
            _industriesToggle.clicked += _industriesToggleHandler;
            _townsToggle.clicked += _townsToggleHandler;
        }

        /// <summary>
        /// Gets whether the vehicles panel is open.
        /// </summary>
        public bool IsVehiclesOpen => _vehicles.IsOpen;

        /// <summary>
        /// Gets whether the unlocks panel is open.
        /// </summary>
        public bool IsUnlocksOpen => _unlocks.IsOpen;

        /// <summary>
        /// Gets whether the industries panel is open.
        /// </summary>
        public bool IsIndustriesOpen => _industries.IsOpen;

        /// <summary>
        /// Gets whether the towns panel is open.
        /// </summary>
        public bool IsTownsOpen => _towns.IsOpen;

        /// <summary>
        /// Gets a counter that changes whenever loaded text changes (map labels rebuild on it, Issue #193).
        /// </summary>
        public int TextVersion => _text.Version;

        /// <summary>
        /// Gets loaded text for a key, or the key while it is not loaded (map labels, Issue #193).
        /// </summary>
        public string GetText(string key)
        {
            return _text.Get(key);
        }

        /// <summary>
        /// Gets the town id selected in the towns list, if any.
        /// </summary>
        public int? SelectedTownId => _selection.TownId;

        /// <summary>
        /// Gets whether an industry row is selected in the industries list.
        /// </summary>
        public bool HasSelectedIndustry => _selection.HasIndustry;

        /// <summary>
        /// Supplies the panel data and command callbacks from Presentation and resets selections.
        /// </summary>
        public void Bind(ListPanelBindings bindings)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _selection.Clear();
            for (int index = 0; index < _panels.Length; index++)
            {
                _panels[index].Refresh(force: true);
            }
        }

        /// <summary>
        /// Opens or closes the vehicles panel (shell button or keyboard L).
        /// </summary>
        public void ToggleVehicles()
        {
            Toggle(_vehicles);
        }

        /// <summary>
        /// Opens or closes the unlocks panel (shell button or keyboard U).
        /// </summary>
        public void ToggleUnlocks()
        {
            Toggle(_unlocks);
        }

        /// <summary>
        /// Opens or closes the industries panel (shell button or keyboard I).
        /// </summary>
        public void ToggleIndustries()
        {
            Toggle(_industries);
        }

        /// <summary>
        /// Opens or closes the towns panel (shell button or keyboard T).
        /// </summary>
        public void ToggleTowns()
        {
            Toggle(_towns);
        }

        /// <summary>
        /// Sets vehicles panel visibility. Opening closes the other list panels.
        /// </summary>
        public void SetVehiclesOpen(bool open)
        {
            SetOpen(_vehicles, open);
        }

        /// <summary>
        /// Sets unlocks panel visibility. Opening closes the other list panels.
        /// </summary>
        public void SetUnlocksOpen(bool open)
        {
            SetOpen(_unlocks, open);
        }

        /// <summary>
        /// Sets industries panel visibility. Opening closes the other list panels.
        /// </summary>
        public void SetIndustriesOpen(bool open)
        {
            SetOpen(_industries, open);
        }

        /// <summary>
        /// Sets towns panel visibility. Opening closes the other list panels.
        /// </summary>
        public void SetTownsOpen(bool open)
        {
            SetOpen(_towns, open);
        }

        /// <summary>
        /// Closes all four list panels (a goals panel is opening).
        /// </summary>
        public void CloseAll()
        {
            for (int index = 0; index < _panels.Length; index++)
            {
                _panels[index].SetOpen(false);
            }
        }

        /// <summary>
        /// Opens the towns panel and selects the given town row.
        /// </summary>
        public void SelectTownInList(int townId)
        {
            _selection.SelectTown(townId);
            SetOpen(_towns, true);
            _towns.ScrollSelectionToTop();
            _industries.RefreshSelectionHighlight();
        }

        /// <summary>
        /// Opens the industries panel and selects the given industry instance row.
        /// </summary>
        public void SelectIndustryInList(string industryId, int instanceIndex)
        {
            _selection.SelectIndustry(industryId, instanceIndex);
            SetOpen(_industries, true);
            _industries.ScrollSelectionToTop();
            _towns.RefreshSelectionHighlight();
        }

        /// <summary>
        /// Clears town and industry list selection and refreshes the row highlights.
        /// </summary>
        public void ClearListSelection()
        {
            _selection.Clear();
            RefreshSelectionHighlights();
        }

        /// <summary>
        /// Returns whether <paramref name="element"/> is one of the four panels or inside one.
        /// </summary>
        public bool IsPanel(VisualElement? element)
        {
            for (int index = 0; index < _panels.Length; index++)
            {
                if (element == _panels[index].Panel)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calls <paramref name="register"/> for each panel root (pointer tracking).
        /// </summary>
        public void ForEachPanel(Action<VisualElement> register)
        {
            for (int index = 0; index < _panels.Length; index++)
            {
                register(_panels[index].Panel);
            }
        }

        /// <summary>
        /// Loads every static string (and the industry and vehicle names) through localization. Call once the
        /// localization system is initialized and again on a locale change.
        /// </summary>
        public void LoadTexts()
        {
            _loader.Reset();
            _loader.Request(StaticKeys);
            _loader.Request(GameUiKeys.IndustryNameKeys);
            _loader.Request(GameUiKeys.VehicleNameKeys);
            _textsLoaded = true;
        }

        /// <summary>
        /// Per-frame update: open panels repaint and the toggle texts follow localization.
        /// </summary>
        public void Update()
        {
            if (!_textsLoaded)
            {
                return;
            }

            for (int index = 0; index < _panels.Length; index++)
            {
                _panels[index].Refresh(force: false);
            }

            if (_lastToggleTextVersion != _text.Version)
            {
                _lastToggleTextVersion = _text.Version;
                ShellTooltip.SetIconButtonName(_vehiclesToggle, _text.Get(GameUiKeys.VehiclesToggle));
                ShellTooltip.SetIconButtonName(_unlocksToggle, _text.Get(GameUiKeys.UnlocksToggle));
                ShellTooltip.SetIconButtonName(_industriesToggle, _text.Get(GameUiKeys.IndustriesToggle));
                ShellTooltip.SetIconButtonName(_townsToggle, _text.Get(GameUiKeys.TownsToggle));
            }
        }

        /// <summary>
        /// Releases callbacks and rows.
        /// </summary>
        public void Dispose()
        {
            _vehiclesToggle.clicked -= _vehiclesToggleHandler;
            _unlocksToggle.clicked -= _unlocksToggleHandler;
            _industriesToggle.clicked -= _industriesToggleHandler;
            _townsToggle.clicked -= _townsToggleHandler;
            for (int index = 0; index < _panels.Length; index++)
            {
                _panels[index].Dispose();
            }

            _selection.Clear();
            _bindings = null;
        }

        private void Toggle(ShellPanelController controller)
        {
            SetOpen(controller, !controller.IsOpen);
        }

        private void SetOpen(ShellPanelController target, bool open)
        {
            if (open)
            {
                _onBeforeOpen();
                for (int index = 0; index < _panels.Length; index++)
                {
                    if (!ReferenceEquals(_panels[index], target))
                    {
                        _panels[index].SetOpen(false);
                    }
                }
            }

            target.SetOpen(open);
        }

        private void RefreshSelectionHighlights()
        {
            _towns.RefreshSelectionHighlight();
            _industries.RefreshSelectionHighlight();
        }

        private VehiclesPanelState ResolveVehiclesState()
        {
            return _bindings != null ? _bindings.VehiclesState() : new VehiclesPanelState(false, 0L, WagonPurchaseFallback);
        }
    }
}
