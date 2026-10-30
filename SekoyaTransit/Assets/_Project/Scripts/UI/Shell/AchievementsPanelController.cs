#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.UIElements;

namespace SekoyaTransit.UI.Shell
{
    /// <summary>
    /// Achievements window (Issue #66, reworked in Issue #223): the Global, Company, and Resources sections, each track
    /// with its label, current value, and a bar filled up to the value with the bronze, silver, and gold trophies evenly
    /// spaced along it. Unlocked trophies are in full color; locked ones are greyed out.
    /// </summary>
    public sealed class AchievementsPanelController : ShellWindowController
    {
        private static readonly string[] _tierClasses =
        {
            "achievements-milestone--bronze",
            "achievements-milestone--silver",
            "achievements-milestone--gold"
        };

        private readonly ScrollView _list;
        private readonly Label _empty;
        private readonly Func<IReadOnlyList<AchievementTrackItem>> _provider;
        private readonly ICargoIconSource? _cargoIcons;
        private IReadOnlyList<AchievementTrackRow> _lastRows = Array.Empty<AchievementTrackRow>();

        /// <summary>
        /// Initializes the controller over the achievements window elements.
        /// </summary>
        public AchievementsPanelController(
            VisualElement panel,
            Label title,
            Button close,
            ScrollView list,
            Label empty,
            Func<IReadOnlyList<AchievementTrackItem>> provider,
            Action onCloseRequested,
            ICargoIconSource? cargoIcons,
            ShellTextCache text,
            Func<CultureInfo> culture)
            : base(
                panel,
                "achievements-panel--hidden",
                title,
                GoalsUiKeys.AchievementsTitle,
                close,
                GoalsUiKeys.AchievementsClose,
                onCloseRequested,
                text,
                culture)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _empty = empty ?? throw new ArgumentNullException(nameof(empty));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _cargoIcons = cargoIcons;
        }

        /// <inheritdoc />
        protected override void DisposeContent()
        {
            _list.Clear();
            _lastRows = Array.Empty<AchievementTrackRow>();
        }

        /// <inheritdoc />
        protected override void RefreshWindowContent(bool force)
        {
            IReadOnlyList<AchievementTrackRow> rows = AchievementsViewModel.BuildRows(_provider(), Text, Culture());
            _empty.text = Text.Get(GoalsUiKeys.AchievementsEmpty);
            _empty.style.display = rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (!force && SameRows(rows))
            {
                return;
            }

            _lastRows = rows;
            _list.Clear();
            VisualElement? section = null;
            AchievementSection? currentSection = null;
            for (int index = 0; index < rows.Count; index++)
            {
                AchievementTrackRow row = rows[index];
                if (section == null || currentSection != row.Section)
                {
                    currentSection = row.Section;
                    var heading = new Label(Text.Get(AchievementsViewModel.SectionKey(row.Section)));
                    heading.AddToClassList("achievements-section-title");
                    _list.Add(heading);
                    section = new VisualElement();
                    section.AddToClassList("achievements-section");
                    _list.Add(section);
                }

                section.Add(BuildTrack(row));
            }
        }

        private VisualElement BuildTrack(AchievementTrackRow row)
        {
            var track = new VisualElement();
            track.AddToClassList("achievements-track");

            var labels = new VisualElement();
            labels.AddToClassList("achievements-track__labels");
            var label = new TextLineView(_cargoIcons);
            label.SetLine(row.Label);
            label.AddToClassList("achievements-track__label");
            labels.Add(label);
            var value = new Label(row.ValueText);
            value.AddToClassList("achievements-track__value");
            labels.Add(value);
            track.Add(labels);

            var bar = new VisualElement();
            bar.AddToClassList("achievements-track__bar-area");
            var line = new VisualElement();
            line.AddToClassList("achievements-track__bar");
            var fill = new VisualElement();
            fill.AddToClassList("achievements-track__fill");
            fill.style.width = Length.Percent(Math.Clamp(row.Fill, 0f, 1f) * 100f);
            line.Add(fill);
            bar.Add(line);
            for (int tier = 0; tier < row.Milestones.Count; tier++)
            {
                bar.Add(BuildMilestone(row.Milestones[tier], tier));
            }

            track.Add(bar);
            return track;
        }

        private static VisualElement BuildMilestone(AchievementMilestoneRow milestone, int tier)
        {
            var element = new VisualElement();
            element.AddToClassList("achievements-milestone");
            element.AddToClassList(_tierClasses[tier]);
            if (milestone.IsUnlocked)
            {
                element.AddToClassList("achievements-milestone--unlocked");
            }

            element.style.left = Length.Percent(AchievementsViewModel.MilestonePosition(tier) * 100f);

            var title = new Label(milestone.Title);
            title.AddToClassList("achievements-milestone__title");
            element.Add(title);
            var trophy = new VisualElement();
            trophy.AddToClassList("achievements-milestone__trophy");
            element.Add(trophy);
            var goal = new Label(milestone.GoalText);
            goal.AddToClassList("achievements-milestone__goal");
            element.Add(goal);
            if (milestone.DateText != null)
            {
                var date = new Label(milestone.DateText);
                date.AddToClassList("achievements-milestone__date");
                element.Add(date);
            }

            return element;
        }

        private bool SameRows(IReadOnlyList<AchievementTrackRow> rows)
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
    }
}
