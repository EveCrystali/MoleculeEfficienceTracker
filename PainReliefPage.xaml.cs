using MoleculeEfficienceTracker.Controls;
using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    public partial class PainReliefPage : BaseMoleculePage<CombinedPainReliefCalculator>
    {
        private readonly Picker _moleculePicker;

        /// <summary>
        /// Les deux molécules partagent un seul fichier.
        ///
        /// L'ancienne version en tenait trois — un agrégat plus un par molécule —
        /// et les recopiait les uns dans les autres à chaque affichage, si bien
        /// qu'une suppression pouvait être ressuscitée par la fusion suivante.
        /// </summary>
        protected override MoleculePanelView Panel => PanelView;

        protected override string DoseAnnotationIcon => "💊";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromHours(-24);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromHours(24);
        protected override int GraphDataNumberOfPoints => 48 * 4;
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-6);
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(12);

        protected override string AddSectionTitle => "Ajouter une prise";
        protected override string DoseFieldCaption => "Dose (mg)";
        protected override string HelperText => "Paracétamol 500 ou 1000 mg · ibuprofène 200 ou 400 mg";
        protected override double MaxPlausibleDose => 4000;
        protected override bool UseConcentrationUnitForDoseAnnotation => false;

        protected override IReadOnlyList<(double Value, string Label, EffectLevel Level)> Thresholds => new[]
        {
            (Calculator.StrongPercent, "Fort", EffectLevel.Strong),
            (Calculator.ModeratePercent, "Net", EffectLevel.Moderate),
            (Calculator.LightPercent, "Léger", EffectLevel.Light),
            (Calculator.NegligibleEffect, "Négligeable", EffectLevel.None)
        };

        public PainReliefPage() : base(MoleculeKeys.PainRelief)
        {
            InitializeComponent();

            _moleculePicker = new Picker
            {
                Title = "Molécule",
                MinimumHeightRequest = 48,
                ItemsSource = new List<string> { "Paracétamol", "Ibuprofène" },
                SelectedIndex = 0
            };
            SemanticProperties.SetDescription(_moleculePicker, "Molécule de la prise");

            PanelView.ExtraInputSlot.Content = _moleculePicker;

            InitializePageUI();
        }

        private readonly DataPersistenceService _paracetamolStore = new(MoleculeKeys.Paracetamol);
        private readonly DataPersistenceService _ibuprofenStore = new(MoleculeKeys.Ibuprofen);

        // Une molécule, un fichier. L'agrégat « pain_relief » a disparu : il
        // stockait chaque prise en double et la fusion à chaque affichage pouvait
        // ressusciter une suppression faite ailleurs.
        protected override async Task<List<DoseEntry>> ReadDosesAsync()
        {
            List<DoseEntry> para = await _paracetamolStore.LoadDosesAsync();
            List<DoseEntry> ibu = await _ibuprofenStore.LoadDosesAsync();
            return para.Concat(ibu).ToList();
        }

        protected override async Task WriteDosesAsync(List<DoseEntry> doses)
        {
            await _paracetamolStore.SaveDosesAsync(
                doses.Where(d => d.MoleculeKey == MoleculeKeys.Paracetamol).ToList());
            await _ibuprofenStore.SaveDosesAsync(
                doses.Where(d => d.MoleculeKey == MoleculeKeys.Ibuprofen).ToList());
        }

        protected override async Task RemoveAllAsync()
        {
            await _paracetamolStore.DeleteAllDataAsync();
            await _ibuprofenStore.DeleteAllDataAsync();
        }

        protected override async Task<string?> BackupStoreAsync(string suffix)
        {
            await _paracetamolStore.BackupAsync(suffix);
            return await _ibuprofenStore.BackupAsync(suffix);
        }

        private string SelectedMoleculeKey =>
            _moleculePicker.SelectedIndex == 1 ? MoleculeKeys.Ibuprofen : MoleculeKeys.Paracetamol;

        protected override DoseEntry BuildDose(double amount, DateTime when)
            => new(when, amount, UserPreferences.GetWeightKg(), SelectedMoleculeKey);

        protected override EffectLevel ResolveEffectLevel(double percent)
        {
            if (percent >= Calculator.StrongPercent) return EffectLevel.Strong;
            if (percent >= Calculator.ModeratePercent) return EffectLevel.Moderate;
            if (percent >= Calculator.LightPercent) return EffectLevel.Light;
            return EffectLevel.None;
        }

        protected override void UpdateMoleculeSpecificConcentrationInfo(
            List<DoseEntry> doses, DateTime currentTime, double percent)
        {
            base.UpdateMoleculeSpecificConcentrationInfo(doses, currentTime, percent);

            Panel.HeadlineText = $"Effet analgésique estimé : {percent:0} %";
            Panel.HeadlineDetailText = "Combinaison de Bliss des deux molécules, bornée à 100 %.";

            DateTime? end = Calculator.PredictEffectEndTime(doses, currentTime);
            Panel.EffectPrediction.Text = end.HasValue && end.Value > currentTime
                ? $"Effet négligeable vers {end.Value:HH\\hmm}."
                : "Effet actuellement négligeable.";
            Panel.EffectPrediction.IsVisible = true;
        }
    }
}
