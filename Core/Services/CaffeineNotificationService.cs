using System.Diagnostics;
using MoleculeEfficienceTracker.Core.Models;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Rappel local à l'heure limite du dernier café.
    ///
    /// C'est la seule chose qui parle à l'utilisateur quand l'application est
    /// fermée — et une application qu'on n'ouvre pas est une application qu'on
    /// abandonne. Le rappel reste désactivé tant qu'il n'a pas été demandé.
    /// </summary>
    public class CaffeineNotificationService
    {
        private const int CutoffNotificationId = 4201;

        private readonly CaffeineCalculator _calculator = new();

        public async Task<bool> RequestPermissionAsync()
        {
            try
            {
                if (await LocalNotificationCenter.Current.AreNotificationsEnabled(new NotificationPermission()))
                    return true;

                return await LocalNotificationCenter.Current.RequestNotificationPermission(new NotificationPermission());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Notification] permission indisponible : {ex.Message}");
                return false;
            }
        }

        public void Cancel()
        {
            try { LocalNotificationCenter.Current.Cancel(CutoffNotificationId); }
            catch (Exception ex) { Debug.WriteLine($"[Notification] annulation : {ex.Message}"); }
        }

        /// <summary>
        /// (Re)programme le rappel pour l'heure limite du jour. Sans heure limite
        /// atteignable, ou si le rappel est désactivé, tout rappel en attente est
        /// simplement annulé.
        /// </summary>
        public async Task ScheduleCutoffAsync(List<DoseEntry> doses, DateTime now)
        {
            Cancel();

            if (!UserPreferences.GetCutoffNotificationEnabled())
                return;

            DateTime bedTime = NextBedTime(now);
            double weight = UserPreferences.GetWeightKg();
            double threshold = UserPreferences.GetSleepThreshold();
            double preset = UserPreferences.GetCaffeinePresets().FirstOrDefault(80.0);

            DateTime? cutoff = _calculator.LatestIntakeTimeBefore(doses, bedTime, preset, weight, now, threshold);

            if (cutoff is null || cutoff <= now)
                return;

            try
            {
                var request = new NotificationRequest
                {
                    NotificationId = CutoffNotificationId,
                    Title = "Dernier café",
                    Description = $"Passé {cutoff:HH:mm}, un {preset:0} mg laisse trop de caféine pour {bedTime:HH:mm}.",
                    Schedule = new NotificationRequestSchedule { NotifyTime = cutoff.Value }
                };

                await LocalNotificationCenter.Current.Show(request);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Notification] programmation impossible : {ex.Message}");
            }
        }

        /// <summary>Prochain coucher : ce soir, ou demain s'il est déjà passé.</summary>
        public static DateTime NextBedTime(DateTime now)
        {
            DateTime bedTime = now.Date.Add(UserPreferences.GetBedTime());
            return bedTime <= now ? bedTime.AddDays(1) : bedTime;
        }
    }
}
