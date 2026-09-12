using Microsoft.Maui.Storage;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{

    public static class UserPreferences
    {
        private const string WeightKey = "user_weight_kg";
        private const string SexKey = "user_sex";
        private const string BedTimeKey = "bed_time_minutes";
        private const string SleepThresholdKey = "caffeine_sleep_threshold";
        private const string PresetsKey = "caffeine_presets_mg";
        private const string NotifyKey = "caffeine_cutoff_notification";

        private const double DefaultWeight = 72.0;

        /// <summary>Recopie les préférences dans le profil lu par les calculs.</summary>
        public static void HydrateProfile()
        {
            UserProfile.WeightKg = GetWeightKg();
            UserProfile.Sex = GetSex();
        }

        public static double GetWeightKg() => Preferences.Get(WeightKey, DefaultWeight);

        public static void SetWeightKg(double weightKg)
        {
            Preferences.Set(WeightKey, weightKg);
            UserProfile.WeightKg = weightKg;
        }

        /// <summary>
        /// Sexe retenu pour le coefficient de diffusion de l'alcool.
        ///
        /// L'ancienne version stockait la chaîne brute et comparait « homme » en
        /// minuscule à « Homme » capitalisé par le sélecteur : la comparaison
        /// échouait toujours, le sélecteur restait vide, et la page Paramètres
        /// refusait d'enregistrer un poids en accusant le poids.
        /// </summary>
        public static Sex GetSex()
        {
            string raw = Preferences.Get(SexKey, nameof(Sex.Male));
            return raw.Trim().ToLowerInvariant() switch
            {
                "femme" or "female" or "f" => Sex.Female,
                _ => Sex.Male
            };
        }

        public static void SetSex(Sex sex)
        {
            Preferences.Set(SexKey, sex.ToString());
            UserProfile.Sex = sex;
        }

        /// <summary>Heure de coucher visée, en minutes depuis minuit. 23 h par défaut.</summary>
        public static TimeSpan GetBedTime()
            => TimeSpan.FromMinutes(Preferences.Get(BedTimeKey, 23 * 60));

        public static void SetBedTime(TimeSpan bedTime)
            => Preferences.Set(BedTimeKey, (int)bedTime.TotalMinutes);

        /// <summary>Concentration de caféine tolérée au coucher, en mg/L.</summary>
        public static double GetSleepThreshold()
            => Preferences.Get(SleepThresholdKey, CaffeineCalculator.DEFAULT_SLEEP_THRESHOLD);

        public static void SetSleepThreshold(double threshold)
            => Preferences.Set(SleepThresholdKey, threshold);

        /// <summary>
        /// Doses proposées en accès direct sur l'écran caféine. Les valeurs par
        /// défaut sortent des données réelles : 80 mg couvre 70 % des prises
        /// enregistrées, 65 et 35 mg vingt de plus.
        /// </summary>
        public static double[] GetCaffeinePresets()
        {
            string raw = Preferences.Get(PresetsKey, "80;65;35");
            var parsed = raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => double.TryParse(s, System.Globalization.NumberStyles.Any,
                                                        System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0)
                            .Where(v => v > 0)
                            .ToArray();
            return parsed.Length > 0 ? parsed : new[] { 80.0, 65.0, 35.0 };
        }

        public static void SetCaffeinePresets(IEnumerable<double> presets)
            => Preferences.Set(PresetsKey, string.Join(';', presets.Select(p =>
                   p.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        public static bool GetCutoffNotificationEnabled() => Preferences.Get(NotifyKey, false);

        public static void SetCutoffNotificationEnabled(bool enabled) => Preferences.Set(NotifyKey, enabled);
    }
}
