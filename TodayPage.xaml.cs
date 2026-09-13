using System.Globalization;
using Microsoft.Maui.Graphics;
using MoleculeEfficienceTracker.Controls;
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
            ApplyAccent();
        }

        /// <summary>
        /// La carte de tête prend la teinte pleine du café, puisque c'est de lui
        /// qu'elle parle, et son texte passe au blanc.
        ///
        /// La version précédente diluait la teinte à un tiers d'opacité : sur un
        /// fond déjà pâle, il n'en restait qu'un voile beige. Une couleur qui doit
        /// dire où l'on se trouve ne se devine pas, elle se voit.
        /// </summary>
        private void ApplyAccent()
        {
            Color accent = EffectPalette.BrandFor(MoleculeKeys.Caffeine);
            Color on = EffectPalette.OnBrand;

            HeroCard.Background = EffectPalette.BrandGradient(accent);
            HeroBadge.Background = new SolidColorBrush(on.WithAlpha(0.22f));

            HeroTitleLabel.TextColor = on.WithAlpha(0.88f);
            HeroUpdatedLabel.TextColor = on.WithAlpha(0.72f);
            CaffeineHeadlineLabel.TextColor = on;
            CaffeineDetailLabel.TextColor = on.WithAlpha(0.90f);

            // Le bouton s'inverse : blanc sur la teinte plutôt que bleu par-dessus.
            // Le bleu primaire posé sur une carte orange en ferait la troisième
            // couleur d'une carte qui n'en veut qu'une.
            QuickAddButton.BackgroundColor = on;
            QuickAddButton.TextColor = EffectPalette.Mix(accent, Colors.Black, 0.22f);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // La teinte est calculée pour le thème courant : il a pu basculer
            // pendant que l'écran dormait en arrière-plan.
            ApplyAccent();
            Entrance.Play(RootStack);

            await RefreshAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Sans cela, revenir sur l'onglet pendant que la cascade court laisserait
            // une carte figée à mi-course, transparente et décalée vers le bas.
            Entrance.Reset(RootStack);
        }

        // Le retour d'appui : le bouton s'enfonce sous le doigt. Android le fait
        // nativement sur ses propres boutons ; un Button MAUI stylé ne le fait plus.
        private void OnQuickAddPressed(object? sender, EventArgs e)
            => _ = QuickAddButton.ScaleToAsync(0.96, 70, Easing.CubicOut);

        private void OnQuickAddReleased(object? sender, EventArgs e)
            => _ = QuickAddButton.ScaleToAsync(1.0, 110, Easing.CubicOut);

        private async Task RefreshAsync()
        {
            try
            {
                await RefreshCaffeineAnswerAsync();
                await RefreshWeekAsync();
                await RefreshLoadsAsync();
                await RefreshStatsAsync();
            }
            catch (Exception ex)
            {
                StatsEmptyLabel.Text = $"Statistiques indisponibles : {ex.Message}";
                StatsEmptyLabel.IsVisible = true;
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
                BloodValueLabel.Text = "—";
                BloodCaptionLabel.Text = string.Empty;
                TodayCountLabel.Text = "—";
                TodayCountCaptionLabel.Text = string.Empty;
                return;
            }

            CaffeineAdvisor.Advice advice = CaffeineAdvisor.Build(_caffeine, doses, now);

            CaffeineHeadlineLabel.Text = advice.Headline;
            CaffeineDetailLabel.Text = advice.Detail;
            HeroUpdatedLabel.Text = $"à {now:HH:mm}";

            UpdateCaffeineTiles(doses, now);

            QuickAddButton.IsVisible = true;
            QuickAddButton.Text = $"Enregistrer un café de {advice.PresetMg:0} mg";
            SemanticProperties.SetDescription(QuickAddButton,
                $"Enregistrer maintenant un café de {advice.PresetMg:0} milligrammes");
        }

        /// <summary>
        /// Les deux vignettes : ce qui circule à l'instant, et ce qui a été bu
        /// depuis minuit.
        ///
        /// Les deux chiffres se déduisaient jusqu'ici de la phrase de conseil, qui
        /// les mentionne au passage dans une subordonnée. Un chiffre cité dans une
        /// phrase se lit après la phrase ; un chiffre en vignette se lit avant.
        /// </summary>
        private void UpdateCaffeineTiles(List<DoseEntry> doses, DateTime now)
        {
            double concentration = _caffeine.CalculateTotalConcentration(doses, now);
            EffectLevel level = CurrentLoadService.LevelFor(MoleculeKeys.Caffeine, concentration);

            BloodValueLabel.Text = string.Format(CultureInfo.CurrentCulture, "{0:0.##}", concentration);
            BloodCaptionLabel.Text = $"mg/L · {EffectPalette.Label(level).ToLowerInvariant()}";

            DateTime midnight = now.Date;
            List<DoseEntry> today = doses
                .Where(d => d.TimeTaken >= midnight && d.TimeTaken <= now)
                .ToList();

            double milligrams = today.Sum(d => d.DoseMg);

            TodayCountLabel.Text = today.Count.ToString(CultureInfo.CurrentCulture);
            TodayCountCaptionLabel.Text = today.Count switch
            {
                0 => "aucun café",
                1 => string.Format(CultureInfo.CurrentCulture, "café · {0:0} mg", milligrams),
                _ => string.Format(CultureInfo.CurrentCulture, "cafés · {0:0} mg", milligrams)
            };
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

        // ===== 2. La semaine, en barres =====

        /// <summary>
        /// Sept barres, une par jour, la plus haute occupant toute la boîte.
        ///
        /// Une moyenne ment par construction : quatre cafés le lundi et zéro le
        /// dimanche donnent le même chiffre qu'une semaine régulière à deux. La
        /// forme de la semaine, elle, ne se résume pas — il faut la montrer.
        ///
        /// Les barres sont construites en code plutôt que liées : leur hauteur
        /// dépend du maximum de la série, qu'aucune liaison ne connaît, et sept
        /// éléments ne justifient pas une collection.
        /// </summary>
        private async Task RefreshWeekAsync()
        {
            const int Days = 7;

            List<DailyStats> history = await _stats.GetDailyStatsAsync(MoleculeKeys.Caffeine, Days);

            DateTime today = DateTime.Now.Date;
            var byDay = history.ToDictionary(d => d.Date.Date, d => d);

            var days = new List<(DateTime Day, double Dose, int Count)>();
            for (int i = Days - 1; i >= 0; i--)
            {
                DateTime day = today.AddDays(-i);
                days.Add(byDay.TryGetValue(day, out DailyStats? stat)
                    ? (day, stat.TotalDose, stat.Count)
                    : (day, 0, 0));
            }

            double total = days.Sum(d => d.Dose);
            int takes = days.Sum(d => d.Count);
            double peak = days.Count > 0 ? days.Max(d => d.Dose) : 0;

            WeekTotalLabel.Text = string.Format(CultureInfo.CurrentCulture, "{0:0}", total);
            WeekCaptionLabel.Text = takes switch
            {
                0 => "mg · aucune prise",
                1 => "mg · 1 prise",
                _ => string.Format(CultureInfo.CurrentCulture,
                                   "mg · {0} prises · {1:0} mg/j", takes, total / Days)
            };

            BuildWeekBars(days, peak);
        }

        private void BuildWeekBars(List<(DateTime Day, double Dose, int Count)> days, double peak)
        {
            WeekChart.Clear();
            WeekChart.ColumnDefinitions.Clear();
            WeekChart.RowDefinitions.Clear();

            WeekChart.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            WeekChart.RowDefinitions.Add(new RowDefinition(new GridLength(16)));

            Color accent = EffectPalette.BrandFor(MoleculeKeys.Caffeine);
            DateTime today = DateTime.Now.Date;

            for (int i = 0; i < days.Count; i++)
            {
                WeekChart.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

                (DateTime day, double dose, _) = days[i];
                bool isToday = day == today;

                // Un jour sans prise garde trois pixels : une colonne vide se
                // lirait comme une colonne manquante, et la semaine paraîtrait
                // trouée plutôt que creuse.
                double ratio = peak > 0 ? dose / peak : 0;
                double height = dose > 0 ? Math.Max(10, ratio * 74) : 3;

                var bar = new BoxView
                {
                    Color = isToday ? accent : accent.WithAlpha(0.42f),
                    CornerRadius = 5,
                    HeightRequest = height,
                    VerticalOptions = LayoutOptions.End,
                    HorizontalOptions = LayoutOptions.Fill
                };

                Grid.SetRow(bar, 0);
                Grid.SetColumn(bar, i);
                WeekChart.Add(bar);

                // L'initiale du jour, et non son numéro : sept numéros alignés se
                // lisent comme une suite, sept initiales comme une semaine.
                var label = new Label
                {
                    Text = day.ToString("ddd", CultureInfo.CurrentCulture)
                              .TrimEnd('.')[..1]
                              .ToUpperInvariant(),
                    FontSize = 11,
                    FontFamily = isToday ? "SansBold" : "Sans",
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = isToday
                        ? accent
                        : (Application.Current?.RequestedTheme == AppTheme.Dark
                            ? Color.FromArgb("#AFB7C4")
                            : Color.FromArgb("#5A6472"))
                };

                Grid.SetRow(label, 1);
                Grid.SetColumn(label, i);
                WeekChart.Add(label);
            }
        }

        // ===== 3. Ce qui circule encore =====

        private async Task RefreshLoadsAsync()
        {
            List<MoleculeLoad> loads = await _loads.GetPresentAsync(DateTime.Now);
            List<LoadRow> rows = loads.Select(l => new LoadRow(l)).ToList();

            LoadsCollection.ItemsSource = rows;
            LoadsCollection.IsVisible = rows.Count > 0;
            LoadsEmptyLabel.IsVisible = rows.Count == 0;
        }

        // ===== 4. Les moyennes =====

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
                StatsEmptyLabel.Text = $"Statistiques indisponibles : {ex.Message}";
                StatsEmptyLabel.IsVisible = true;
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
            StatsEmptyLabel.Text = "Aucune prise sur la période.";
            StatsEmptyLabel.IsVisible = rows.Count == 0;
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

            public Color Accent => EffectPalette.BrandFor(_load.Key);

            /// <summary>Le fond de la pastille. Une Border veut un pinceau, pas une
            /// couleur : la liaison échouerait en silence sur Background.</summary>
            public Brush AccentBrush => new SolidColorBrush(Accent);

            public string Glyph => EffectPalette.BrandGlyph(_load.Key);

            public Color LevelColor => EffectPalette.For(_load.Level);

            public string LevelDisplay => EffectPalette.Describe(_load.Level);

            public string ValueDisplay => string.Format(
                CultureInfo.CurrentCulture, "{0:0.##} {1}", _load.Concentration, _load.ConcentrationUnit);

            /// <summary>
            /// La part remplie de la barre, rapportée au seuil d'effet fort.
            ///
            /// Deux colonnes en étoile plutôt qu'une largeur en pixels : la ligne ne
            /// sait pas ce que mesure l'écran, et une barre calculée en dur y serait
            /// juste sur un téléphone et fausse sur tous les autres. Le plancher à
            /// 2 % laisse un trait visible pour une molécule tout juste perceptible ;
            /// sans lui, la barre disparaîtrait et la ligne semblerait cassée.
            /// </summary>
            public GridLength FilledWidth => new(Ratio, GridUnitType.Star);

            public GridLength EmptyWidth => new(1 - Ratio, GridUnitType.Star);

            private double Ratio
            {
                get
                {
                    double strong = CurrentLoadService.Thresholds(_load.Key).Strong;
                    if (strong <= 0) return 0.02;

                    return Math.Clamp(_load.Concentration / strong, 0.02, 1.0);
                }
            }

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

            public Color Accent => EffectPalette.BrandFor(_key);

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
