using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            UserPreferences.HydrateProfile();

            // La migration des fichiers de données part dès le lancement ; les pages
            // l'attendent avant de lire quoi que ce soit.
            _ = StartupAsync();
        }

        /// <summary>
        /// Migration d'abord, sauvegarde sortante ensuite — jamais l'inverse : il
        /// serait absurde d'envoyer au loin des données qu'on s'apprête à réparer.
        /// </summary>
        private static async Task StartupAsync()
        {
            await DataMigrationService.EnsureMigratedAsync();
            await new OutboundBackupService().RunIfDueAsync();
        }

        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(new MainTabsPage()) { Title = "Molecule Tracker" };
    }
}
