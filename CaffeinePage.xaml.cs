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
        /// La phrase en tête d'écran vient de <see cref="CaffeineAdvisor"/> : l'écran
        /// Aujourd'hui la redemande mot pour mot, et une règle recopiée à deux
        /// endroits finit toujours par diverger.
        /// </summary>
        protected override void UpdateMoleculeSpecificConcentrationInfo(
            List<DoseEntry> doses, DateTime currentTime, double concentration)
        {
            base.UpdateMoleculeSpecificConcentrationInfo(doses, currentTime, concentration);

            CaffeineAdvisor.Advice advice = CaffeineAdvisor.Build(Calculator, doses, currentTime);
            Panel.HeadlineText = advice.Headline;
            Panel.HeadlineDetailText = advice.Detail;

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
