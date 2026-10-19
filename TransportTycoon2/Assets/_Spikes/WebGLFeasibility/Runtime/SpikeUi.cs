#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TransportTycoon.Spikes.WebGL
{
    // UI Toolkit screen built in code, with texts resolved through the Localization package (async, WebGL-safe).
    [RequireComponent(typeof(UIDocument))]
    public sealed class SpikeUi : MonoBehaviour
    {
        private const string Table = "SpikeStrings";

        private readonly List<Action> _refreshers = new List<Action>();
        private SpikeApp _app = null!;
        private Label _status = null!;
        private Label _log = null!;
        private float _nextStatusUpdate;

        private IEnumerator Start()
        {
            _app = GetComponent<SpikeApp>();
            Build(GetComponent<UIDocument>().rootVisualElement);
            _app.LineAdded += OnLine;

            float start = Time.realtimeSinceStartup;
            yield return LocalizationSettings.InitializationOperation;
            _app.Log($"localization.init ms={(Time.realtimeSinceStartup - start) * 1000f:F0} locales={LocalizationSettings.AvailableLocales.Locales.Count}");
            LocalizationSettings.SelectedLocaleChanged += _ => RefreshTexts();
            RefreshTexts();
        }

        private void Update()
        {
            if (_status == null || Time.unscaledTime < _nextStatusUpdate)
            {
                return;
            }

            _nextStatusUpdate = Time.unscaledTime + 0.5f;
            float ms = Time.unscaledDeltaTime * 1000f;
            _status.text = $"{_app.Mode}  vehicles={_app.VehicleCount}  {1000f / Mathf.Max(ms, 0.01f):F0} fps ({ms:F1} ms)";
        }

        private void Build(VisualElement root)
        {
            var panel = new VisualElement();
            panel.style.position = Position.Absolute;
            panel.style.left = 8;
            panel.style.top = 8;
            panel.style.width = 380;
            panel.style.paddingLeft = panel.style.paddingRight = panel.style.paddingTop = panel.style.paddingBottom = 8;
            panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
            root.Add(panel);

            panel.Add(Localized("title", bold: true));
            _status = Plain();
            panel.Add(_status);

            panel.Add(ButtonRow(
                Localized("renderer", button: () => _app.SetMode(_app.Mode == SpikeMapMode.Mesh ? SpikeMapMode.Tilemap : SpikeMapMode.Mesh)),
                Localized("vehiclesMinus", button: () => _app.SetVehicleCount(_app.VehicleCount - 1000)),
                Localized("vehiclesPlus", button: () => _app.SetVehicleCount(_app.VehicleCount + 1000))));
            panel.Add(ButtonRow(
                Localized("benchmark", button: _app.RunBenchmark),
                Localized("saveTest", button: _app.RunSaveWrite),
                Localized("verifySaves", button: _app.RunSaveVerify)));
            panel.Add(ButtonRow(
                Localized("english", button: () => SelectLocale("en")),
                Localized("french", button: () => SelectLocale("fr"))));

            _log = Plain();
            _log.style.fontSize = 10;
            panel.Add(_log);
        }

        private void OnLine(string line)
        {
            string[] lines = _app.Results.TrimEnd().Split('\n');
            int from = Mathf.Max(0, lines.Length - 12);
            _log.text = string.Join("\n", lines, from, lines.Length - from);
        }

        private static void SelectLocale(string code)
        {
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale != null)
            {
                LocalizationSettings.SelectedLocale = locale;
            }
        }

        private void RefreshTexts()
        {
            foreach (Action refresh in _refreshers)
            {
                refresh();
            }
        }

        private Label Plain()
        {
            var label = new Label();
            label.style.color = Color.white;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private VisualElement Localized(string key, bool bold = false, Action? button = null)
        {
            VisualElement element;
            Action<string> setText;
            if (button != null)
            {
                var b = new Button(button);
                b.style.flexGrow = 1;
                element = b;
                setText = text => b.text = text;
            }
            else
            {
                Label label = Plain();
                label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
                element = label;
                setText = text => label.text = text;
            }

            setText(key);
            _refreshers.Add(() =>
            {
                var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(Table, key);
                op.Completed += handle => setText(handle.Result);
            });
            return element;
        }

        private static VisualElement ButtonRow(params VisualElement[] children)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            foreach (VisualElement child in children)
            {
                row.Add(child);
            }

            return row;
        }
    }
}
