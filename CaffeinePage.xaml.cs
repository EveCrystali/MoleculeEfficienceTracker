﻿﻿﻿using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Syncfusion.Maui.Charts;
using Microsoft.Maui.Graphics;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using System.Text;

namespace MoleculeEfficienceTracker
{
    public partial class CaffeinePage : BaseMoleculePage<CaffeineCalculator>
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
        protected override string DoseAnnotationIcon => "🍵";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-7);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(3);
        protected override int GraphDataNumberOfPoints => 10 * 24 * 2; // 10 jours, 2 points par heure
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-24); // Vue initiale de -24h
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(24);   // Vue initiale de +24h
        protected override bool UseConcentrationUnitForDoseAnnotation => false;

        public CaffeinePage()
            : base("caffeine") // Utiliser une clé en minuscules pour la cohérence
        {
            InitializeComponent();
            base.InitializePageUI(); 
        }

        // Surcharge pour la logique spécifique à la caféine
        protected override void UpdateMoleculeSpecificConcentrationInfo(List<DoseEntry> doses, DateTime currentTime)
        {
            if (Calculator is CaffeineCalculator caffeineCalc)
            {
                double concentration = caffeineCalc.CalculateTotalConcentration(doses, currentTime);
                double totalMg = caffeineCalc.CalculateTotalAmount(doses, currentTime);

                ConcentrationOutputLabel.Text = $"{totalMg:F0} mg ({concentration:F2} {Calculator.ConcentrationUnit})";

                var level = caffeineCalc.GetEffectLevel(concentration);

                string text = level switch
                {
                    EffectLevel.Strong => "Effet fort/toxique",
                    EffectLevel.Moderate => "Effet net",
                    EffectLevel.Light => "Effet léger",
                    _ => "Effet négligeable"
                };

                Color color = level switch
                {
                    EffectLevel.Strong => Colors.Green,
                    EffectLevel.Moderate => Colors.Green,
                    EffectLevel.Light => Colors.Orange,
                    _ => Colors.Red
                };

                if (EffectStatusLabel != null)
                {
                    EffectStatusLabel.Text = text;
                    EffectStatusLabel.TextColor = color;
                    EffectStatusLabel.IsVisible = true;
                }

                DateTime? endTime = caffeineCalc.PredictEffectEndTime(doses, currentTime);
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

        protected override void AddMoleculeSpecificChartAnnotations()
        {
            if (Calculator is CaffeineCalculator calc && ChartControl != null)
            {
                AddThresholdAnnotation(CaffeineCalculator.STRONG_THRESHOLD, "Effet fort", Colors.Green);
                AddThresholdAnnotation(CaffeineCalculator.MODERATE_THRESHOLD, "Effet modéré", Colors.Green);
                AddThresholdAnnotation(CaffeineCalculator.LIGHT_THRESHOLD, "Effet léger", Colors.Orange);
                AddThresholdAnnotation(CaffeineCalculator.NEGLIGIBLE_THRESHOLD, "Seuil de perception", Colors.Red);
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

        protected override async Task OnBeforeLoadDataAsync()
        {
            await base.OnBeforeLoadDataAsync();

            List<DoseEntry> existing = await PersistenceService.LoadDosesAsync();
            bool converted = false;

            foreach (DoseEntry d in existing)
            {
                if (d.DoseMg > 0 && d.DoseMg < 20)
                {
                    d.DoseMg *= CaffeineCalculator.MG_PER_UNIT;
                    converted = true;
                }
            }

            if (converted)
            {
                await PersistenceService.SaveDosesAsync(existing);
            }
        }
    }
}
