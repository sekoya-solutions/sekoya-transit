#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace TransportTycoon.UI.Shell
{
    /// <summary>
    /// Hosts the finance, achievements, and contracts panels, the toast stack, and the game-over summary
    /// (Issue #66), so <see cref="MapPlayShell"/> only wires them in. Owns the localized-text cache the
    /// view-models read and the three panel toggle buttons.
    /// </summary>
    public sealed class GoalsShellPanels
    {
        private readonly ShellTextCache _text = new ShellTextCache();
        private readonly ToastQueue _toastQueue = new ToastQueue();
        private readonly HashSet<string> _requestedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<CultureInfo> _culture;
        private readonly Action _onBeforeOpen;
        private readonly Button _financeToggle;
        private readonly Button _achievementsToggle;
        private readonly Button _contractsToggle;
        private readonly Label _gameOverScore;
        private readonly Label _gameOverDate;
        private readonly FinancePanelController _finance;
        private readonly AchievementsPanelController _achievements;
        private readonly ContractsPanelController _contracts;
        private readonly ToastController _toasts;
        private readonly Action _financeToggleHandler;
        private readonly Action _achievementsToggleHandler;
        private readonly Action _contractsToggleHandler;
        private Func<FinanceRecap>? _financeProvider;
        private Func<IReadOnlyList<AchievementListItem>>? _achievementsProvider;
        private Func<IReadOnlyList<ContractListItem>>? _contractsProvider;
        private Action<int, int>? _onContractRowSelected;
        private bool _hasSummary;
        private long _summaryScore;
        private string _summaryDate = string.Empty;
        private int _lastSummaryTextVersion = int.MinValue;
        private int _lastToggleTextVersion = int.MinValue;

        /// <summary>
        /// Queries the panel elements from the UXML root. <paramref name="onBeforeOpen"/> runs before a panel opens
        /// so the shell can close its other panels.
        /// </summary>
        public GoalsShellPanels(VisualElement root, Func<CultureInfo> culture, Action onBeforeOpen)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            _culture = culture ?? throw new ArgumentNullException(nameof(culture));
            _onBeforeOpen = onBeforeOpen ?? throw new ArgumentNullException(nameof(onBeforeOpen));
            VisualElement? financePanel = root.Q<VisualElement>("finance-panel");
            Label? financeTitle = root.Q<Label>("finance-title");
            ScrollView? financeList = root.Q<ScrollView>("finance-list");
            VisualElement? achievementsPanel = root.Q<VisualElement>("achievements-panel");
            Label? achievementsTitle = root.Q<Label>("achievements-title");
            ScrollView? achievementsList = root.Q<ScrollView>("achievements-list");
            Label? achievementsEmpty = root.Q<Label>("achievements-empty");
            VisualElement? contractsPanel = root.Q<VisualElement>("contracts-panel");
            Label? contractsTitle = root.Q<Label>("contracts-title");
            ScrollView? contractsList = root.Q<ScrollView>("contracts-list");
            Label? contractsEmpty = root.Q<Label>("contracts-empty");
            VisualElement? toastStack = root.Q<VisualElement>("toast-stack");
            Button? financeToggle = root.Q<Button>("finance-toggle");
            Button? achievementsToggle = root.Q<Button>("achievements-toggle");
            Button? contractsToggle = root.Q<Button>("contracts-toggle");
            Label? gameOverScore = root.Q<Label>("game-over-score");
            Label? gameOverDate = root.Q<Label>("game-over-date");
            if (financePanel == null
                || financeTitle == null
                || financeList == null
                || achievementsPanel == null
                || achievementsTitle == null
                || achievementsList == null
                || achievementsEmpty == null
                || contractsPanel == null
                || contractsTitle == null
                || contractsList == null
                || contractsEmpty == null
                || toastStack == null
                || financeToggle == null
                || achievementsToggle == null
                || contractsToggle == null
                || gameOverScore == null
                || gameOverDate == null)
            {
                throw new InvalidOperationException(
                    "MapPlayShell UXML must define the finance, achievements, and contracts panels and toggles, the toast stack, and the game-over score and date labels.");
            }

            _financeToggle = financeToggle;
            _achievementsToggle = achievementsToggle;
            _contractsToggle = contractsToggle;
            _gameOverScore = gameOverScore;
            _gameOverDate = gameOverDate;
            financePanel.pickingMode = PickingMode.Position;
            achievementsPanel.pickingMode = PickingMode.Position;
            contractsPanel.pickingMode = PickingMode.Position;
            _finance = new FinancePanelController(
                financePanel,
                financeTitle,
                financeList,
                () => _financeProvider != null ? _financeProvider() : EmptyFinance,
                _text,
                _culture);
            _achievements = new AchievementsPanelController(
                achievementsPanel,
                achievementsTitle,
                achievementsList,
                achievementsEmpty,
                () => _achievementsProvider != null
                    ? _achievementsProvider()
                    : Array.Empty<AchievementListItem>(),
                _text,
                _culture);
            _contracts = new ContractsPanelController(
                contractsPanel,
                contractsTitle,
                contractsList,
                contractsEmpty,
                () => _contractsProvider != null
                    ? _contractsProvider()
                    : Array.Empty<ContractListItem>(),
                OnContractRowSelected,
                _text,
                _culture);
            _toasts = new ToastController(toastStack, _toastQueue, _text, _culture);
            _financeToggleHandler = () => Toggle(_finance);
            _achievementsToggleHandler = () => Toggle(_achievements);
            _contractsToggleHandler = () => Toggle(_contracts);
            _financeToggle.clicked += _financeToggleHandler;
            _achievementsToggle.clicked += _achievementsToggleHandler;
            _contractsToggle.clicked += _contractsToggleHandler;
        }

        private static FinanceRecap EmptyFinance { get; } = new FinanceRecap(
            new FinanceYearRecap(0, Array.Empty<FinanceRecapLine>()),
            new FinanceYearRecap(0, Array.Empty<FinanceRecapLine>()));

        /// <summary>
        /// Gets whether the finance panel is open.
        /// </summary>
        public bool IsFinanceOpen => _finance.IsOpen;

        /// <summary>
        /// Gets whether the achievements panel is open.
        /// </summary>
        public bool IsAchievementsOpen => _achievements.IsOpen;

        /// <summary>
        /// Gets whether the contracts panel is open.
        /// </summary>
        public bool IsContractsOpen => _contracts.IsOpen;

        /// <summary>
        /// Supplies the panel data and the contract-row callback from Presentation.
        /// </summary>
        public void Bind(
            Func<FinanceRecap> financeProvider,
            Func<IReadOnlyList<AchievementListItem>> achievementsProvider,
            Func<IReadOnlyList<ContractListItem>> contractsProvider,
            Action<int, int> onContractRowSelected)
        {
            _financeProvider = financeProvider ?? throw new ArgumentNullException(nameof(financeProvider));
            _achievementsProvider = achievementsProvider ?? throw new ArgumentNullException(nameof(achievementsProvider));
            _contractsProvider = contractsProvider ?? throw new ArgumentNullException(nameof(contractsProvider));
            _onContractRowSelected = onContractRowSelected
                ?? throw new ArgumentNullException(nameof(onContractRowSelected));
        }

        /// <summary>
        /// Opens or closes the finance panel (shell button or keyboard F).
        /// </summary>
        public void ToggleFinance()
        {
            Toggle(_finance);
        }

        /// <summary>
        /// Opens or closes the achievements panel (shell button or keyboard H).
        /// </summary>
        public void ToggleAchievements()
        {
            Toggle(_achievements);
        }

        /// <summary>
        /// Opens or closes the contracts panel (shell button or keyboard K).
        /// </summary>
        public void ToggleContracts()
        {
            Toggle(_contracts);
        }

        /// <summary>
        /// Closes all three panels (another shell panel is opening).
        /// </summary>
        public void CloseAll()
        {
            _finance.SetOpen(false);
            _achievements.SetOpen(false);
            _contracts.SetOpen(false);
        }

        /// <summary>
        /// Returns whether <paramref name="element"/> is one of the three panels or inside one.
        /// </summary>
        public bool IsPanel(VisualElement? element)
        {
            return element == _finance.Panel || element == _achievements.Panel || element == _contracts.Panel;
        }

        /// <summary>
        /// Calls <paramref name="register"/> for each panel root (pointer tracking).
        /// </summary>
        public void ForEachPanel(Action<VisualElement> register)
        {
            register(_finance.Panel);
            register(_achievements.Panel);
            register(_contracts.Panel);
        }

        /// <summary>
        /// Shows a toast now (real time). Safe to call before the shell UI is ready.
        /// </summary>
        public void ShowToast(ToastMessage message, float nowSeconds)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            for (int index = 0; index < message.Arguments.Count; index++)
            {
                if (message.Arguments[index].Kind == ToastArgumentKind.LocalizationKey)
                {
                    RequestKeys(new[] { message.Arguments[index].Text });
                }
            }

            RequestKeys(new[] { message.TemplateKey });
            _toasts.Show(message, nowSeconds);
        }

        /// <summary>
        /// Records the final score and bankruptcy date shown on the game-over card.
        /// </summary>
        public void SetGameOverSummary(long finalScore, string dateText)
        {
            _hasSummary = true;
            _summaryScore = finalScore;
            _summaryDate = dateText ?? throw new ArgumentNullException(nameof(dateText));
            _lastSummaryTextVersion = int.MinValue;
            RefreshSummary();
        }

        /// <summary>
        /// Loads every static string (and the industry and achievement names) through localization. Call once the
        /// localization system is initialized and again on a locale change.
        /// </summary>
        public void LoadTexts()
        {
            _requestedKeys.Clear();
            RequestKeys(GoalsUiKeys.StaticKeys);
            RequestKeys(GameUiKeys.IndustryNameKeys);
            RequestAchievementNames();
            _lastSummaryTextVersion = int.MinValue;
        }

        /// <summary>
        /// Per-frame update: toasts expire, open panels repaint, toggle and summary texts follow localization.
        /// </summary>
        public void Update(float nowSeconds)
        {
            _toasts.Refresh(nowSeconds);
            _finance.Refresh(force: false);
            _achievements.Refresh(force: false);
            _contracts.Refresh(force: false);
            if (_lastToggleTextVersion != _text.Version)
            {
                _lastToggleTextVersion = _text.Version;
                _financeToggle.text = _text.Get(GoalsUiKeys.FinanceToggle);
                _achievementsToggle.text = _text.Get(GoalsUiKeys.AchievementsToggle);
                _contractsToggle.text = _text.Get(GoalsUiKeys.ContractsToggle);
            }

            RefreshSummary();
        }

        /// <summary>
        /// Releases callbacks and rows.
        /// </summary>
        public void Dispose()
        {
            _financeToggle.clicked -= _financeToggleHandler;
            _achievementsToggle.clicked -= _achievementsToggleHandler;
            _contractsToggle.clicked -= _contractsToggleHandler;
            _finance.Dispose();
            _achievements.Dispose();
            _contracts.Dispose();
            _toasts.Dispose();
            _financeProvider = null;
            _achievementsProvider = null;
            _contractsProvider = null;
            _onContractRowSelected = null;
        }

        private void Toggle(ShellPanelController controller)
        {
            bool open = !controller.IsOpen;
            if (open)
            {
                _onBeforeOpen();
                CloseAll();
                if (ReferenceEquals(controller, _achievements))
                {
                    RequestAchievementNames();
                }
            }

            controller.SetOpen(open);
        }

        private void OnContractRowSelected(int tileX, int tileY)
        {
            _onContractRowSelected?.Invoke(tileX, tileY);
        }

        private void RequestAchievementNames()
        {
            if (_achievementsProvider == null)
            {
                return;
            }

            IReadOnlyList<AchievementListItem> items = _achievementsProvider();
            var keys = new string[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                keys[index] = items[index].NameKey;
            }

            RequestKeys(keys);
        }

        private void RequestKeys(IEnumerable<string> keys)
        {
            foreach (string key in keys)
            {
                if (!_requestedKeys.Add(key))
                {
                    continue;
                }

                string captured = key;
                var operation = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(GameUiKeys.Table, captured);
                operation.Completed += handle =>
                {
                    if (!string.IsNullOrEmpty(handle.Result))
                    {
                        _text.Set(captured, handle.Result);
                    }
                };
            }
        }

        private void RefreshSummary()
        {
            if (!_hasSummary || _lastSummaryTextVersion == _text.Version)
            {
                return;
            }

            _lastSummaryTextVersion = _text.Version;
            CultureInfo culture = _culture();
            _gameOverScore.text = string.Format(
                culture,
                _text.Get(GoalsUiKeys.GameOverScore),
                CashFormat.FormatAmount(_summaryScore, culture));
            _gameOverDate.text = string.Format(culture, _text.Get(GoalsUiKeys.GameOverDate), _summaryDate);
        }
    }
}
