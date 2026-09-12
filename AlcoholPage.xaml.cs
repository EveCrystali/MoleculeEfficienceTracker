using System.Globalization;
using MoleculeEfficienceTracker.Controls;
using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    public partial class AlcoholPage : BaseMoleculePage<AlcoholCalculator>
    {
        private readonly Entry _volumeEntry;
        private readonly Entry _percentEntry;
        private readonly Picker _beveragePicker;

        protected override MoleculePanelView Panel => PanelView;

        protected override string DoseAnnotationIcon => "🍷";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromHours(-18);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromHours(18);
        protected override int GraphDataNumberOfPoints => 36 * 6;
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-6);
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(8);

        protected override string AddSectionTitle => "Ajouter un verre";
        protected override string DoseFieldCaption => "Unités";
        protected override string HelperText =>
            "1 unité = 10 g d'alcool pur. Bière 330 ml à 5 % ≈ 1,3 u · vin 120 ml à 12 % ≈ 1,2 u";
        protected override double MaxPlausibleDose => 40;

        protected override IReadOnlyList<(double Value, string Label, EffectLevel Level)> Thresholds => new[]
        {
            (AlcoholCalculator.BAC_STRONG_THRESHOLD, "Ivresse forte", EffectLevel.Strong),
            (AlcoholCalculator.BAC_MODERATE_THRESHOLD, "Limite légale 0,5", EffectLevel.Moderate),
            (AlcoholCalculator.BAC_LIGHT_THRESHOLD, "Léger", EffectLevel.Light),
            (AlcoholCalculator.BAC_NEGLIGIBLE_THRESHOLD, "Négligeable", EffectLevel.None)
        };

        public AlcoholPage() : base(MoleculeKeys.Alcohol)
        {
            InitializeComponent();

            _volumeEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "Volume (ml)", MinimumHeightRequest = 48 };
            _percentEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "Degré (%)", MinimumHeightRequest = 48 };
            _beveragePicker = new Picker { Title = "Type de boisson", MinimumHeightRequest = 48 };

            _beveragePicker.ItemsSource = AlcoholCalculator.KnownBeverageTypes.ToList();
            _beveragePicker.SelectedIndex = 0;

            SemanticProperties.SetDescription(_volumeEntry, "Volume du verre en millilitres");
            SemanticProperties.SetDescription(_percentEntry, "Degré d'alcool en pourcentage");
            SemanticProperties.SetDescription(_beveragePicker, "Type de boisson, qui fixe la vitesse d'absorption");

            PanelView.ExtraInputSlot.Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = "Ou par volume et degré", FontSize = 13 },
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitionCollection(
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Star)),
                        ColumnSpacing = 10,
                        Children = { _volumeEntry, _percentEntry }
                    },
                    _beveragePicker
                }
            };

            Grid.SetColumn(_volumeEntry, 0);
            Grid.SetColumn(_percentEntry, 1);

            InitializePageUI();
        }

        private string SelectedBeverage =>
            _beveragePicker.SelectedItem as string ?? AlcoholCalculator.KnownBeverageTypes.First();

        /// <summary>
        /// La quantité peut venir du champ « unités » ou du couple volume/degré.
        /// </summary>
        protected override (bool Ok, double Amount, string? Error) ReadDoseInput()
        {
            bool hasVolume = TryReadNumber(_volumeEntry.Text, out double volume);
            bool hasPercent = TryReadNumber(_percentEntry.Text, out double percent);

            if (hasVolume && hasPercent)
            {
                if (volume <= 0 || percent <= 0 || percent > 100)
                    return (false, 0, "Volume et degré doivent être plausibles (degré au plus 100 %).");

                return (true, AlcoholCalculator.VolumePercentToUnits(volume, percent), null);
            }

            if (hasVolume ^ hasPercent)
                return (false, 0, "Indiquez le volume et le degré, ou saisissez directement des unités.");

            return base.ReadDoseInput();
        }

        private static bool TryReadNumber(string? raw, out double value)
            => double.TryParse((raw ?? string.Empty).Trim().Replace(',', '.'),
                               NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        /// <summary>
        /// Le type de boisson accompagne la prise.
        ///
        /// Il vivait sur l'instance du calculateur, et le sélecteur était de toute
        /// façon lié à une propriété que la page n'exposait pas : le temps
        /// d'absorption restait figé sur « bière », et le dernier type choisi se
        /// serait appliqué rétroactivement à tout l'historique.
        /// </summary>
        protected override DoseEntry BuildDose(double amount, DateTime when)
            => new(when, amount, UserPreferences.GetWeightKg(), MoleculeKeys.Alcohol)
            {
                BeverageType = SelectedBeverage
            };

        protected override EffectLevel ResolveEffectLevel(double bac)
            => Calculator.GetEffectLevelFromBAC(bac);

        protected override void UpdateMoleculeSpecificConcentrationInfo(
            List<DoseEntry> doses, DateTime currentTime, double bac)
        {
            base.UpdateMoleculeSpecificConcentrationInfo(doses, currentTime, bac);

            Panel.HeadlineText = $"Alcoolémie estimée : {bac:0.00} g/L";

            DateTime? sober = Calculator.PredictSoberTime(doses, currentTime);
            if (bac >= AlcoholCalculator.BAC_MODERATE_THRESHOLD)
                Panel.HeadlineDetailText = "Au-dessus de la limite légale de conduite.";
            else if (bac > 0)
                Panel.HeadlineDetailText = "Sous la limite légale — estimation, jamais une autorisation.";
            else
                Panel.HeadlineDetailText = string.Empty;

            Panel.EffectPrediction.Text = sober.HasValue && sober.Value > currentTime
                ? $"Sous {AlcoholCalculator.BAC_LIGHT_THRESHOLD:0.0} g/L vers {sober.Value:HH\\hmm}."
                : "Sous le seuil léger.";
            Panel.EffectPrediction.IsVisible = true;
        }

        protected override Task OnAfterDoseChangedAsync()
        {
            _volumeEntry.Text = string.Empty;
            _percentEntry.Text = string.Empty;
            return Task.CompletedTask;
        }
    }
}
