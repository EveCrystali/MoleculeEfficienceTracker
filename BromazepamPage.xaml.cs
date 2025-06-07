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
    public partial class BromazepamPage : BaseMoleculePage<BromazepamCalculator>
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

        private readonly PharmacodynamicModel _pdModel = new PharmacodynamicModel(0.05);
        

        protected override string DoseAnnotationIcon => "💊";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-7);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(3);
        protected override int GraphDataNumberOfPoints => 10 * 24 * 2;
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-12); // Ajustez si nécessaire
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(24);  // Ajustez si nécessaire


        public BromazepamPage() : base("bromazepam")
        {
            InitializeComponent();
            base.InitializePageUI();
        }

        protected override void UpdateMoleculeSpecificConcentrationInfo(List<DoseEntry> doses, DateTime currentTime)
        {
            if (Calculator is BromazepamCalculator calc)
            {
                double concentration = calc.CalculateTotalConcentration(doses, currentTime);
                double saturation = _pdModel.GetEffectPercent(concentration);
                var level = calc.GetEffectLevelFromSaturation(saturation);

                string text = level switch
                {
                    EffectLevel.Strong => "⚠️ Risque de surdosage",
                    EffectLevel.Moderate => "Effet marqué",
                    EffectLevel.Light => "Effet modéré",
                    _ => "Effet léger"
                };

                Color color = level switch
                {
                    EffectLevel.Strong => Colors.Red,
                    EffectLevel.Moderate => Colors.Green,
                    EffectLevel.Light => Colors.Green,
                    _ => Colors.Orange
                };

                if (EffectStatusLabel != null)
                {
                    EffectStatusLabel.Text = text;
                    EffectStatusLabel.TextColor = color;
                    EffectStatusLabel.IsVisible = true;
                }

                if (EffectPowerLabel != null)
                {
                    EffectPowerLabel.Text = $"Saturation des récepteurs : {saturation:F0} %";
                    EffectPowerLabel.IsVisible = true;
                }

                DateTime? endTime = calc.PredictEffectEndTime(doses, currentTime);
                if (EffectEndPredictionLabel != null)
                {
                    if (endTime.HasValue && endTime.Value > currentTime)
                    {
                        var remaining = endTime.Value - currentTime;
                        EffectEndPredictionLabel.Text = $"Effet négligeable estimé dans {remaining.TotalHours:F1} heures";
                    }
                    else
                    {
                        EffectEndPredictionLabel.Text = "Effet actuellement négligeable";
                    }
                    EffectEndPredictionLabel.IsVisible = true;
                }
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
            if (Calculator is BromazepamCalculator calc && ChartControl != null)
            {
                AddThresholdAnnotation(BromazepamCalculator.STRONG_THRESHOLD, "Fort (4,5mg)", Colors.Orange);
                AddThresholdAnnotation(BromazepamCalculator.MODERATE_THRESHOLD, "Modéré (3mg)", Colors.Yellow);
                AddThresholdAnnotation(BromazepamCalculator.LIGHT_THRESHOLD, "Léger (1,5mg)", Colors.Green);
                AddThresholdAnnotation(BromazepamCalculator.NEGLIGIBLE_THRESHOLD, "Imperceptible", Colors.Grey);
            }
        }

        protected override double? GetEffectPercentForConcentration(double concentration)
        {
            return _pdModel.GetEffectPercent(concentration);
        }

        protected override async Task OnBeforeLoadDataAsync()
        {
            await base.OnBeforeLoadDataAsync(); // Appel à l'implémentation de base (facultatif ici car vide)
        }
    }
}
