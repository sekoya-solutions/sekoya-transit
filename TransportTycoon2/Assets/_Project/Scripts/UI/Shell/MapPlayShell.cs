#nullable enable
using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// MapPlay HUD shell: localized cash and hint strip. Read-only; does not mutate simulation state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MapPlayShell : MonoBehaviour
    {
        private Label? _cashLabel;
        private Label? _hintLabel;
        private Func<long>? _cashProvider;
        private string _cashTemplate = "Cash: {0}";
        private CultureInfo _culture = CultureInfo.GetCultureInfo("en");
        private long _lastCash = long.MinValue;
        private bool _localizationReady;

        /// <summary>
        /// Binds a cash provider from the composition root (Presentation). Safe to call before or after Start.
        /// </summary>
        public void Bind(Func<long> cashProvider)
        {
            _cashProvider = cashProvider ?? throw new ArgumentNullException(nameof(cashProvider));
            _lastCash = long.MinValue;
        }

        private IEnumerator Start()
        {
            UIDocument document = GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            _cashLabel = root.Q<Label>("cash-label");
            _hintLabel = root.Q<Label>("hint-label");
            if (_cashLabel == null || _hintLabel == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define cash-label and hint-label.");
            }

            yield return LocalizationSettings.InitializationOperation;
            ApplyCultureFromLocale(LocalizationSettings.SelectedLocale);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            RefreshStaticTexts();
            _localizationReady = true;
            RefreshCash(force: true);
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            _cashProvider = null;
            _cashLabel = null;
            _hintLabel = null;
        }

        private void Update()
        {
            RefreshCash(force: false);
        }

        private void OnLocaleChanged(Locale locale)
        {
            ApplyCultureFromLocale(locale);
            RefreshStaticTexts();
            RefreshCash(force: true);
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
            if (_hintLabel == null)
            {
                return;
            }

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

            var hintOp = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                GameUiKeys.Table,
                GameUiKeys.Hint);
            hintOp.Completed += handle =>
            {
                if (_hintLabel != null && !string.IsNullOrEmpty(handle.Result))
                {
                    _hintLabel.text = handle.Result;
                }
            };
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
