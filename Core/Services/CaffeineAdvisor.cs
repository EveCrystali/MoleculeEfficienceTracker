using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// La phrase qui répond, pour la caféine.
    ///
    /// Elle vivait dans l'écran Café. L'écran Aujourd'hui la redemandant mot pour
    /// mot, elle vit ici : deux écrans, une seule règle, et le jour où le seuil de
    /// sommeil change, il ne change qu'à un endroit.
    ///
    /// C'est elle qui porte la décision. La courbe reste dessous : elle documente,
    /// elle ne tranche pas — et à sept heures du matin, personne ne lit une courbe.
    /// </summary>
    public static class CaffeineAdvisor
    {
        public readonly record struct Advice(
            string Headline,
            string Detail,
            DateTime BedTime,
            DateTime? Cutoff,
            double ConcentrationAtBedTime,
            double PresetMg);

        public static Advice Build(CaffeineCalculator calculator, List<DoseEntry> doses, DateTime now)
        {
            DateTime bedTime = CaffeineNotificationService.NextBedTime(now);
            double threshold = UserPreferences.GetSleepThreshold();
            double weight = UserPreferences.GetWeightKg();
            double preset = UserPreferences.GetCaffeinePresets().FirstOrDefault(CaffeineCalculator.MG_PER_UNIT);

            double atBedTime = calculator.CalculateTotalConcentration(doses, bedTime);
            DateTime? cutoff = calculator.LatestIntakeTimeBefore(doses, bedTime, preset, weight, now, threshold);

            string headline, detail;

            if (cutoff is null)
            {
                headline = $"Le café déjà bu suffit à dépasser {threshold:0.#} mg/L à {bedTime:HH\\hmm}.";
                detail = $"Estimation au coucher : {atBedTime:0.##} mg/L. Un café de plus repousserait l'endormissement.";
            }
            else if (cutoff.Value <= now.AddMinutes(1))
            {
                headline = $"C'est le moment ou jamais pour un {preset:0} mg.";
                detail = $"Au-delà de maintenant, il resterait plus de {threshold:0.#} mg/L à {bedTime:HH\\hmm}.";
            }
            else
            {
                headline = $"Dernier {preset:0} mg avant {cutoff.Value:HH\\hmm} pour dormir à {bedTime:HH\\hmm}.";
                detail = $"Sans autre café, il resterait {atBedTime:0.##} mg/L au coucher — seuil retenu {threshold:0.#} mg/L.";
            }

            return new Advice(headline, detail, bedTime, cutoff, atBedTime, preset);
        }
    }
}
