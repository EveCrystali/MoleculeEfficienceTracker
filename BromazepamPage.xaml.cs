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
                var level = calc.GetEffectLevel(concentration);

                string text = level switch
                {
                    EffectLevel.Strong => "Effet anxiolytique fort",
                    EffectLevel.Moderate => "Effet anxiolytique modéré",
                    EffectLevel.Light => "Effet anxiolytique très léger",
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

        protected override void AddMoleculeSpecificChartAnnotations()
        {
            if (Calculator is BromazepamCalculator calc && ChartControl != null)
            {
                void AddLevelLine(double y, Color color, string text)
                {
                    var ann = new HorizontalLineAnnotation
                    {
                        Y1 = y,
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
                    ChartControl.Annotations.Add(ann);
                }

                AddLevelLine(BromazepamCalculator.NEGLIGIBLE_THRESHOLD, Colors.Red, "Seuil de perception");
                AddLevelLine(BromazepamCalculator.LIGHT_THRESHOLD, Colors.Orange, "Effet anxiolytique très léger");
                AddLevelLine(BromazepamCalculator.MODERATE_THRESHOLD, Colors.Green, "Effet anxiolytique modéré");
                AddLevelLine(BromazepamCalculator.STRONG_THRESHOLD, Colors.Green, "Effet anxiolytique fort");
            }
        }

        protected override async Task OnBeforeLoadDataAsync()
        {
            await base.OnBeforeLoadDataAsync(); // Appel à l'implémentation de base (facultatif ici car vide)
        }
    }
}
