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
    public partial class ParacetamolPage : BaseMoleculePage<ParacetamolCalculator>
    {
        protected override Entry DoseInputControl => DoseEntry;
        protected override DatePicker DatePickerControl => DatePicker;
        protected override TimePicker TimePickerControl => TimePicker;
        protected override Label ConcentrationOutputLabel => ConcentrationLabel;
        protected override Label LastUpdateOutputLabel => LastUpdateLabel;
        protected override SfCartesianChart ChartControl => ConcentrationChart;
        protected override CollectionView DosesDisplayCollection => DosesCollection;
        protected override Label EmptyStateIndicatorLabel => EmptyDosesLabel;
        

        protected override string DoseAnnotationIcon => "💊";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-7);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(3);
        protected override int GraphDataNumberOfPoints => 10 * 24 * 2;
        // Zoom plus serré : la majorité des utilisations concernent un seul
        // comprimé de 1 g, l'effet durant seulement quelques heures.
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-4);
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(12);


        public ParacetamolPage() : base("paracetamol")
        {
            InitializeComponent();
            base.InitializePageUI();
        }

        protected override async Task OnBeforeLoadDataAsync()
        {
            await base.OnBeforeLoadDataAsync(); // Appel à l'implémentation de base (facultatif ici car vide)
        }

        protected override void UpdateMoleculeSpecificConcentrationInfo(List<DoseEntry> doses, DateTime currentTime)
        {
            if (Calculator is ParacetamolCalculator calc)
            {
                double concentration = calc.CalculateTotalConcentration(doses, currentTime);
                double totalMg = calc.CalculateTotalAmount(doses, currentTime);

                ConcentrationOutputLabel.Text = $"{totalMg:F0} mg ({concentration:F2} {Calculator.ConcentrationUnit})";

                double effectPercent = _pdModel.GetEffectPercent(concentration);
                var level = calc.GetEffectLevel(concentration);

                string text = level switch
                {
                    EffectLevel.Strong => "Effet fort",
                    EffectLevel.Moderate => "Effet modéré",
                    EffectLevel.Light => "Effet léger",
                    _ => "Effet négligeable"
                };

                Color color = level switch
                {
                    EffectLevel.Strong => Colors.Red,
                    EffectLevel.Moderate => Colors.Green,
                    EffectLevel.Light => Colors.Green,
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
            if (Calculator is ParacetamolCalculator calc && ChartControl != null)
            {
                AddThresholdAnnotation(ParacetamolCalculator.STRONG_THRESHOLD, "Fort (1g)", Colors.Orange);
                AddThresholdAnnotation(ParacetamolCalculator.MODERATE_THRESHOLD, "Modéré", Colors.YellowGreen);
                AddThresholdAnnotation(ParacetamolCalculator.LIGHT_THRESHOLD, "Léger", Colors.Green);
                AddThresholdAnnotation(ParacetamolCalculator.NEGLIGIBLE_THRESHOLD, "Imperceptible", Colors.Grey);
            }
        }

        protected override double? GetEffectPercentForConcentration(double concentration)
        {
            return _pdModel.GetEffectPercent(concentration);
        }
    }
}
