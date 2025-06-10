using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Syncfusion.Maui.Charts;
using Microsoft.Maui.Graphics;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using System.Text;
using System.IO;

namespace MoleculeEfficienceTracker
{
    public partial class IbuprofenePage : BaseMoleculePage<IbuprofeneCalculator>
    {
        protected override Entry DoseInputControl => DoseEntry;
        protected override DatePicker DatePickerControl => DatePicker;
        protected override TimePicker TimePickerControl => TimePicker;
        protected override Label ConcentrationOutputLabel => ConcentrationLabel;
        protected override Label LastUpdateOutputLabel => LastUpdateLabel;
        protected override SfCartesianChart ChartControl => ConcentrationChart;
        protected override CollectionView DosesDisplayCollection => DosesCollection;
        protected override Label EmptyStateIndicatorLabel => EmptyDosesLabel;

        // Labels spécifiques à l'effet
        private Label EffectStatusLabel => EffectStatus;
        private Label EffectEndPredictionLabel => EffectPrediction;
        private Label EffectPowerLabel => EffectPower;

        private readonly PharmacodynamicModel _pdModel = new PharmacodynamicModel(12.0);
        private readonly DataPersistenceService _painService = new("pain_relief");

        protected override string DoseAnnotationIcon => "💊";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-7);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(3);
        protected override int GraphDataNumberOfPoints => 10 * 24 * 2;
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-12); // Ajustez si nécessaire
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(12);  // Ajustez si nécessaire


        public IbuprofenePage() : base("ibuprofene")
        {
            InitializeComponent();
            base.InitializePageUI();
        }

        private async Task SyncPainReliefAsync()
        {
            var combined = await _painService.LoadDosesAsync();
            combined.RemoveAll(d => d.MoleculeKey.Equals("ibuprofen", StringComparison.OrdinalIgnoreCase) || d.MoleculeKey.Equals("ibuprofene", StringComparison.OrdinalIgnoreCase));
            combined.AddRange(Doses.Select(d => { if (string.IsNullOrEmpty(d.MoleculeKey)) d.MoleculeKey = "ibuprofene"; return d; }));
            combined = combined.GroupBy(d => d.Id).Select(g => g.First()).OrderByDescending(d => d.TimeTaken).ToList();
            await _painService.SaveDosesAsync(combined);
        }

        private new async void OnAddDoseClicked(object sender, EventArgs e)
        {
            base.OnAddDoseClicked(sender, e);
            await SyncPainReliefAsync();
        }

        private new async void OnDeleteDoseClicked(object sender, EventArgs e)
        {
            base.OnDeleteDoseClicked(sender, e);
            await SyncPainReliefAsync();
        }

        private new async void OnClearAllDataClicked(object sender, EventArgs e)
        {
            base.OnClearAllDataClicked(sender, e);
            await _painService.DeleteAllDataAsync();
        }

        protected override void UpdateMoleculeSpecificConcentrationInfo(List<DoseEntry> doses, DateTime currentTime)
        {
            if (Calculator is IbuprofeneCalculator calc)
            {
                double concentration = calc.CalculateTotalConcentration(doses, currentTime);
                double effectPercent = _pdModel.GetEffectPercent(concentration);
                var level = calc.GetEffectLevel(concentration);

                string text = level switch
                {
                    EffectLevel.Strong => "Fort",
                    EffectLevel.Moderate => "Net",
                    EffectLevel.Light => "Léger",
                    _ => "Négligeable"
                };

                Color color = level switch
                {
                    EffectLevel.Strong => Colors.Red,
                    EffectLevel.Moderate => Colors.Green,
                    EffectLevel.Light => Colors.Orange,
                    _ => Colors.Gray
                };

                if (EffectStatusLabel != null)
                {
                    EffectStatusLabel.Text = text;
                    EffectStatusLabel.TextColor = color;
                    EffectStatusLabel.IsVisible = true;
                }

                if (EffectPowerLabel != null)
                {
                    EffectPowerLabel.Text = $"Saturation : {effectPercent:F0} %";
                    EffectPowerLabel.IsVisible = true;
                }

                DateTime? endTime = calc.PredictEffectEndTime(doses, currentTime);
                if (EffectEndPredictionLabel != null)
                {
                    if (endTime.HasValue && endTime.Value > currentTime)
                    {
                        var remaining = endTime.Value - currentTime;
                        EffectEndPredictionLabel.Text = $"Effet négligeable dans {remaining.TotalHours:F1} h";
                    }
                    else
                    {
                        EffectEndPredictionLabel.Text = "Effet actuellement négligeable";
                    }
                    EffectEndPredictionLabel.IsVisible = true;
                }
            }
        }

        protected override async Task OnBeforeLoadDataAsync()
        {
            await base.OnBeforeLoadDataAsync();

            var own = await PersistenceService.LoadDosesAsync();
            var combined = await _painService.LoadDosesAsync();

            foreach (var d in own)
            {
                if (string.IsNullOrEmpty(d.MoleculeKey)) d.MoleculeKey = "ibuprofene";
            }

            var fromCombined = combined.Where(d => d.MoleculeKey.Equals("ibuprofen", StringComparison.OrdinalIgnoreCase) || d.MoleculeKey.Equals("ibuprofene", StringComparison.OrdinalIgnoreCase)).ToList();

            var merged = own
                .Concat(fromCombined)
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderByDescending(d => d.TimeTaken)
                .ToList();

            await PersistenceService.SaveDosesAsync(merged);
            combined.RemoveAll(d => d.MoleculeKey.Equals("ibuprofen", StringComparison.OrdinalIgnoreCase) || d.MoleculeKey.Equals("ibuprofene", StringComparison.OrdinalIgnoreCase));
            combined.AddRange(merged);
            combined = combined.GroupBy(d => d.Id).Select(g => g.First()).OrderByDescending(d => d.TimeTaken).ToList();
            await _painService.SaveDosesAsync(combined);
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
            if (Calculator is IbuprofeneCalculator calc && ChartControl != null)
            {
                AddThresholdAnnotation(IbuprofeneCalculator.STRONG_THRESHOLD, "Fort", Colors.Orange);
                AddThresholdAnnotation(IbuprofeneCalculator.MODERATE_THRESHOLD, "Net", Colors.YellowGreen);
                AddThresholdAnnotation(IbuprofeneCalculator.LIGHT_THRESHOLD, "Léger", Colors.Green);
                AddThresholdAnnotation(IbuprofeneCalculator.NEGLIGIBLE_THRESHOLD, "Imperceptible", Colors.Grey);
            }
        }

        protected override double? GetEffectPercentForConcentration(double concentration)
        {
            return _pdModel.GetEffectPercent(concentration);
        }
    }
}
