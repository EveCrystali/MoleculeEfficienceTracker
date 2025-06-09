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
    public partial class AlcoholPage : BaseMoleculePage<AlcoholCalculator>
    {
        // Implémentation des propriétés abstraites pour les contrôles UI
        protected override Entry DoseInputControl => DoseEntry;
        protected override DatePicker DatePickerControl => DatePicker;
        protected override TimePicker TimePickerControl => TimePicker;
        protected override Label ConcentrationOutputLabel => ConcentrationLabel;
        protected override Label LastUpdateOutputLabel => LastUpdateLabel;
        protected override SfCartesianChart ChartControl => ConcentrationChart;
        protected override Label EmptyStateIndicatorLabel => EmptyDosesLabel;
        protected override CollectionView DosesDisplayCollection => DosesCollection;

        // Labels spécifiques à l'effet
        private Label EffectStatusLabel => EffectStatus;
        private Label EffectEndPredictionLabel => EffectPrediction;

        // Implémentation des propriétés abstraites spécifiques à la molécule
        protected override string DoseAnnotationIcon => "🍾"; // Icône pour l'alcool
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-3); // Par exemple, afficher 3 jours avant
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(1);     // Et 1 jour après
        protected override int GraphDataNumberOfPoints => 4 * 24 * 4; // 4 jours au total, 4 points par heure
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-12); // Vue initiale de -12h
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(12);   // Vue initiale de +12h

        public AlcoholPage() : base("alcohol")
        {
            InitializeComponent();
            base.InitializePageUI();
        }

        protected override void UpdateMoleculeSpecificConcentrationInfo(List<DoseEntry> doses, DateTime currentTime)
        {
            if (Calculator is AlcoholCalculator calc)
            {
                double units = calc.CalculateTotalConcentration(doses, currentTime);
                double bac = calc.CalculateTotalBloodAlcohol(doses, currentTime);

                ConcentrationOutputLabel.Text = $"{units:F2} u ({bac:F2} g/L)";

                var level = calc.GetEffectLevelFromBAC(bac);

                string text = level switch
                {
                    EffectLevel.Strong => "Ivresse forte",
                    EffectLevel.Moderate => "Effet modéré",
                    EffectLevel.Light => "Effet léger",
                    _ => "Effet négligeable"
                };

                Color color = level switch
                {
                    EffectLevel.Strong => Colors.Red,
                    EffectLevel.Moderate => Colors.Orange,
                    EffectLevel.Light => Colors.Green,
                    _ => Colors.Gray
                };

                if (EffectStatusLabel != null)
                {
                    EffectStatusLabel.Text = text;
                    EffectStatusLabel.TextColor = color;
                    EffectStatusLabel.IsVisible = true;
                }

                DateTime? endTime = calc.PredictSoberTime(doses, currentTime);
                if (EffectEndPredictionLabel != null)
                {
                    if (endTime.HasValue && endTime.Value > currentTime)
                    {
                        var remaining = endTime.Value - currentTime;
                        EffectEndPredictionLabel.Text = $"BAC < {AlcoholCalculator.BAC_NEGLIGIBLE_THRESHOLD:F1} g/L dans {remaining.TotalHours:F1} h";
                    }
                    else
                    {
                        EffectEndPredictionLabel.Text = "BAC négligeable";
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
            if (ChartControl != null)
            {
                AddThresholdAnnotation(AlcoholCalculator.BAC_STRONG_THRESHOLD, "Ivresse forte", Colors.Red);
                AddThresholdAnnotation(AlcoholCalculator.BAC_MODERATE_THRESHOLD, "Modéré", Colors.Orange);
                AddThresholdAnnotation(AlcoholCalculator.BAC_LIGHT_THRESHOLD, "Léger", Colors.Green);
                AddThresholdAnnotation(AlcoholCalculator.BAC_NEGLIGIBLE_THRESHOLD, "Négligeable", Colors.Grey);
            }
        }
    }
}
