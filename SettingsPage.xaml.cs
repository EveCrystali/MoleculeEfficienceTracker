using System.Globalization;
using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    public partial class SettingsPage : ContentPage
    {
        private readonly CaffeineNotificationService _notifications;

        public SettingsPage()
        {
            InitializeComponent();
            _notifications = ServiceLocator.GetOptional<CaffeineNotificationService>() ?? new CaffeineNotificationService();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            WeightEntry.Text = UserPreferences.GetWeightKg().ToString("0.#", CultureInfo.CurrentCulture);

            // Le sélecteur ne se pré-remplissait jamais : la préférence rendait
            // « homme » en minuscule quand les entrées étaient « Homme ». La
            // comparaison passe désormais par une énumération.
            SexPicker.SelectedIndex = UserPreferences.GetSex() == Sex.Female ? 1 : 0;

            BedTimePicker.Time = UserPreferences.GetBedTime();
            SleepThresholdEntry.Text = UserPreferences.GetSleepThreshold().ToString("0.##", CultureInfo.CurrentCulture);
            PresetsEntry.Text = string.Join(';', UserPreferences.GetCaffeinePresets().Select(p => p.ToString("0.##", CultureInfo.InvariantCulture)));
            NotificationSwitch.IsToggled = UserPreferences.GetCutoffNotificationEnabled();

            BackupEndpointEntry.Text = OutboundBackupService.GetEndpoint();
            RefreshBackupStatus();

            MigrationReport? report = DataMigrationService.LastReport;
            MigrationLabel.Text = report is null
                ? "Aucune migration n'a été nécessaire au dernier démarrage."
                : $"Dernière migration : {report}";
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            // Chaque champ dit ce qui ne va pas. L'ancienne version répondait
            // « Entrez un poids valide » alors que c'était le sexe non sélectionné
            // qui bloquait l'enregistrement.
            if (!TryReadNumber(WeightEntry.Text, out double weight) || weight <= 20 || weight > 350)
            {
                await DisplayAlertAsync("Poids", "Entrez un poids entre 20 et 350 kg.", "OK");
                return;
            }

            if (!TryReadNumber(SleepThresholdEntry.Text, out double threshold) || threshold <= 0 || threshold > 20)
            {
                await DisplayAlertAsync("Seuil de sommeil", "Entrez une concentration entre 0 et 20 mg/L.", "OK");
                return;
            }

            double[] presets = (PresetsEntry.Text ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => TryReadNumber(s, out double v) ? v : 0)
                .Where(v => v > 0)
                .ToArray();

            if (presets.Length == 0)
            {
                await DisplayAlertAsync("Doses rapides", "Indiquez au moins une dose, par exemple 80;65;35.", "OK");
                return;
            }

            UserPreferences.SetWeightKg(weight);
            UserPreferences.SetSex(SexPicker.SelectedIndex == 1 ? Sex.Female : Sex.Male);
            UserPreferences.SetBedTime(BedTimePicker.Time ?? UserPreferences.GetBedTime());
            UserPreferences.SetSleepThreshold(threshold);
            UserPreferences.SetCaffeinePresets(presets);

            string endpoint = (BackupEndpointEntry.Text ?? string.Empty).Trim();
            if (endpoint.Length > 0 && !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
            {
                await DisplayAlertAsync("Sauvegarde", "L'adresse de dépôt n'est pas une URL valide.", "OK");
                return;
            }
            OutboundBackupService.SetEndpoint(endpoint);

            bool wantsNotification = NotificationSwitch.IsToggled;
            if (wantsNotification)
            {
                bool granted = await _notifications.RequestPermissionAsync();
                if (!granted)
                {
                    wantsNotification = false;
                    NotificationSwitch.IsToggled = false;
                    await DisplayAlertAsync("Rappel", "Android a refusé les notifications. Le rappel reste désactivé.", "OK");
                }
            }

            UserPreferences.SetCutoffNotificationEnabled(wantsNotification);
            if (!wantsNotification) _notifications.Cancel();

            await DisplayAlertAsync("Enregistré", "Les réglages sont pris en compte. Le poids ne modifie pas les prises déjà enregistrées.", "OK");
        }

        private void RefreshBackupStatus()
        {
            if (!OutboundBackupService.IsConfigured)
            {
                BackupStatusLabel.Text = "Désactivée.";
                return;
            }

            DateTime? last = OutboundBackupService.GetLastRun();
            BackupStatusLabel.Text = last is null
                ? "Configurée, aucun envoi réussi pour l'instant."
                : $"Dernier envoi réussi le {last.Value:dd/MM} à {last.Value:HH\\hmm}.";
        }

        private async void OnBackupNowClicked(object sender, EventArgs e)
        {
            string endpoint = (BackupEndpointEntry.Text ?? string.Empty).Trim();
            if (endpoint.Length == 0)
            {
                await DisplayAlertAsync("Sauvegarde", "Indiquez d'abord une adresse de dépôt.", "OK");
                return;
            }

            OutboundBackupService.SetEndpoint(endpoint);

            var service = ServiceLocator.GetOptional<OutboundBackupService>() ?? new OutboundBackupService();
            bool sent = await service.SendAsync();

            RefreshBackupStatus();
            await DisplayAlertAsync(
                sent ? "Sauvegarde envoyée" : "Envoi échoué",
                sent
                    ? "Les données ont été déposées."
                    : "Le dépôt n'a pas répondu. Vérifiez l'adresse et l'accès au tailnet.",
                "OK");
        }

        /// <summary>
        /// Relit un fichier de prises.
        ///
        /// C'est la porte de retour qui manquait : l'application savait exporter
        /// depuis l'origine, jamais relire, et un changement d'identifiant de paquet
        /// — Android y voit alors une autre application, avec un autre répertoire de
        /// données — laisse l'historique intact mais hors de portée.
        /// </summary>
        private async void OnImportClicked(object sender, EventArgs e)
        {
            try
            {
                ImportButton.IsEnabled = false;

                ImportReport? report = await new DataImportService().PickAndImportAsync();
                if (report is null) return;

                ImportStatusLabel.Text = report.ToString();
                ImportStatusLabel.IsVisible = true;

                await DisplayAlertAsync("Import terminé", report.ToString(), "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Import impossible", ex.Message, "OK");
            }
            finally
            {
                ImportButton.IsEnabled = true;
            }
        }

        private static bool TryReadNumber(string? raw, out double value)
            => double.TryParse((raw ?? string.Empty).Trim().Replace(',', '.'),
                               NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
