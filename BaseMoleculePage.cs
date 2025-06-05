using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using Syncfusion.Maui.Charts;
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using System.Text;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MoleculeEfficienceTracker
{
    public abstract partial class BaseMoleculePage<TCalculator> : ContentPage
    where TCalculator : IMoleculeCalculator, new()
    {
        protected readonly TCalculator Calculator;
        protected readonly DataPersistenceService PersistenceService;
        protected readonly IAlertService AlertService;
        protected readonly string MoleculeKey;

        public ObservableCollection<DoseEntry> Doses { get; set; }
        public ObservableCollection<ChartDataPoint> ChartData { get; set; }

        private DateTime? _lastDateTimeWithDayDisplayedOnXAxis = null;

        // Abstractions pour les contrôles UI (doivent être implémentées par les classes dérivées)
        protected abstract Entry DoseInputControl { get; }
        protected abstract DatePicker DatePickerControl { get; }
        protected abstract TimePicker TimePickerControl { get; }
        protected abstract Label ConcentrationOutputLabel { get; }
        protected abstract Label LastUpdateOutputLabel { get; }
        protected abstract SfCartesianChart ChartControl { get; }
        protected abstract Label EmptyStateIndicatorLabel { get; }
        protected abstract CollectionView DosesDisplayCollection { get; }

        // Abstractions pour les spécificités de la molécule
        protected abstract string DoseAnnotationIcon { get; } // Ex: "🍵", "💊", "🍾"
        protected abstract TimeSpan GraphDataStartOffset { get; } // Ex: TimeSpan.FromDays(-7)
        protected abstract TimeSpan GraphDataEndOffset { get; }   // Ex: TimeSpan.FromDays(3)
        protected abstract int GraphDataNumberOfPoints { get; } // Ex: 10 * 24 * 2
        protected abstract TimeSpan InitialVisibleStartOffset { get; } // Ex: TimeSpan.FromHours(-12)
        protected abstract TimeSpan InitialVisibleEndOffset { get; }   // Ex: TimeSpan.FromHours(24)
        public bool HasDoses => Doses != null && Doses.Count > 0;
        public bool IsDosesListEmpty => Doses == null || Doses.Count == 0; // Ou simplement !HasDoses



        protected BaseMoleculePage(string moleculeKey)
        {
            MoleculeKey = moleculeKey;
            Calculator = new TCalculator();
            PersistenceService = new DataPersistenceService(MoleculeKey);
            AlertService = new AlertService(); // Ou injectez si vous préférez

            Doses = new ObservableCollection<DoseEntry>();
            ChartData = new ObservableCollection<ChartDataPoint>();

            if (Doses is System.Collections.Specialized.INotifyCollectionChanged observableDoses)
            {
                observableDoses.CollectionChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(HasDoses));
                    OnPropertyChanged(nameof(IsDosesListEmpty));
                };
            }
            // Notifiez l'état initial
            OnPropertyChanged(nameof(HasDoses));
            OnPropertyChanged(nameof(IsDosesListEmpty));
        }

        /// <summary>
        /// Doit être appelé par la classe dérivée APRÈS InitializeComponent().
        /// </summary>
        protected void InitializePageUI()
        {
            BindingContext = this;
            DatePickerControl.Date = DateTime.Today;
            TimePickerControl.Time = DateTime.Now.TimeOfDay;
            StartConcentrationTimer();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDataAsyncInternal();
            UpdateEmptyState();
        }

        protected virtual async Task OnBeforeLoadDataAsync()
        {
            // Point d'accroche pour la logique de pré-chargement (ex: migration)
            await Task.CompletedTask;
        }

        private async Task LoadDataAsyncInternal()
        {
            await OnBeforeLoadDataAsync();

            List<DoseEntry> savedDoses = await PersistenceService.LoadDosesAsync();

            Doses.Clear();
            foreach (DoseEntry? dose in savedDoses.OrderByDescending(d => d.TimeTaken))
            {
                Doses.Add(dose);
            }

            UpdateAllDisplays();
        }

        private async void UpdateAllDisplays()
        {
            UpdateConcentrationDisplay();
            await UpdateChart(); // Rendre asynchrone si UpdateChart l'est
            UpdateDoseAnnotations();
            UpdateEmptyState();
        }


        protected async Task SaveDataAsync()
        {
            try
            {
                await PersistenceService.SaveDosesAsync(Doses.ToList());
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌", $"Erreur lors de la sauvegarde: {ex.Message}", "OK");
            }
        }

        protected async void OnAddDoseClicked(object sender, EventArgs e)
        {
            if (double.TryParse(DoseInputControl.Text, out double doseMg) && doseMg > 0)
            {
                DateTime selectedDate = DatePickerControl.Date;
                TimeSpan selectedTime = TimePickerControl.Time;
                DateTime dateTime = selectedDate.Add(selectedTime);

                DoseEntry dose = new DoseEntry(dateTime, doseMg);

                Doses.Insert(0, dose);
                DoseInputControl.Text = "";

                UpdateConcentrationDisplay();
                await UpdateChart();
                UpdateDoseAnnotations(); // Mettre à jour les annotations après l'ajout

                await SaveDataAsync();
                await AlertService.ShowAlertAsync("✅", $"Dose de {doseMg}mg ajoutée pour {dateTime:dd/MM HH:mm}");
            }
            else
            {
                await AlertService.ShowAlertAsync("❌", "Veuillez entrer une dose valide");
            }

            if (sender is Button btn) AnimateButton(btn);
            UpdateEmptyState();
        }

        protected async void OnDeleteDoseClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string doseId)
            {
                DoseEntry? dose = Doses.FirstOrDefault(d => d.Id == doseId);
                if (dose != null)
                {
                    bool confirm = await DisplayAlert("Supprimer",
                        $"Supprimer la dose de {dose.DoseMg}mg du {dose.TimeTaken:dd/MM HH:mm} ?",
                        "Oui", "Non");

                    if (confirm)
                    {
                        Doses.Remove(dose);
                        UpdateConcentrationDisplay();
                        await UpdateChart();
                        UpdateDoseAnnotations(); // Mettre à jour les annotations après la suppression
                        await SaveDataAsync();
                    }
                }
            }
            UpdateEmptyState();
        }

        protected void UpdateConcentrationDisplay()
        {
            DateTime currentTime = DateTime.Now;
            double concentration = Calculator.CalculateTotalConcentration(Doses.ToList(), currentTime);

            ConcentrationOutputLabel.Text = $"{concentration:F2} {Calculator.ConcentrationUnit}";
            LastUpdateOutputLabel.Text = $"Mise à jour: {currentTime:HH:mm:ss}";

            UpdateMoleculeSpecificConcentrationInfo(Doses.ToList(), currentTime);
        }

        /// <summary>
        /// Peut être surchargée pour afficher des informations de concentration spécifiques à la molécule.
        /// </summary>
        protected virtual void UpdateMoleculeSpecificConcentrationInfo(List<DoseEntry> doses, DateTime currentTime)
        {
            // Par défaut, ne fait rien. La page Caféine surchargera ceci.
        }

        protected async Task UpdateChart()
        {
            ChartData.Clear();
            var chart = ChartControl; // Utiliser la propriété abstraite

            if (chart == null) return;

            if (!Doses.Any())
            {
                DateTime nowForEmptyChart = DateTime.Now;
                if (chart.XAxes?.FirstOrDefault() is DateTimeAxis emptyXAxis)
                {
                    emptyXAxis.Minimum = nowForEmptyChart.AddDays(-7); // Valeur par défaut, pourrait être abstrait
                    emptyXAxis.Maximum = nowForEmptyChart.AddDays(7);  // Valeur par défaut
                    emptyXAxis.ZoomFactor = (24.0 / (14.0 * 24.0));
                    emptyXAxis.ZoomPosition = ((emptyXAxis.Maximum.Value - emptyXAxis.Minimum.Value).TotalHours * (1 - emptyXAxis.ZoomFactor)) > 0 ?
                                              (nowForEmptyChart.AddHours(-12) - emptyXAxis.Minimum.Value).TotalHours /
                                              ((emptyXAxis.Maximum.Value - emptyXAxis.Minimum.Value).TotalHours * (1 - emptyXAxis.ZoomFactor))
                                              : 0.5;
                    emptyXAxis.IntervalType = DateTimeIntervalType.Auto;
                    emptyXAxis.Interval = 3;
                    emptyXAxis.ZoomPosition = Math.Max(0.0, Math.Min(1.0, emptyXAxis.ZoomPosition));
                }
                return;
            }

            DateTime currentTime = DateTime.Now;
            DateTime graphDataStartTime = currentTime.Add(GraphDataStartOffset);
            DateTime graphDataEndTime = currentTime.Add(GraphDataEndOffset);
            int numberOfPoints = GraphDataNumberOfPoints;

            List<(DateTime Time, double Concentration)> graphPoints = Calculator.GenerateGraph(Doses.ToList(), graphDataStartTime, graphDataEndTime, numberOfPoints);

            foreach ((DateTime Time, double Concentration) point in graphPoints)
            {
                ChartData.Add(new ChartDataPoint(point.Time, point.Concentration));
            }

            if (chart.XAxes?.FirstOrDefault() is DateTimeAxis xAxis)
            {
                xAxis.Minimum = graphDataStartTime;
                xAxis.Maximum = graphDataEndTime;
                xAxis.IntervalType = DateTimeIntervalType.Auto;
                xAxis.Interval = 3;

                DateTime initialVisibleStartTime = currentTime.Add(InitialVisibleStartOffset);
                DateTime initialVisibleEndTime = currentTime.Add(InitialVisibleEndOffset);

                double totalAxisRangeInHours = (graphDataEndTime - graphDataStartTime).TotalHours;
                double desiredVisibleDurationInHours = (initialVisibleEndTime - initialVisibleStartTime).TotalHours;

                if (totalAxisRangeInHours > 0)
                {
                    xAxis.ZoomFactor = Math.Max(0.00001, Math.Min(1.0, desiredVisibleDurationInHours / totalAxisRangeInHours));
                    double desiredStartOffsetInHours = (initialVisibleStartTime - graphDataStartTime).TotalHours;
                    xAxis.ZoomPosition = desiredStartOffsetInHours / totalAxisRangeInHours;
                    xAxis.ZoomPosition = Math.Max(0.0, Math.Min(1.0 - xAxis.ZoomFactor, xAxis.ZoomPosition));
                }
                else
                {
                    xAxis.ZoomFactor = 1;
                    xAxis.ZoomPosition = 0;
                }
            }

            await chart.FadeTo(0.7, 80);
            await chart.FadeTo(1.0, 80);
        }

        /// <summary>
        /// Peut être surchargée pour ajouter des annotations spécifiques au graphique.
        /// </summary>
        protected virtual void AddMoleculeSpecificChartAnnotations()
        {
            // Par défaut, ne fait rien. La page Caféine surchargera ceci.
        }

        protected void UpdateDoseAnnotations()
        {
            var chart = ChartControl;
            if (chart == null) return;

            chart.Annotations.Clear();

            AddMoleculeSpecificChartAnnotations();

            DateTime now = DateTime.Now;
            VerticalLineAnnotation nowLine = new VerticalLineAnnotation
            {
                CoordinateUnit = ChartCoordinateUnit.Axis,
                X1 = now,
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeWidth = 1,
                Text = "Maintenant",
                LabelStyle = new ChartAnnotationLabelStyle
                {
                    TextColor = Colors.Red,
                    FontSize = 10,
                    HorizontalTextAlignment = ChartLabelAlignment.Start,
                    VerticalTextAlignment = ChartLabelAlignment.End,
                    Margin = new Thickness(10, 0, 0, 0)
                }
            };
            chart.Annotations.Add(nowLine);

            List<DoseEntry> currentDoses = Doses.ToList();
            foreach (DoseEntry dose in Doses)
            {
                double concentrationAtDoseTime = Calculator.CalculateTotalConcentration(currentDoses, dose.TimeTaken);
                double displayValue = Calculator.GetDoseDisplayValueInConcentrationUnit(dose);
                string doseText = $"{DoseAnnotationIcon}{displayValue:F2}{Calculator.DoseUnit}";

                TextAnnotation doseAnnotation = new TextAnnotation
                {
                    CoordinateUnit = ChartCoordinateUnit.Axis,
                    X1 = dose.TimeTaken,
                    Y1 = concentrationAtDoseTime,
                    Text = doseText,
                    LabelStyle = new ChartAnnotationLabelStyle
                    {
                        VerticalTextAlignment = ChartLabelAlignment.End,
                        HorizontalTextAlignment = ChartLabelAlignment.Center,
                        FontSize = 10,
                        TextColor = Colors.DarkSlateBlue,
                        Margin = new Thickness(0, 0, 0, 20)
                    }
                };
                chart.Annotations.Add(doseAnnotation);

                TextAnnotation timeAnnotation = new TextAnnotation
                {
                    CoordinateUnit = ChartCoordinateUnit.Axis,
                    X1 = dose.TimeTaken,
                    Y1 = concentrationAtDoseTime,
                    Text = $"🕐:{dose.TimeTaken:HH:mm}",
                    LabelStyle = new ChartAnnotationLabelStyle
                    {
                        VerticalTextAlignment = ChartLabelAlignment.End,
                        HorizontalTextAlignment = ChartLabelAlignment.Center,
                        FontSize = 10,
                        TextColor = Colors.DarkSlateBlue,
                        Margin = new Thickness(0, 0, 0, 2)
                    }
                };
                chart.Annotations.Add(timeAnnotation);
            }
        }


        protected void StartConcentrationTimer()
        {
            IDispatcherTimer timer = Application.Current.Dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMinutes(1); // Mettre à jour toutes les minutes
            timer.Tick += async (s, e) =>
            {
                UpdateConcentrationDisplay();
                // Le graphique n'a pas besoin d'être redessiné aussi fréquemment si seules les données de concentration changent.
                // Mais si la courbe doit refléter le temps qui passe, alors oui.
                // Pour l'instant, on le garde pour la cohérence avec le code original.
                await UpdateChart();
                UpdateDoseAnnotations(); // Les positions des annotations par rapport à la courbe peuvent changer
            };
            timer.Start();
        }

        protected async void OnExportDataClicked(object sender, EventArgs e)
        {
            try
            {
                List<DoseEntry> dosesToExport = Doses.ToList();
                if (!dosesToExport.Any())
                {
                    await DisplayAlert("📂", "Aucune donnée à exporter", "OK");
                    return;
                }

                string json = JsonSerializer.Serialize(dosesToExport, new JsonSerializerOptions { WriteIndented = true });
                string defaultFileName = $"{Calculator.DisplayName.ToLowerInvariant()}_export_{DateTime.Now:yyyyMMdd_HHmm}.json";

                using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                FileSaverResult fileSaverResult = await FileSaver.Default.SaveAsync(defaultFileName, stream, CancellationToken.None);

                if (fileSaverResult.IsSuccessful)
                {
                    await DisplayAlert("✅", $"Fichier sauvegardé." + (string.IsNullOrWhiteSpace(fileSaverResult.FilePath) ? "" : $"\nChemin: {fileSaverResult.FilePath}"), "OK");
                }
                else
                {
                    string errorMessage = "Sauvegarde annulée ou échouée.";
                    if (fileSaverResult.Exception != null)
                    {
                        errorMessage += $"\nErreur: {fileSaverResult.Exception.Message}";
                    }
                    await DisplayAlert("❌", errorMessage, "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌", $"Erreur d'export: {ex.Message}", "OK");
            }
        }

        protected async void OnClearAllDataClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("⚠️ Attention",
                "Supprimer toutes les données ?\nCette action est irréversible.",
                "Oui", "Annuler");

            if (confirm)
            {
                Doses.Clear();
                await PersistenceService.DeleteAllDataAsync();
                UpdateConcentrationDisplay();
                await UpdateChart();
                UpdateDoseAnnotations();
                UpdateEmptyState();
                await DisplayAlert("✅", "Toutes les données ont été supprimées", "OK");
            }
        }

        protected void ChartXAxis_LabelCreated(object sender, ChartAxisLabelEventArgs e)
        {
            DateTime currentLabelDateTime;

            if (!DateTime.TryParseExact(e.Label, "dd/MM HH:mm", null, System.Globalization.DateTimeStyles.None, out currentLabelDateTime) &&
                !DateTime.TryParseExact(e.Label, "HH:mm", null, System.Globalization.DateTimeStyles.None, out currentLabelDateTime) &&
                !DateTime.TryParse(e.Label, out currentLabelDateTime))
            {
                return;
            }

            if (_lastDateTimeWithDayDisplayedOnXAxis == null ||
                currentLabelDateTime.Date != _lastDateTimeWithDayDisplayedOnXAxis.Value.Date)
            {
                e.Label = currentLabelDateTime.ToString("dd/MM HH:mm");
                _lastDateTimeWithDayDisplayedOnXAxis = currentLabelDateTime;
            }
            else
            {
                e.Label = currentLabelDateTime.ToString("HH:mm");
            }
        }

        protected void UpdateEmptyState()
        {
            bool isEmpty = !Doses.Any();
            if (EmptyStateIndicatorLabel != null)
                EmptyStateIndicatorLabel.IsVisible = isEmpty;
            if (DosesDisplayCollection != null)
                DosesDisplayCollection.IsVisible = !isEmpty;
        }

        protected async void AnimateButton(Button btn)
        {
            await btn.ScaleTo(1.1, 80, Easing.CubicOut);
            await btn.ScaleTo(1.0, 80, Easing.CubicIn);
        }
    }
}
