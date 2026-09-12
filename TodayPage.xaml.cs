using System.Globalization;
using Microsoft.Maui.Graphics;
using MoleculeEfficienceTracker.Core.Design;
using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    /// <summary>
    /// L'écran d'ouverture : ce qu'il faut savoir, puis ce qu'il faut faire.
    ///
    /// Il s'appelait Charge et ne montrait que des moyennes — trois cartes vides
    /// sur cinq molécules dont quatre dorment depuis des mois. Il lisait et
    /// désérialisait en outre près de cent fois les mêmes fichiers à chaque
    /// affichage, et interrogeait « alcool » là où la page Alcool écrivait
    /// « alcohol », si bien que cette ligne affichait zéro depuis l'origine.
    /// </summary>
    public partial class TodayPage : ContentPage
    {
        private readonly UsageStatsService _stats = new(MoleculeKeys.All);
        private readonly CurrentLoadService _loads = new();
        private readonly CaffeineCalculator _caffeine = new();
        private readonly DataPersistenceService _caffeineStore = new(MoleculeKeys.Caffeine);
        private readonly CaffeineNotificationService _notifications;
        private readonly IAlertService _alerts;

        private static readonly int[] Periods = { 1, 7, 30 };

        public TodayPage()
        {
            InitializeComponent();

            _notifications = ServiceLocator.GetOptional<CaffeineNotificationService>() ?? new CaffeineNotificationService();
            _alerts = ServiceLocator.GetOptional<IAlertService>() ?? new AlertService();

            PeriodPicker.SelectedIndex = 1;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            try
            {
                await RefreshCaffeineAnswerAsync();
                await RefreshLoadsAsync();
                await RefreshStatsAsync();
            }
            catch (Exception ex)
            {
                SummaryLabel.Text = $"Statistiques indisponibles : {ex.Message}";
            }
        }

        // ===== 1. La question du matin =====

        private async Task RefreshCaffeineAnswerAsync()
        {
            DateTime now = DateTime.Now;

            List<DoseEntry> doses;
            try
            {
                doses = await _caffeineStore.LoadDosesAsync();
            }
            catch (DoseDataCorruptedException)
            {
                CaffeineHeadlineLabel.Text = "Le fichier caféine n'a pas pu être relu.";
                CaffeineDetailLabel.Text = "Une copie a été mise de côté. Rien n'a été effacé.";
                QuickAddButton.IsVisible = false;
                return;
            }

            CaffeineAdvisor.Advice advice = CaffeineAdvisor.Build(_caffeine, doses, now);

            CaffeineHeadlineLabel.Text = advice.Headline;
            CaffeineDetailLabel.Text = advice.Detail;

            QuickAddButton.IsVisible = true;
            QuickAddButton.Text = $"Enregistrer un café de {advice.PresetMg:0} mg";
            SemanticProperties.SetDescription(QuickAddButton,
                $"Enregistrer maintenant un café de {advice.PresetMg:0} milligrammes");
        }

        /// <summary>
        /// Le geste du matin : un appui, sans changer d'écran ni ouvrir de
        /// formulaire.
        /// </summary>
        private async void OnQuickAddClicked(object? sender, EventArgs e)
        {
            try
            {
                double preset = UserPreferences.GetCaffeinePresets().FirstOrDefault(CaffeineCalculator.MG_PER_UNIT);

                List<DoseEntry> doses = await _caffeineStore.LoadDosesAsync();
                doses.Insert(0, new DoseEntry(DateTime.Now, preset, UserPreferences.GetWeightKg(), MoleculeKeys.Caffeine));

                await _caffeineStore.SaveDosesAsync(doses.OrderByDescending(d => d.TimeTaken).ToList());
                await _notifications.ScheduleCutoffAsync(doses, DateTime.Now);

                _stats.Invalidate();
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                await _alerts.ShowAlertAsync("Enregistrement impossible", ex.Message);
            }
        }

        // ===== 2. Ce qui circule encore =====

        private async Task RefreshLoadsAsync()
        {
            List<MoleculeLoad> loads = await _loads.GetPresentAsync(DateTime.Now);
            List<LoadRow> rows = loads.Select(l => new LoadRow(l)).ToList();

            LoadsCollection.ItemsSource = rows;
            LoadsCollection.IsVisible = rows.Count > 0;
            LoadsEmptyLabel.IsVisible = rows.Count == 0;
        }

        // ===== 3. Les moyennes =====

        private async void OnPeriodChanged(object? sender, EventArgs e)
        {
            // Le sélecteur déclenche aussi au premier positionnement, avant même
            // l'affichage : une exception y serait sans rattrapage.
            try
            {
                await RefreshStatsAsync();
            }
            catch (Exception ex)
            {
                SummaryLabel.Text = $"Statistiques indisponibles : {ex.Message}";
            }
        }

        private async Task RefreshStatsAsync()
        {
            int index = Math.Clamp(PeriodPicker.SelectedIndex, 0, Periods.Length - 1);
            int days = Periods[index];

            _stats.Invalidate();

            List<StatRow> rows = await BuildRowsAsync(days);

            StatsCollection.ItemsSource = rows;
            StatsCollection.IsVisible = rows.Count > 0;
            StatsEmptyLabel.IsVisible = rows.Count == 0;

            SummaryLabel.Text = rows.Count > 0
                ? $"Mis à jour à {DateTime.Now:HH\\hmm}. La variation compare la période à la précédente de même durée."
                : $"Mis à jour à {DateTime.Now:HH\\hmm}.";
        }

        private async Task<List<StatRow>> BuildRowsAsync(int days)
        {
            var rows = new List<StatRow>();

            foreach (string key in MoleculeKeys.All)
            {
                List<DailyStats> history = await _stats.GetDailyStatsAsync(key, days * 2);
                if (history.Count == 0) continue;

                List<DailyStats> current = history.TakeLast(days).ToList();
                List<DailyStats> previous = history.Take(history.Count - days).ToList();

                double average = current.Count > 0 ? current.Average(d => d.TotalDose) : 0;
                double before = previous.Count > 0 ? previous.Average(d => d.TotalDose) : 0;
                int takes = current.Sum(d => d.Count);

                if (average <= 0 && takes == 0) continue;

                double? variation = before > 0 ? 100.0 * (average - before) / before : null;

                rows.Add(new StatRow(key, average, takes, days, variation));
            }

            return rows;
        }

        /// <summary>Une molécule encore en circulation, prête à afficher.</summary>
        public sealed class LoadRow
        {
            private readonly MoleculeLoad _load;

            public LoadRow(MoleculeLoad load) => _load = load;

            public string Name => _load.Name;

            public Color Accent => EffectPalette.ForMolecule(_load.Key);

            public Color LevelColor => EffectPalette.For(_load.Level);

            public string LevelDisplay => EffectPalette.Describe(_load.Level);

            public string ValueDisplay => string.Format(
                CultureInfo.CurrentCulture, "{0:0.##} {1}", _load.Concentration, _load.ConcentrationUnit);

            public string Detail
            {
                get
                {
                    var parts = new List<string>();

                    if (_load.Amount > 0)
                        parts.Add(string.Format(CultureInfo.CurrentCulture,
                            "{0:0.##} {1} restants", _load.Amount, _load.AmountUnit));

                    if (_load.LastIntake is DateTime last)
                    {
                        double hours = (DateTime.Now - last).TotalHours;
                        parts.Add(hours < 1
                            ? $"dernière prise il y a {hours * 60:0} min"
                            : $"dernière prise il y a {hours:0.#} h");
                    }

                    return string.Join(" · ", parts);
                }
            }
        }

        /// <summary>Une ligne de statistiques, prête à afficher.</summary>
        public sealed class StatRow
        {
            private readonly string _key;
            private readonly double _average;
            private readonly int _takes;
            private readonly int _days;
            private readonly double? _variation;

            public StatRow(string key, double average, int takes, int days, double? variation)
            {
                _key = key;
                _average = average;
                _takes = takes;
                _days = days;
                _variation = variation;
            }

            public string MoleculeName => MoleculeKeys.DisplayName(_key);

            public Color Accent => EffectPalette.ForMolecule(_key);

            public string AverageDisplay =>
                string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}/j", _average, MoleculeKeys.DoseUnit(_key));

            public string Detail => _takes switch
            {
                0 => "aucune prise",
                1 => "1 prise sur la période",
                _ => $"{_takes} prises · {_takes / (double)_days:0.#} par jour"
            };

            /// <summary>
            /// La variation porte toujours un glyphe en plus de sa couleur — c'était
            /// déjà le cas ici, et c'est désormais la règle partout. La palette
            /// renonce en revanche au vert : hausse en orange, baisse en bleu.
            /// </summary>
            public string VariationDisplay => _variation switch
            {
                null => "—",
                > 1 => $"▲ {_variation:0} %",
                < -1 => $"▼ {Math.Abs(_variation.Value):0} %",
                _ => "= stable"
            };

            public Color VariationColor => _variation switch
            {
                null => EffectPalette.Landmark,
                > 1 => EffectPalette.For(EffectLevel.Moderate),
                < -1 => EffectPalette.For(EffectLevel.Light),
                _ => EffectPalette.Landmark
            };
        }
    }
}
