#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// MapPlay HUD shell: localized cash, hint, and build toolbar. Does not mutate simulation state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MapPlayShell : MonoBehaviour
    {
        private static readonly BuildToolId[] ToolbarTools =
        {
            BuildToolId.Select,
            BuildToolId.Road,
            BuildToolId.DemolishRoad,
            BuildToolId.Station,
            BuildToolId.DemolishStation,
        };

        private Label? _cashLabel;
        private Label? _hintLabel;
        private VisualElement? _root;
        private readonly Dictionary<BuildToolId, Button> _toolButtons = new Dictionary<BuildToolId, Button>();
        private readonly Dictionary<BuildToolId, Action> _toolClickHandlers = new Dictionary<BuildToolId, Action>();
        private Func<long>? _cashProvider;
        private Action<BuildToolId>? _onToolChosen;
        private string _cashTemplate = "Cash: {0}";
        private CultureInfo _culture = CultureInfo.GetCultureInfo("en");
        private long _lastCash = long.MinValue;
        private BuildToolId _activeTool = BuildToolId.Select;
        private bool _localizationReady;
        private bool _uiReady;

        /// <summary>
        /// Binds cash and tool-selection callbacks from the composition root (Presentation).
        /// </summary>
        public void Bind(Func<long> cashProvider, Action<BuildToolId> onToolChosen)
        {
            _cashProvider = cashProvider ?? throw new ArgumentNullException(nameof(cashProvider));
            _onToolChosen = onToolChosen ?? throw new ArgumentNullException(nameof(onToolChosen));
            _lastCash = long.MinValue;
        }

        /// <summary>
        /// Updates toolbar highlight and hint for the active tool (keyboard or UI).
        /// </summary>
        public void SetActiveBuildTool(BuildToolId tool)
        {
            _activeTool = tool;
            RefreshToolHighlight();
            RefreshHint();
        }

        /// <summary>
        /// Returns whether a screen-space pointer is over an interactive shell control (blocks map clicks).
        /// </summary>
        public bool IsScreenPositionOverInteractiveUi(Vector2 screenPosition)
        {
            if (!_uiReady || _root == null)
            {
                return false;
            }

            IPanel? panel = _root.panel;
            if (panel == null)
            {
                return false;
            }

            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
            VisualElement? picked = panel.Pick(panelPosition);
            if (picked == null || picked == _root)
            {
                return false;
            }

            return picked.pickingMode != PickingMode.Ignore;
        }

        private IEnumerator Start()
        {
            UIDocument document = GetComponent<UIDocument>();
            _root = document.rootVisualElement;
            _cashLabel = _root.Q<Label>("cash-label");
            _hintLabel = _root.Q<Label>("hint-label");
            if (_cashLabel == null || _hintLabel == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define cash-label and hint-label.");
            }

            BindToolbarButtons(_root);
            _uiReady = true;

            yield return LocalizationSettings.InitializationOperation;
            ApplyCultureFromLocale(LocalizationSettings.SelectedLocale);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            RefreshStaticTexts();
            _localizationReady = true;
            RefreshCash(force: true);
            RefreshHint();
            RefreshToolHighlight();
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            foreach (KeyValuePair<BuildToolId, Button> pair in _toolButtons)
            {
                if (_toolClickHandlers.TryGetValue(pair.Key, out Action handler))
                {
                    pair.Value.clicked -= handler;
                }
            }

            _toolButtons.Clear();
            _toolClickHandlers.Clear();
            _cashProvider = null;
            _onToolChosen = null;
            _cashLabel = null;
            _hintLabel = null;
            _root = null;
            _uiReady = false;
        }

        private void Update()
        {
            RefreshCash(force: false);
        }

        private void BindToolbarButtons(VisualElement root)
        {
            _toolButtons.Clear();
            _toolClickHandlers.Clear();
            RegisterToolButton(root, "tool-select", BuildToolId.Select);
            RegisterToolButton(root, "tool-road", BuildToolId.Road);
            RegisterToolButton(root, "tool-demolish-road", BuildToolId.DemolishRoad);
            RegisterToolButton(root, "tool-station", BuildToolId.Station);
            RegisterToolButton(root, "tool-demolish-station", BuildToolId.DemolishStation);
        }

        private void RegisterToolButton(VisualElement root, string name, BuildToolId tool)
        {
            Button? button = root.Q<Button>(name);
            if (button == null)
            {
                throw new InvalidOperationException("MapPlayShell UXML must define button " + name + ".");
            }

            Action handler = () => _onToolChosen?.Invoke(tool);
            button.clicked += handler;
            _toolButtons[tool] = button;
            _toolClickHandlers[tool] = handler;
        }

        private void OnLocaleChanged(Locale locale)
        {
            ApplyCultureFromLocale(locale);
            RefreshStaticTexts();
            RefreshCash(force: true);
            RefreshHint();
        }

        private void ApplyCultureFromLocale(Locale? locale)
        {
            if (locale == null)
            {
                _culture = CultureInfo.GetCultureInfo("en");
                return;
            }

            try
            {
                _culture = CultureInfo.GetCultureInfo(locale.Identifier.Code);
            }
            catch (CultureNotFoundException)
            {
                _culture = CultureInfo.GetCultureInfo("en");
            }
        }

        private void RefreshStaticTexts()
        {
            var cashOp = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                GameUiKeys.Table,
                GameUiKeys.Cash);
            cashOp.Completed += handle =>
            {
                if (!string.IsNullOrEmpty(handle.Result))
                {
                    _cashTemplate = handle.Result;
                    RefreshCash(force: true);
                }
            };

            foreach (BuildToolId tool in ToolbarTools)
            {
                if (!_toolButtons.ContainsKey(tool))
                {
                    continue;
                }

                BuildToolId captured = tool;
                var labelOp = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                    GameUiKeys.Table,
                    GameUiKeys.ToolLabelKey(captured));
                labelOp.Completed += handle =>
                {
                    if (_toolButtons.TryGetValue(captured, out Button live) && !string.IsNullOrEmpty(handle.Result))
                    {
                        live.text = handle.Result;
                    }
                };
            }
        }

        private void RefreshHint()
        {
            if (_hintLabel == null)
            {
                return;
            }

            string key = GameUiKeys.HintKey(_activeTool);
            var hintOp = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                GameUiKeys.Table,
                key);
            hintOp.Completed += handle =>
            {
                if (_hintLabel != null && !string.IsNullOrEmpty(handle.Result))
                {
                    _hintLabel.text = handle.Result;
                }
            };
        }

        private void RefreshToolHighlight()
        {
            foreach (KeyValuePair<BuildToolId, Button> pair in _toolButtons)
            {
                if (pair.Key == _activeTool)
                {
                    pair.Value.AddToClassList("build-tool--active");
                }
                else
                {
                    pair.Value.RemoveFromClassList("build-tool--active");
                }
            }
        }

        private void RefreshCash(bool force)
        {
            if (!_localizationReady || _cashLabel == null || _cashProvider == null)
            {
                return;
            }

            long cash = _cashProvider();
            if (!force && cash == _lastCash)
            {
                return;
            }

            _lastCash = cash;
            _cashLabel.text = CashFormat.FormatLabeled(_cashTemplate, cash, _culture);
        }
    }
}
