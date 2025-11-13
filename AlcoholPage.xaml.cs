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
using System.Linq;

namespace MoleculeEfficienceTracker
{
    public partial class AlcoholPage : BaseMoleculePage<AlcoholCalculator>
    {
        // Implémentation des propriétés abstraites pour les contrôles UI
        protected override Entry DoseInputControl => VolumeEntry;
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
        protected override int GraphDataNumberOfPoints => 4 * 24 * 12; // 4 jours au total, 12 points par heure for accurate spline interpolation
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-4); // Vue initiale de -6h
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(6);   // Vue initiale de +6h

        /// <summary>
        /// Items displayed in the beverage picker.
        /// </summary>
        public List<string> BeverageOptions => AlcoholCalculator.KnownBeverageTypes.ToList();

        public AlcoholPage() : base("alcohol")
        {
            InitializeComponent();
            InitializePageUI();
            if (BeverageOptions.Count > 0 && string.IsNullOrEmpty(Calculator.BeverageType))
            {
                Calculator.BeverageType = BeverageOptions[0];
            }
        }

        // Ajout personnalisé pour convertir volume et pourcentage en unités
        protected async new void OnAddDoseClicked(object sender, EventArgs e)
        {
            if (double.TryParse(VolumeEntry.Text, out double volume) &&
                double.TryParse(DegreeEntry.Text, out double percent) &&
                volume > 0 && percent > 0)
            {
                double units = AlcoholCalculator.VolumePercentToUnits(volume, percent);
                DateTime selectedDate = DatePicker.Date;
                TimeSpan selectedTime = TimePicker.Time;
                DateTime dateTime = selectedDate.Add(selectedTime);

                double weight = UserPreferences.GetWeightKg();
                DoseEntry dose = new DoseEntry(dateTime, units, weight, MoleculeKey);

                Doses.Insert(0, dose);
                VolumeEntry.Text = string.Empty;
                DegreeEntry.Text = string.Empty;

                UpdateConcentrationDisplay();
                await UpdateChart();
                UpdateDoseAnnotations();

                await SaveDataAsync();
                // await AlertService.ShowAlertAsync("✅", $"Dose de {units:F2} u ajoutée pour {dateTime:dd/MM HH:mm}");
            }
            else
            {
                await AlertService.ShowAlertAsync("❌", "Veuillez entrer un volume et un % valides");
            }

            if (sender is Button btn) AnimateButton(btn);
            UpdateEmptyState();
        }

        protected override void UpdateMoleculeSpecificConcentrationInfo(List<DoseEntry> doses, DateTime currentTime)
        {
            if (Calculator is AlcoholCalculator calc)
            {
                double units = calc.CalculateTotalAmount(doses, currentTime); // ✅ renvoie les unités restantes
                double bac = calc.CalculateTotalBloodAlcohol(doses, currentTime);

                ConcentrationOutputLabel.Text = $"{units:F2} u ({bac:F2} g/L)";

                var level = calc.GetEffectLevelFromBAC(bac);

                string text = level switch
                {
                    EffectLevel.Strong => "Ivre",
                    EffectLevel.Moderate => "Limite",
                    EffectLevel.Light => "Léger",
                    _ => "Effet négligeable"
                };

                Color color = level switch
                {
                    EffectLevel.Strong => Colors.Brown,
                    EffectLevel.Moderate => Colors.Red,
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
                        EffectEndPredictionLabel.Text = $"BAC < {AlcoholCalculator.BAC_LIGHT_THRESHOLD:F1} g/L dans {remaining.TotalHours:F1} h";
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
                AddThresholdAnnotation(AlcoholCalculator.BAC_MODERATE_THRESHOLD, "Légale", Colors.Orange);
                AddThresholdAnnotation(AlcoholCalculator.BAC_LIGHT_THRESHOLD, "Léger", Colors.Green);
                AddThresholdAnnotation(AlcoholCalculator.BAC_NEGLIGIBLE_THRESHOLD, "Négligeable", Colors.Grey);
            }
        }
    }
}
