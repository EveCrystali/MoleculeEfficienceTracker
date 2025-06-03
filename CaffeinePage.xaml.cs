﻿using MoleculeEfficienceTracker.Core.Models;
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

        // Implémentation des propriétés abstraites spécifiques à la molécule
        protected override string DoseAnnotationIcon => "🍵";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-7);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(3);
        protected override int GraphDataNumberOfPoints => 10 * 24 * 2; // 10 jours, 2 points par heure
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-24); // Vue initiale de -24h
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(24);   // Vue initiale de +24h

        public CaffeinePage()
            : base("Caffeine")
        {
            InitializeComponent();
            base.InitializePageUI(); 
        }

        // Surcharge pour la logique spécifique à la caféine
        protected override void UpdateMoleculeSpecificConcentrationInfo(List<DoseEntry> doses, DateTime currentTime)
        {
            if (Calculator is CaffeineCalculator caffeineCalc && IneffectiveTimeLabel != null)
            {
                var ineffectiveTime = caffeineCalc.GetIneffectiveTime(Doses.ToList(), currentTime);
                if (ineffectiveTime.HasValue)
                {
                    var timeRemaining = ineffectiveTime.Value - currentTime;
                    if (timeRemaining.TotalMinutes > 0)
                    {
                        IneffectiveTimeLabel.Text = $"⚠️ Effet négligeable à {ineffectiveTime.Value:HH:mm} (dans {timeRemaining.TotalHours:F1}h)";
                        IneffectiveTimeLabel.IsVisible = true;
                    }
                    else
                    {
                        IneffectiveTimeLabel.Text = "⚠️ Effet actuellement négligeable";
                        IneffectiveTimeLabel.IsVisible = true;
                    }
                }
                else
                {
                    IneffectiveTimeLabel.Text = "✅ Effet maintenu (>24h)";
                    IneffectiveTimeLabel.IsVisible = true; // Ou false si pas d'info à afficher
                }
            }
            else if (IneffectiveTimeLabel != null)
            {
                IneffectiveTimeLabel.IsVisible = false;
            }
        }

        protected override void AddMoleculeSpecificChartAnnotations()
        {
            if (Calculator is CaffeineCalculator caffeineCalc && ChartControl != null)
            {
                var threshold = CaffeineCalculator.GetEffectivenessThreshold();
                var thresholdAnnotation = new HorizontalLineAnnotation
                {
                    Y1 = threshold,
                    Stroke = Brush.Red,
                    StrokeWidth = 2,
                    StrokeDashArray = new DoubleCollection { 5, 5 },
                    // Rétablit le texte et les styles de label de l'annotation d'origine
                    Text = $"Seuil d'efficacité",
                    LabelStyle = new ChartAnnotationLabelStyle
                    {
                        FontSize = 10,
                        TextColor = Colors.Red,
                        Background = Brush.White,
                        CornerRadius = 3,
                        HorizontalTextAlignment = ChartLabelAlignment.Start,
                        VerticalTextAlignment = ChartLabelAlignment.Center,
                        Margin = new Thickness(5, 0, 0, 0)
                    }
                };
                ChartControl.Annotations.Add(thresholdAnnotation);
            }
        }
    }
}
