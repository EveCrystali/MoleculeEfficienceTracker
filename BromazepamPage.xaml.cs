using MoleculeEfficienceTracker.Controls;
using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    public partial class BromazepamPage : BaseMoleculePage<BromazepamCalculator>
    {
        private readonly PharmacodynamicModel _saturation = new(BromazepamCalculator.EC50_MG_PER_L);

        protected override MoleculePanelView Panel => PanelView;

        protected override string DoseAnnotationIcon => "💊";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-2);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(3);
        protected override int GraphDataNumberOfPoints => 5 * 24 * 2;
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-12);
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(24);

        protected override string AddSectionTitle => "Ajouter une prise";
        protected override string DoseFieldCaption => "Dose (mg)";
        protected override string HelperText =>
            "Comprimé sécable : 1,5 mg · 3 mg · 6 mg.\n" +
            $"La saturation vient d'un modèle Emax, EC50 {BromazepamCalculator.EC50_MG_PER_L:0.###} mg/L.";
        protected override double MaxPlausibleDose => 30;

        protected override IReadOnlyList<double> Presets => new[] { 1.5, 3.0, 6.0 };

        protected override IReadOnlyList<(double Value, string Label, EffectLevel Level)> Thresholds => new[]
        {
            (BromazepamCalculator.STRONG_THRESHOLD, "Fort (4,5 mg)", EffectLevel.Strong),
            (BromazepamCalculator.MODERATE_THRESHOLD, "Net (3 mg)", EffectLevel.Moderate),
            (BromazepamCalculator.LIGHT_THRESHOLD, "Léger (1,5 mg)", EffectLevel.Light),
            (BromazepamCalculator.NEGLIGIBLE_THRESHOLD, "Imperceptible", EffectLevel.None)
        };

        public BromazepamPage() : base(MoleculeKeys.Bromazepam)
        {
            InitializeComponent();
            InitializePageUI();
        }

        protected override double? GetEffectPercentForConcentration(double concentration)
            => _saturation.GetEffectPercent(concentration);

        protected override void UpdateMoleculeSpecificConcentrationInfo(
            List<DoseEntry> doses, DateTime currentTime, double concentration)
        {
            base.UpdateMoleculeSpecificConcentrationInfo(doses, currentTime, concentration);

            // La formule descend dans l'aide du formulaire : « modèle Emax,
            // EC50 0,05 mg/L » en tête d'écran ne répondait à aucune question.
            double saturation = _saturation.GetEffectPercent(concentration);
            Panel.HeadlineText = $"Récepteurs saturés à {saturation:0} %.";
            Panel.HeadlineDetailText = string.Empty;

            DateTime? end = Calculator.PredictEffectEndTime(doses, currentTime);
            Panel.EffectPrediction.Text = end.HasValue && end.Value > currentTime
                ? $"Sous le seuil imperceptible le {end.Value:dd/MM} vers {end.Value:HH\\hmm}."
                : "Sous le seuil imperceptible.";
            Panel.EffectPrediction.IsVisible = true;
        }
    }
}
