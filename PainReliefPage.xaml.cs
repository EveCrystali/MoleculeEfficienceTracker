using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using MoleculeEfficienceTracker.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Syncfusion.Maui.Charts;
using Microsoft.Maui.Graphics;


namespace MoleculeEfficienceTracker
{
    public partial class PainReliefPage : BaseMoleculePage<CombinedPainReliefCalculator>
        private readonly DataPersistenceService _paraService = new("paracetamol");
        private readonly DataPersistenceService _ibuService = new("ibuprofene");

            // Charger toutes les données existantes
            var own = await PersistenceService.LoadDosesAsync();
            var para = await _paraService.LoadDosesAsync();
            var ibu = await _ibuService.LoadDosesAsync();
            foreach (var d in own)
            {
                if (string.IsNullOrEmpty(d.MoleculeKey)) d.MoleculeKey = "pain_relief";
            }
            var merged = own
                .Concat(para)
                .Concat(ibu)
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderByDescending(d => d.TimeTaken)
                .ToList();

            await PersistenceService.SaveDosesAsync(merged);
            await _paraService.SaveDosesAsync(merged.Where(d => d.MoleculeKey.Equals("paracetamol", StringComparison.OrdinalIgnoreCase)).ToList());
            await _ibuService.SaveDosesAsync(merged.Where(d => d.MoleculeKey.Equals("ibuprofen", StringComparison.OrdinalIgnoreCase) || d.MoleculeKey.Equals("ibuprofene", StringComparison.OrdinalIgnoreCase)).ToList());
                await SaveAllDataAsync();

        protected override string DoseAnnotationIcon => "💊";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-7);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(3);
        protected override int GraphDataNumberOfPoints => 10 * 24 * 2;
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-12);
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(12);

        public ObservableCollection<DoseEntry> ParacetamolDoses { get; } = new();
        public ObservableCollection<DoseEntry> IbuprofenDoses { get; } = new();
        public ObservableRangeCollection<ChartDataPoint> ParacetamolChartData { get; } = new();
        public ObservableRangeCollection<ChartDataPoint> IbuprofenChartData { get; } = new();
        public ObservableRangeCollection<ChartDataPoint> TotalChartData { get; } = new();

        public PainReliefPage() : base("pain_relief")
        {
            InitializeComponent();
            base.InitializePageUI();
            Doses.CollectionChanged += (s, e) => RefreshDoseGroups();
            RefreshDoseGroups();
        }

        protected override async Task OnBeforeLoadDataAsync()
        {
            await base.OnBeforeLoadDataAsync();

            // Charger les données existantes des pages individuelles
            DataPersistenceService paraService = new DataPersistenceService("paracetamol");
            DataPersistenceService ibuService = new DataPersistenceService("ibuprofene");

            var para = await paraService.LoadDosesAsync();
            foreach (var d in para)
            {
                if (string.IsNullOrEmpty(d.MoleculeKey)) d.MoleculeKey = "paracetamol";
                if (!Doses.Any(x => x.Id == d.Id)) Doses.Add(d);
            }

            var ibu = await ibuService.LoadDosesAsync();
            foreach (var d in ibu)
            {
                if (string.IsNullOrEmpty(d.MoleculeKey)) d.MoleculeKey = "ibuprofene";
                if (!Doses.Any(x => x.Id == d.Id)) Doses.Add(d);
            }

            // Trier par date décroissante
            var ordered = Doses.OrderByDescending(d => d.TimeTaken).ToList();
            Doses.Clear();
            foreach (var d in ordered) Doses.Add(d);
        }

        private void RefreshDoseGroups()
        {
            ParacetamolDoses.Clear();
            IbuprofenDoses.Clear();
            foreach (var d in Doses)
            {
                if (d.MoleculeKey.Equals("paracetamol", StringComparison.OrdinalIgnoreCase))
                    ParacetamolDoses.Add(d);
                else if (d.MoleculeKey.Equals("ibuprofen", StringComparison.OrdinalIgnoreCase) ||
                         d.MoleculeKey.Equals("ibuprofene", StringComparison.OrdinalIgnoreCase))
                    IbuprofenDoses.Add(d);
            }
        }

        private async void OnAddPainDoseClicked(object sender, EventArgs e)
        {
            if (double.TryParse(DoseEntry.Text, out double doseMg) && doseMg > 0 && MoleculePicker.SelectedItem is string molecule)
            {
                DateTime selectedDate = DatePicker.Date;
                DateTime dateTime = selectedDate.Add(TimePicker.Time);
                double weight = UserPreferences.GetWeightKg();
                DoseEntry dose = new DoseEntry(dateTime, doseMg, weight, molecule);
                Doses.Insert(0, dose);
                DoseEntry.Text = string.Empty;
                RefreshDoseGroups();

                UpdateConcentrationDisplay();
                await UpdateChart();
                UpdateDoseAnnotations();
                await SaveDataAsync();
                await AlertService.ShowAlertAsync("✅", $"Dose {doseMg}mg {molecule} ajoutée pour {dateTime:dd/MM HH:mm}");
            }
            else
            {
                await AlertService.ShowAlertAsync("❌", "Veuillez entrer une dose valide");
            }

            if (sender is Button btn) AnimateButton(btn);
            UpdateEmptyState();
        }

        protected override void UpdateMoleculeSpecificConcentrationInfo(List<DoseEntry> doses, DateTime currentTime)
        {
            double effect = Calculator.CalculateTotalConcentration(doses, currentTime);
            ConcentrationOutputLabel.Text = $"Saturation : {effect:F0} %";

            EffectLevel level = Calculator.GetCombinedEffectLevel(doses, currentTime);
            if (EffectStatusLabel != null)
            {
                string text = level switch
                {
                    EffectLevel.Strong => "Effet fort",
                    EffectLevel.Moderate => "Effet net",
                    EffectLevel.Light => "Effet léger",
                    _ => "Négligeable"
                };
                Color color = level switch
                {
                    EffectLevel.Strong => Colors.Red,
                    EffectLevel.Moderate => Colors.Green,
                    EffectLevel.Light => Colors.Orange,
                    _ => Colors.Gray
                };
                EffectStatusLabel.Text = text;
                EffectStatusLabel.TextColor = color;
                EffectStatusLabel.IsVisible = true;
            }

            DateTime? endTime = Calculator.PredictEffectEndTime(doses, currentTime);
            if (EffectEndPredictionLabel != null)
            {
                if (endTime.HasValue && endTime.Value > currentTime)
                {
                    var remaining = endTime.Value - currentTime;
                    EffectEndPredictionLabel.Text = $"Effet négligeable dans {remaining.TotalHours:F1} heures";
                }
                else
                {
                    EffectEndPredictionLabel.Text = "Effet actuellement négligeable";
                }
                EffectEndPredictionLabel.IsVisible = true;
            }
        }

        private async void OnDeleteDoseClickedCustom(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string doseId)
            {
                DoseEntry? dose = Doses.FirstOrDefault(d => d.Id == doseId);
                if (dose != null)
                {
                    bool confirm = await DisplayAlert("Supprimer",
                        $"Supprimer la dose de {dose.DoseMg}mg ({dose.MoleculeKey}) du {dose.TimeTaken:dd/MM HH:mm} ?",
                        "Oui", "Non");

                    if (confirm)
                    {
                        Doses.Remove(dose);
                        RefreshDoseGroups();
                        UpdateConcentrationDisplay();
                        await UpdateChart();
                        UpdateDoseAnnotations();
                        await SaveAllDataAsync();
                    }
                }
            }
            UpdateEmptyState();
        }

        private async Task SaveAllDataAsync()
        {
            await PersistenceService.SaveDosesAsync(Doses.ToList());
            await _paraService.SaveDosesAsync(Doses.Where(d => d.MoleculeKey.Equals("paracetamol", StringComparison.OrdinalIgnoreCase)).ToList());
            await _ibuService.SaveDosesAsync(Doses.Where(d => d.MoleculeKey.Equals("ibuprofen", StringComparison.OrdinalIgnoreCase) || d.MoleculeKey.Equals("ibuprofene", StringComparison.OrdinalIgnoreCase)).ToList());
        }

        private async void OnClearAllDataClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("⚠️ Attention",
                "Supprimer toutes les données ?\nCette action est irréversible.",
                "Oui", "Annuler");

            if (confirm)
            {
                Doses.Clear();
                await PersistenceService.DeleteAllDataAsync();
                await _paraService.DeleteAllDataAsync();
                await _ibuService.DeleteAllDataAsync();
                UpdateConcentrationDisplay();
                await UpdateChart();
                UpdateDoseAnnotations();
                UpdateEmptyState();
                await DisplayAlert("✅", "Toutes les données ont été supprimées", "OK");
            }
        }

        private void AddThresholdAnnotation(double yValue, string text, Color color)
        {
            var annotation = new HorizontalLineAnnotation
            {
                Y1 = yValue,
                Stroke = new SolidColorBrush(color),
                StrokeWidth = 2,
                StrokeDashArray = new DoubleCollection { 5, 5 },
                Text = text,
                LabelStyle = new ChartAnnotationLabelStyle
                {
                    FontSize = 10,
                    TextColor = color,
                    Background = Brush.White,
                    CornerRadius = 3,
                    HorizontalTextAlignment = ChartLabelAlignment.Start,
                    VerticalTextAlignment = ChartLabelAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0)
                }
            };

            ChartControl.Annotations.Add(annotation);
        }

        protected override void AddMoleculeSpecificChartAnnotations()
        {
            if (ChartControl == null) return;
            AddThresholdAnnotation(Calculator.StrongPercent, "Effet fort", Colors.Orange);
            AddThresholdAnnotation(Calculator.ModeratePercent, "Effet modéré", Colors.YellowGreen);
            AddThresholdAnnotation(Calculator.LightPercent, "Effet léger", Colors.Green);
        }

        protected override async Task UpdateChart()
        {
            var chart = ChartControl;
            if (chart == null) return;

            if (!Doses.Any())
            {
                ParacetamolChartData.Clear();
                IbuprofenChartData.Clear();
                TotalChartData.Clear();
                await base.UpdateChart();
                return;
            }

            DateTime currentTime = DateTime.Now;
            DateTime start = currentTime.Add(GraphDataStartOffset);
            DateTime end = currentTime.Add(GraphDataEndOffset);
            int points = GraphDataNumberOfPoints;

            List<DoseEntry> copy = Doses.ToList();
            var result = await Task.Run(() => Calculator.GenerateEffectGraph(copy, start, end, points));

            ParacetamolChartData.ReplaceRange(result.Item1.Select(p => new ChartDataPoint(p.Time, p.EffectPara)));
            IbuprofenChartData.ReplaceRange(result.Item2.Select(p => new ChartDataPoint(p.Time, p.EffectIbu)));
            TotalChartData.ReplaceRange(result.Item3.Select(p => new ChartDataPoint(p.Time, p.EffectTotal)));

            if (chart.XAxes?.FirstOrDefault() is DateTimeAxis xAxis)
            {
                xAxis.Minimum = start;
                xAxis.Maximum = end;
                xAxis.IntervalType = DateTimeIntervalType.Auto;
                xAxis.Interval = 3;

                DateTime visibleStart = currentTime.Add(InitialVisibleStartOffset);
                DateTime visibleEnd = currentTime.Add(InitialVisibleEndOffset);

                double totalRange = (end - start).TotalHours;
                double desiredDuration = (visibleEnd - visibleStart).TotalHours;

                if (totalRange > 0)
                {
                    xAxis.ZoomFactor = Math.Max(0.00001, Math.Min(1.0, desiredDuration / totalRange));
                    double desiredStartOffset = (visibleStart - start).TotalHours;
                    xAxis.ZoomPosition = desiredStartOffset / totalRange;
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
    }
}
