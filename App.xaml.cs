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

        /// <summary>
        /// L'onglier vit dans une NavigationPage.
        ///
        /// Elle apporte deux choses qui manquaient : une barre de titre — les
        /// onglets touchaient jusqu'ici la barre d'état — et une destination pour
        /// les Réglages, qui occupaient un sixième onglet là où Material 3 en
        /// plafonne cinq.
        /// </summary>
        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(new NavigationPage(new MainTabsPage())) { Title = "Molecule Tracker" };
    }
}
