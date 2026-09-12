using MoleculeEfficienceTracker.Controls;
using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    public partial class CaffeinePage : BaseMoleculePage<CaffeineCalculator>
    {
        private readonly CaffeineNotificationService _notifications;

        protected override MoleculePanelView Panel => PanelView;

        protected override string DoseAnnotationIcon => "☕";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromHours(-18);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromHours(18);
        protected override int GraphDataNumberOfPoints => 36 * 4; // un point par quart d'heure
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-6);
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(10);

        protected override string AddSectionTitle => "Ajouter un café";
        protected override string DoseFieldCaption => "Dose (mg)";
        protected override string HelperText =>
            "Nespresso 80 mg · lungo 95 mg · filtre 120 mg · thé 35 mg";

        protected override double MaxPlausibleDose => 1500;

        /// <summary>
        /// Les doses proposées sortent des données réelles : sur 220 prises
        /// enregistrées, 80 mg en couvre 153, puis 65 mg et 35 mg vingt de plus.
        /// </summary>
        protected override IReadOnlyList<double> Presets => UserPreferences.GetCaffeinePresets();

        protected override IReadOnlyList<(double Value, string Label, EffectLevel Level)> Thresholds => new[]
        {
            (CaffeineCalculator.STRONG_THRESHOLD, "Effet fort", EffectLevel.Strong),
            (CaffeineCalculator.MODERATE_THRESHOLD, "Effet net", EffectLevel.Moderate),
            (CaffeineCalculator.LIGHT_THRESHOLD, "Effet léger", EffectLevel.Light),
            (CaffeineCalculator.NEGLIGIBLE_THRESHOLD, "Perception", EffectLevel.None)
        };

        public CaffeinePage() : base(MoleculeKeys.Caffeine)
        {
            _notifications = ServiceLocator.GetOptional<CaffeineNotificationService>() ?? new CaffeineNotificationService();

            InitializeComponent();
            InitializePageUI();
        }

        /// <summary>
        /// La phrase en tête d'écran.
        ///
        /// C'est elle qui porte la décision. La courbe reste dessous : elle
        /// documente, elle ne tranche pas — et à sept heures du matin, personne ne
        /// lit une courbe.
        /// </summary>
        protected override void UpdateMoleculeSpecificConcentrationInfo(
            List<DoseEntry> doses, DateTime currentTime, double concentration)
        {
            base.UpdateMoleculeSpecificConcentrationInfo(doses, currentTime, concentration);

            DateTime bedTime = CaffeineNotificationService.NextBedTime(currentTime);
            double threshold = UserPreferences.GetSleepThreshold();
            double weight = UserPreferences.GetWeightKg();
            double preset = Presets.Count > 0 ? Presets[0] : CaffeineCalculator.MG_PER_UNIT;

            double atBedTime = Calculator.CalculateTotalConcentration(doses, bedTime);

            DateTime? cutoff = Calculator.LatestIntakeTimeBefore(
                doses, bedTime, preset, weight, currentTime, threshold);

            if (cutoff is null)
            {
                Panel.HeadlineText = $"Le café déjà bu suffit à dépasser {threshold:0.#} mg/L à {bedTime:HH\\hmm}.";
                Panel.HeadlineDetailText =
                    $"Estimation au coucher : {atBedTime:0.##} mg/L. Un café de plus repousserait l'endormissement.";
            }
            else if (cutoff.Value <= currentTime.AddMinutes(1))
            {
                Panel.HeadlineText = $"C'est le moment ou jamais pour un {preset:0} mg.";
                Panel.HeadlineDetailText =
                    $"Au-delà de maintenant, il resterait plus de {threshold:0.#} mg/L à {bedTime:HH\\hmm}.";
            }
            else
            {
                Panel.HeadlineText = $"Dernier {preset:0} mg avant {cutoff.Value:HH\\hmm} pour dormir à {bedTime:HH\\hmm}.";
                Panel.HeadlineDetailText =
                    $"Sans autre café, il resterait {atBedTime:0.##} mg/L au coucher — seuil retenu {threshold:0.#} mg/L.";
            }

            DateTime? end = Calculator.PredictEffectEndTime(doses, currentTime);
            Panel.EffectPrediction.Text = end.HasValue && end.Value > currentTime
                ? $"Sous le seuil de perception vers {end.Value:HH\\hmm}."
                : "Sous le seuil de perception.";
            Panel.EffectPrediction.IsVisible = true;
        }

        protected override async Task OnAfterDoseChangedAsync()
        {
            await _notifications.ScheduleCutoffAsync(Doses.ToList(), DateTime.Now);
        }
    }
}
