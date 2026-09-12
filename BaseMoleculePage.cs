using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Controls.Shapes;
using MoleculeEfficienceTracker.Controls;
using MoleculeEfficienceTracker.Core.Design;
using MoleculeEfficienceTracker.Core.Extensions;
using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using Syncfusion.Maui.Charts;

namespace MoleculeEfficienceTracker
{
    public abstract class BaseMoleculePage<TCalculator> : ContentPage
        where TCalculator : IMoleculeCalculator, new()
    {
        protected readonly TCalculator Calculator;
        protected readonly DataPersistenceService PersistenceService;
        protected readonly IAlertService AlertService;
        protected readonly string MoleculeKey;

        public ObservableCollection<DoseEntry> Doses { get; }
        public ObservableRangeCollection<ChartDataPoint> ChartData { get; }

        private IDispatcherTimer? _timer;
        private DateTime? _lastDayLabelled;
        private bool _wired;

        /// <summary>Le corps partagé de la page, déclaré dans le XAML de la page.</summary>
        protected abstract MoleculePanelView Panel { get; }

        protected abstract string DoseAnnotationIcon { get; }
        protected abstract TimeSpan GraphDataStartOffset { get; }
        protected abstract TimeSpan GraphDataEndOffset { get; }
        protected abstract int GraphDataNumberOfPoints { get; }
        protected abstract TimeSpan InitialVisibleStartOffset { get; }
        protected abstract TimeSpan InitialVisibleEndOffset { get; }

        /// <summary>Doses proposées en accès direct. Vide : aucun bouton rapide.</summary>
        protected virtual IReadOnlyList<double> Presets => Array.Empty<double>();

        /// <summary>Seuils à tracer, du plus fort au plus faible.</summary>
        protected virtual IReadOnlyList<(double Value, string Label, EffectLevel Level)> Thresholds
            => Array.Empty<(double, string, EffectLevel)>();

        protected virtual bool UseConcentrationUnitForDoseAnnotation => false;

        /// <summary>Intitulé de la carte de saisie.</summary>
        protected virtual string AddSectionTitle => $"Ajouter — {Calculator.DisplayName}";

        protected virtual string DoseFieldCaption => $"Dose ({Calculator.DoseUnit})";

        protected virtual string HelperText => string.Empty;

        public bool HasDoses => Doses.Count > 0;

        protected BaseMoleculePage(string moleculeKey)
        {
            MoleculeKey = MoleculeKeys.Normalize(moleculeKey);
            Calculator = new TCalculator();
            PersistenceService = new DataPersistenceService(MoleculeKey);
            AlertService = ServiceLocator.GetOptional<IAlertService>() ?? new AlertService();

            Doses = new ObservableCollection<DoseEntry>();
            ChartData = new ObservableRangeCollection<ChartDataPoint>();
        }

        /// <summary>Appelé par la page dérivée après InitializeComponent().</summary>
        protected void InitializePageUI()
        {
            BindingContext = this;

            Panel.SectionTitle = AddSectionTitle;
            Panel.DoseFieldCaption = DoseFieldCaption;
            Panel.HelperText = HelperText;
            Panel.SetPresets(Presets, Calculator.DoseUnit);
            Panel.Series.Fill = new SolidColorBrush(EffectPalette.Series);
            Panel.YAxis.Title = new ChartAxisTitle { Text = Calculator.ConcentrationUnit };

            if (Thresholds.Count > 0)
                Panel.SetThresholdLegend(Thresholds.Select(t => (t.Label, t.Level)));

            if (!_wired)
            {
                Panel.AddDoseButton.Clicked += OnAddDoseClicked;
                Panel.ExportDataButton.Clicked += OnExportDataClicked;
                Panel.ClearDataButton.Clicked += OnClearAllDataClicked;
                Panel.DoseDeleteRequested += OnDoseDeleteRequested;
                Panel.PresetSelected += OnPresetSelected;
                Panel.XAxis.LabelCreated += ChartXAxis_LabelCreated;
                _wired = true;
            }

            ResetPickersToNow();
        }

        /// <summary>
        /// Date et heure proposées, remises à l'instant courant.
        ///
        /// Elles n'étaient posées que dans le constructeur ; comme les six pages
        /// étaient construites au lancement, le sélecteur proposait l'heure du
        /// démarrage de l'application. Une application laissée en arrière-plan une
        /// demi-journée suggérait une heure vieille de plusieurs heures, sans le dire.
        /// </summary>
        private void ResetPickersToNow()
        {
            DateTime now = DateTime.Now;
            Panel.DatePickerControl.Date = now.Date;
            Panel.TimePickerControl.Time = now.TimeOfDay;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            ResetPickersToNow();
            StartConcentrationTimer();

            try
            {
                await LoadDataAsyncInternal();
                await UpdateAllDisplays();
            }
            catch (Exception ex)
            {
                await AlertService.ShowAlertAsync("Chargement impossible", ex.Message);
            }
        }

        /// <summary>
        /// Le minuteur s'arrête avec la page.
        ///
        /// Il n'était jamais arrêté : les six pages étant construites au lancement,
        /// six minuteurs tournaient en permanence, y compris sur les onglets jamais
        /// ouverts, chacun régénérant des centaines de points et une centaine
        /// d'annotations toutes les cinq minutes.
        /// </summary>
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopConcentrationTimer();
        }

        protected virtual Task OnBeforeLoadDataAsync() => Task.CompletedTask;

        // ----- Persistance remplaçable -----
        //
        // L'écran anti-douleur couvre deux molécules et doit les répartir dans deux
        // fichiers, faute de quoi la page de synthèse ne les verrait pas. Il
        // redéfinit ces quatre méthodes ; toutes les autres pages s'en remettent au
        // service standard.

        protected virtual Task<List<DoseEntry>> ReadDosesAsync() => PersistenceService.LoadDosesAsync();

        protected virtual Task WriteDosesAsync(List<DoseEntry> doses) => PersistenceService.SaveDosesAsync(doses);

        protected virtual Task RemoveAllAsync() => PersistenceService.DeleteAllDataAsync();

        protected virtual Task<string?> BackupStoreAsync(string suffix) => PersistenceService.BackupAsync(suffix);

        private async Task LoadDataAsyncInternal()
        {
            await DataMigrationService.EnsureMigratedAsync();
            await OnBeforeLoadDataAsync();

            List<DoseEntry> saved;
            try
            {
                saved = await ReadDosesAsync();
            }
            catch (DoseDataCorruptedException ex)
            {
                // Surtout ne rien écrire par-dessus : l'ancienne version rendait une
                // liste vide, que la saisie suivante enregistrait à la place de tout
                // l'historique.
                await AlertService.ShowAlertAsync(
                    "Fichier de données illisible",
                    $"{Calculator.DisplayName} : le fichier n'a pas pu être relu. Une copie a été mise de côté " +
                    $"({System.IO.Path.GetFileName(ex.QuarantinePath)}). Rien n'a été effacé ; évitez de saisir une " +
                    "nouvelle prise avant de l'avoir récupéré.");
                return;
            }

            Doses.Clear();
            foreach (DoseEntry dose in saved.OrderByDescending(d => d.TimeTaken))
                Doses.Add(dose);
        }

        protected virtual async Task UpdateAllDisplays()
        {
            UpdateConcentrationDisplay();
            await UpdateChart();
            await UpdateDoseAnnotations();
            UpdateEmptyState();
        }

        protected async Task SaveDataAsync()
        {
            try
            {
                await WriteDosesAsync(Doses.ToList());
            }
            catch (Exception ex)
            {
                // L'ancienne version avalait l'exception dans le service : cette
                // alerte ne pouvait jamais s'afficher, et un disque plein passait
                // inaperçu.
                await AlertService.ShowAlertAsync("Enregistrement impossible", ex.Message);
            }
        }

        // ===== Saisie =====

        private async void OnPresetSelected(object? sender, double amount)
        {
            await AddDoseAsync(amount, DateTime.Now);
            ResetPickersToNow();
        }

        protected async void OnAddDoseClicked(object? sender, EventArgs e)
        {
            (bool ok, double amount, string? error) = ReadDoseInput();

            if (!ok)
            {
                await AlertService.ShowAlertAsync("Saisie incomplète", error ?? "Vérifiez la quantité.");
                return;
            }

            // MAUI 10 rend ces deux sélecteurs nullables.
            DateTime day = Panel.DatePickerControl.Date ?? DateTime.Today;
            TimeSpan time = Panel.TimePickerControl.Time ?? DateTime.Now.TimeOfDay;
            DateTime when = day.Add(time);
            await AddDoseAsync(amount, when);

            if (sender is Button btn) _ = AnimateButtonAsync(btn);
        }

        /// <summary>
        /// Lit la quantité saisie. La culture est explicitement celle de l'appareil :
        /// le pavé numérique français produit une virgule, et l'ancien TryParse sans
        /// culture échouait silencieusement selon la plateforme.
        /// </summary>
        protected virtual (bool Ok, double Amount, string? Error) ReadDoseInput()
        {
            string raw = (Panel.DoseInput.Text ?? string.Empty).Trim().Replace(',', '.');

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount))
                return (false, 0, "Entrez une quantité valide.");

            if (amount <= 0)
                return (false, 0, "La quantité doit être supérieure à zéro.");

            if (amount > MaxPlausibleDose)
                return (false, 0, $"Quantité invraisemblable (plus de {MaxPlausibleDose:0} {Calculator.DoseUnit}).");

            return (true, amount, null);
        }

        /// <summary>Borne haute de vraisemblance. Rien ne bornait la saisie auparavant.</summary>
        protected virtual double MaxPlausibleDose => 2000;

        protected virtual DoseEntry BuildDose(double amount, DateTime when)
            => new(when, amount, UserPreferences.GetWeightKg(), MoleculeKey);

        protected async Task AddDoseAsync(double amount, DateTime when)
        {
            if (when > DateTime.Now.AddMinutes(5))
            {
                bool confirm = await AlertService.ShowConfirmAsync(
                    "Prise dans le futur",
                    $"L'heure indiquée ({when:dd/MM HH:mm}) est à venir. L'enregistrer quand même ?");

                if (!confirm) return;
            }

            DoseEntry dose = BuildDose(amount, when);

            Doses.Insert(0, dose);
            SortDoses();
            Panel.DoseInput.Text = string.Empty;

            await SaveDataAsync();
            await OnAfterDoseChangedAsync();
            await UpdateAllDisplays();
        }

        private void SortDoses()
        {
            List<DoseEntry> ordered = Doses.OrderByDescending(d => d.TimeTaken).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                int current = Doses.IndexOf(ordered[i]);
                if (current != i) Doses.Move(current, i);
            }
        }

        /// <summary>Point d'accroche après toute modification (notification, cache…).</summary>
        protected virtual Task OnAfterDoseChangedAsync() => Task.CompletedTask;

        private async void OnDoseDeleteRequested(object? sender, string doseId)
        {
            DoseEntry? dose = Doses.FirstOrDefault(d => d.Id == doseId);
            if (dose is null) return;

            bool confirm = await AlertService.ShowConfirmAsync(
                "Supprimer",
                $"Supprimer {dose.AmountDisplay} du {dose.TimeTaken:dd/MM à HH:mm} ?");

            if (!confirm) return;

            Doses.Remove(dose);
            await SaveDataAsync();
            await OnAfterDoseChangedAsync();
            await UpdateAllDisplays();
        }

        // ===== Affichage =====

        protected void UpdateConcentrationDisplay()
        {
            DateTime now = DateTime.Now;
            List<DoseEntry> doses = Doses.ToList();

            double concentration = Calculator.CalculateTotalConcentration(doses, now);
            double amount = Calculator.CalculateTotalAmount(doses, now);

            Panel.ConcentrationOutput.Text =
                $"{amount:0.##} {Calculator.DoseUnit} · {concentration:0.###} {Calculator.ConcentrationUnit}";
            Panel.LastUpdateOutput.Text = $"Calculé à {now:HH:mm}";

            UpdateMoleculeSpecificConcentrationInfo(doses, now, concentration);
        }

        /// <summary>
        /// Libellé d'état. Redéfinissable, mais la polarité est imposée ici : le
        /// rouge dit toujours « fort », jamais « rien ». L'écran caféine peignait
        /// « effet fort ou toxique » en vert et « effet négligeable » en rouge,
        /// l'inverse exact des écrans voisins.
        /// </summary>
        protected virtual void UpdateMoleculeSpecificConcentrationInfo(
            List<DoseEntry> doses, DateTime currentTime, double concentration)
        {
            EffectLevel level = ResolveEffectLevel(concentration);

            Panel.EffectStatus.Text = EffectPalette.Describe(level);
            Panel.EffectStatus.TextColor = EffectPalette.For(level);
            Panel.EffectStatus.IsVisible = true;
        }

        protected virtual EffectLevel ResolveEffectLevel(double concentration)
        {
            foreach ((double value, _, EffectLevel level) in Thresholds)
                if (concentration >= value) return level;

            return EffectLevel.None;
        }

        protected virtual double? GetEffectPercentForConcentration(double concentration) => null;

        protected virtual async Task UpdateChart()
        {
            SfCartesianChart chart = Panel.Chart;
            DateTime now = DateTime.Now;

            DateTime from = now.Add(GraphDataStartOffset);
            DateTime to = now.Add(GraphDataEndOffset);

            if (Doses.Count == 0)
            {
                ChartData.Clear();
                Panel.XAxis.Minimum = from;
                Panel.XAxis.Maximum = to;
                return;
            }

            List<DoseEntry> copy = Doses.ToList();
            int points = GraphDataNumberOfPoints;

            List<ChartDataPoint> data = await Task.Run(() =>
            {
                var generated = Calculator.GenerateGraph(copy, from, to, points);
                var list = new List<ChartDataPoint>(generated.Count);

                foreach ((DateTime time, double concentration) in generated)
                    list.Add(new ChartDataPoint(time, concentration,
                        GetEffectPercentForConcentration(concentration) ?? 0));

                return list;
            });

            ChartData.ReplaceRange(data);

            Panel.XAxis.Minimum = from;
            Panel.XAxis.Maximum = to;
            Panel.XAxis.IntervalType = DateTimeIntervalType.Auto;

            // Le cadrage initial n'est posé qu'une fois. Il était recalculé à chaque
            // rafraîchissement, ce qui effaçait toutes les cinq minutes le zoom que
            // l'utilisateur venait de régler.
            if (!_initialZoomApplied)
            {
                ApplyInitialZoom(from, to, now);
                _initialZoomApplied = true;
            }
        }

        private bool _initialZoomApplied;

        private void ApplyInitialZoom(DateTime from, DateTime to, DateTime now)
        {
            double totalHours = (to - from).TotalHours;
            if (totalHours <= 0) return;

            DateTime visibleStart = now.Add(InitialVisibleStartOffset);
            double visibleHours = (now.Add(InitialVisibleEndOffset) - visibleStart).TotalHours;

            double factor = Math.Clamp(visibleHours / totalHours, 0.00001, 1.0);
            double position = Math.Clamp((visibleStart - from).TotalHours / totalHours, 0.0, 1.0 - factor);

            Panel.XAxis.ZoomFactor = factor;
            Panel.XAxis.ZoomPosition = position;
        }

        /// <summary>
        /// Seuils et repères sur le graphique.
        ///
        /// La méthode d'annotation de seuil était recopiée à l'identique dans les
        /// quatre pages, caractère pour caractère. Elle vit ici, une fois.
        /// </summary>
        protected async Task UpdateDoseAnnotations()
        {
            SfCartesianChart chart = Panel.Chart;
            chart.Annotations.Clear();

            foreach ((double value, string label, EffectLevel level) in Thresholds)
                chart.Annotations.Add(BuildThresholdAnnotation(value, label, level));

            DateTime now = DateTime.Now;
            chart.Annotations.Add(new VerticalLineAnnotation
            {
                CoordinateUnit = ChartCoordinateUnit.Axis,
                X1 = now,
                // Neutre. Elle était rouge, sur le même graphique que le seuil de
                // toxicité rouge : le repère et le danger partageaient leur couleur.
                Stroke = new SolidColorBrush(EffectPalette.Landmark),
                StrokeWidth = 1.5,
                StrokeDashArray = Dashes(new double[] { 4, 3 }),
                Text = "Maintenant",
                LabelStyle = new ChartAnnotationLabelStyle
                {
                    TextColor = EffectPalette.Landmark,
                    FontSize = 11,
                    HorizontalTextAlignment = ChartLabelAlignment.Start,
                    VerticalTextAlignment = ChartLabelAlignment.End,
                    Margin = new Thickness(8, 0, 0, 0)
                }
            });

            await AddDoseMarkersAsync(chart);
        }

        private async Task AddDoseMarkersAsync(SfCartesianChart chart)
        {
            DateTime? min = Panel.XAxis.Minimum;
            DateTime? max = Panel.XAxis.Maximum;

            List<DoseEntry> visible = Doses
                .Where(d => (!min.HasValue || d.TimeTaken >= min.Value) &&
                            (!max.HasValue || d.TimeTaken <= max.Value))
                .OrderByDescending(d => d.TimeTaken)
                .Take(30)
                .ToList();

            if (visible.Count == 0) return;

            List<DoseEntry> all = Doses.ToList();

            // Les concentrations sont calculées hors du fil d'interface : la boucle
            // était quadratique et s'exécutait sur lui, toutes les cinq minutes.
            double[] heights = await Task.Run(() =>
                visible.Select(d => Calculator.CalculateTotalConcentration(all, d.TimeTaken)).ToArray());

            for (int i = 0; i < visible.Count; i++)
            {
                DoseEntry dose = visible[i];
                double display = Calculator.GetDoseDisplayValueInConcentrationUnit(dose);
                string unit = UseConcentrationUnitForDoseAnnotation
                    ? Calculator.ConcentrationUnit
                    : Calculator.DoseUnit;

                chart.Annotations.Add(new TextAnnotation
                {
                    CoordinateUnit = ChartCoordinateUnit.Axis,
                    X1 = dose.TimeTaken,
                    Y1 = heights[i],
                    Text = $"{DoseAnnotationIcon} {display:0.##}{unit}",
                    LabelStyle = new ChartAnnotationLabelStyle
                    {
                        VerticalTextAlignment = ChartLabelAlignment.End,
                        HorizontalTextAlignment = ChartLabelAlignment.Center,
                        FontSize = 11,
                        TextColor = EffectPalette.Series,
                        Margin = new Thickness(0, 0, 0, 8)
                    }
                });
            }
        }


        /// <summary>
        /// Construit un motif de trait sans dépendre d'un constructeur par
        /// collection, dont la présence varie selon les versions de MAUI.
        /// </summary>
        private static DoubleCollection Dashes(double[] pattern)
        {
            var collection = new DoubleCollection();
            foreach (double value in pattern) collection.Add(value);
            return collection;
        }

        private static HorizontalLineAnnotation BuildThresholdAnnotation(double value, string label, EffectLevel level)
        {
            var annotation = new HorizontalLineAnnotation
            {
                Y1 = value,
                Stroke = new SolidColorBrush(EffectPalette.For(level)),
                StrokeWidth = 2,
                Text = label,
                LabelStyle = new ChartAnnotationLabelStyle
                {
                    FontSize = 11,
                    TextColor = EffectPalette.For(level),
                    CornerRadius = 3,
                    HorizontalTextAlignment = ChartLabelAlignment.Start,
                    VerticalTextAlignment = ChartLabelAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                }
            };

            // Le motif de trait double la couleur : les quatre seuils se
            // distinguaient jusqu'ici par la seule teinte, et sur deux pages
            // c'étaient orange, jaune-vert et vert.
            double[] dash = EffectPalette.DashPattern(level);
            if (dash.Length > 0)
                annotation.StrokeDashArray = Dashes(dash);

            return annotation;
        }

        // ===== Minuteur =====

        protected void StartConcentrationTimer()
        {
            if (_timer is not null) return;

            IDispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null) return;

            _timer = dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromMinutes(1);
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        protected void StopConcentrationTimer()
        {
            if (_timer is null) return;

            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }

        private int _ticksSinceChartRefresh;

        private async void OnTimerTick(object? sender, EventArgs e)
        {
            // Seule la valeur courante bouge d'une minute à l'autre ; redessiner la
            // courbe et cent annotations à chaque battement ne servait à rien.
            UpdateConcentrationDisplay();

            // Le repère « Maintenant » finirait tout de même par dériver sur un
            // onglet laissé ouvert : la courbe se rafraîchit au quart d'heure.
            if (++_ticksSinceChartRefresh < 15) return;

            _ticksSinceChartRefresh = 0;

            try
            {
                await UpdateChart();
                await UpdateDoseAnnotations();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Graphique] rafraîchissement : {ex.Message}");
            }
        }

        // ===== Données =====

        protected async void OnExportDataClicked(object? sender, EventArgs e)
        {
            try
            {
                List<DoseEntry> toExport = Doses.ToList();
                if (toExport.Count == 0)
                {
                    await AlertService.ShowAlertAsync("Export", "Aucune donnée à exporter.");
                    return;
                }

                // Même convention de nommage que le stockage : l'export sortait en
                // PascalCase quand les fichiers étaient en camelCase, si bien qu'un
                // fichier exporté n'était pas relisible par l'application.
                string json = JsonSerializer.Serialize(toExport, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                string fileName = $"{MoleculeKey}_export_{DateTime.Now:yyyyMMdd_HHmm}.json";

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                FileSaverResult result = await FileSaver.Default.SaveAsync(fileName, stream, CancellationToken.None);

                await AlertService.ShowAlertAsync(
                    result.IsSuccessful ? "Export terminé" : "Export interrompu",
                    result.IsSuccessful
                        ? $"{toExport.Count} prises enregistrées."
                        : result.Exception?.Message ?? "Sauvegarde annulée.");
            }
            catch (Exception ex)
            {
                await AlertService.ShowAlertAsync("Export impossible", ex.Message);
            }
        }

        protected async void OnClearAllDataClicked(object? sender, EventArgs e)
        {
            bool confirm = await AlertService.ShowConfirmAsync(
                "Tout effacer",
                $"Supprimer les {Doses.Count} prises de {Calculator.DisplayName} ?\nCette action est irréversible.",
                "Effacer", "Annuler");

            if (!confirm) return;

            await BackupStoreAsync("avant-effacement");
            Doses.Clear();
            await RemoveAllAsync();
            await UpdateAllDisplays();

            await AlertService.ShowAlertAsync("Effacé", "Une copie de sauvegarde a été conservée sur l'appareil.");
        }

        // ===== Divers =====

        protected void ChartXAxis_LabelCreated(object? sender, ChartAxisLabelEventArgs e)
        {
            if (!DateTime.TryParse(e.Label, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime labelDate) &&
                !DateTime.TryParseExact(e.Label, "dd/MM HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out labelDate) &&
                !DateTime.TryParseExact(e.Label, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out labelDate))
                return;

            bool newDay = _lastDayLabelled is null || labelDate.Date != _lastDayLabelled.Value.Date;
            e.Label = newDay ? labelDate.ToString("dd/MM HH:mm") : labelDate.ToString("HH:mm");
            _lastDayLabelled = labelDate;
        }

        protected void UpdateEmptyState()
        {
            bool empty = Doses.Count == 0;
            Panel.EmptyIndicator.IsVisible = empty;
            Panel.DosesView.IsVisible = !empty;
            OnPropertyChanged(nameof(HasDoses));
        }

        private static async Task AnimateButtonAsync(Button btn)
        {
            await btn.ScaleToAsync(1.06, 70, Easing.CubicOut);
            await btn.ScaleToAsync(1.0, 70, Easing.CubicIn);
        }
    }
}
