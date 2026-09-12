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
        protected override string HelperText =>
            "Paracétamol 500 ou 1000 mg · ibuprofène 200 ou 400 mg.\n" +
            "Les deux effets se combinent par indépendance de Bliss, bornée à 100 %.";
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

        /// <summary>L'axe porte un pourcentage : il s'arrête à cent.</summary>
        protected override double? YAxisMaximumCap => 100;

        protected override string FormatConcentration(double percent) => $"{percent:0} %";

        /// <summary>
        /// La grandeur affichée est un effet, pas une quantité : annoncer « 42 mg
        /// encore présents » n'aurait ici aucun sens. La ligne dit donc d'où vient
        /// l'effet.
        /// </summary>
        protected override string? BuildAmountDetail(List<DoseEntry> doses, DateTime currentTime, double amount)
        {
            DoseEntry? last = doses.OrderByDescending(d => d.TimeTaken)
                                   .FirstOrDefault(d => d.TimeTaken <= currentTime);

            if (last is null) return null;

            double hours = (currentTime - last.TimeTaken).TotalHours;
            string ago = hours < 1
                ? $"il y a {hours * 60:0} min"
                : $"il y a {(int)hours} h {(hours - (int)hours) * 60:00}";

            return $"Dernière prise {ago} · {MoleculeKeys.DisplayName(last.MoleculeKey)} {last.DoseMg:0.#} mg";
        }

        protected override string EmptyChartMessage
            => "La courbe apparaîtra dès la première prise enregistrée.";

        protected override void UpdateMoleculeSpecificConcentrationInfo(
            List<DoseEntry> doses, DateTime currentTime, double percent)
        {
            base.UpdateMoleculeSpecificConcentrationInfo(doses, currentTime, percent);

            // La phrase ne répète plus la valeur, écrite en grand juste dessous, et
            // la mécanique du modèle descend dans l'aide du formulaire.
            bool para = doses.Any(d => d.MoleculeKey == MoleculeKeys.Paracetamol && IsActive(d, currentTime));
            bool ibu = doses.Any(d => d.MoleculeKey == MoleculeKeys.Ibuprofen && IsActive(d, currentTime));

            Panel.HeadlineText = (para, ibu) switch
            {
                (true, true) => "Paracétamol et ibuprofène agissent ensemble.",
                (true, false) => "Paracétamol seul en action.",
                (false, true) => "Ibuprofène seul en action.",
                _ => "Aucun antalgique en action."
            };

            Panel.HeadlineDetailText = string.Empty;

            DateTime? end = Calculator.PredictEffectEndTime(doses, currentTime);
            Panel.EffectPrediction.Text = end.HasValue && end.Value > currentTime
                ? $"Effet négligeable vers {end.Value:HH\\hmm}."
                : "Effet actuellement négligeable.";
            Panel.EffectPrediction.IsVisible = true;
        }

        /// <summary>
        /// Une prise compte tant qu'elle contribue encore à l'effet. Le calculateur
        /// aiguille lui-même sur la bonne molécule d'après la clé de la prise.
        /// </summary>
        private bool IsActive(DoseEntry dose, DateTime at)
            => Calculator.CalculateSingleDoseConcentration(dose, at) > Calculator.NegligibleEffect;
    }
}
