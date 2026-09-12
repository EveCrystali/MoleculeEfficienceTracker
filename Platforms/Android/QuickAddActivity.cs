using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    /// <summary>
    /// Enregistre un café depuis l'écran d'accueil, sans ouvrir l'application.
    ///
    /// Quinze mois de données montrent six abandons successifs, et toujours au même
    /// endroit : la saisie coûtait quatre appuis et deux frappes. Ici, un seul.
    /// </summary>
    [Activity(
        Name = "com.evecrystali.moleculeefficiencetracker.QuickAddActivity",
        Exported = true,
        NoHistory = true,
        Theme = "@android:style/Theme.NoDisplay",
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class QuickAddActivity : Activity
    {
        protected override async void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            string message;
            try
            {
                UserPreferences.HydrateProfile();
                await DataMigrationService.EnsureMigratedAsync();

                double amount = UserPreferences.GetCaffeinePresets().FirstOrDefault(80.0);
                var store = new DataPersistenceService(MoleculeKeys.Caffeine);

                List<DoseEntry> doses = await store.LoadDosesAsync();
                DateTime now = DateTime.Now;

                doses.Insert(0, new DoseEntry(now, amount, UserPreferences.GetWeightKg(), MoleculeKeys.Caffeine));
                await store.SaveDosesAsync(doses.OrderByDescending(d => d.TimeTaken).ToList());

                message = BuildConfirmation(doses, amount, now);
            }
            catch (DoseDataCorruptedException)
            {
                // Ne rien écrire par-dessus un fichier qu'on ne sait pas relire.
                message = "Fichier de données illisible — ouvrez l'application.";
            }
            catch (Exception ex)
            {
                message = $"Échec de l'enregistrement : {ex.Message}";
            }

            Toast.MakeText(this, message, ToastLength.Long)?.Show();
            Finish();
        }

        /// <summary>Le retour immédiat porte la même réponse que l'écran principal.</summary>
        private static string BuildConfirmation(List<DoseEntry> doses, double amount, DateTime now)
        {
            var calculator = new CaffeineCalculator();
            DateTime bedTime = CaffeineNotificationService.NextBedTime(now);
            double atBedTime = calculator.CalculateTotalConcentration(doses, bedTime);

            return $"+{amount:0} mg enregistré. Estimation à {bedTime:HH\\hmm} : {atBedTime:0.##} mg/L.";
        }
    }
}
