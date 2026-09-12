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
            _ = DataMigrationService.EnsureMigratedAsync();
        }

        protected override Window CreateWindow(IActivationState? activationState)
            => new Window(new MainTabsPage()) { Title = "Molecule Tracker" };
    }
}
