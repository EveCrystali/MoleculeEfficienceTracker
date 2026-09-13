namespace MoleculeEfficienceTracker
{
    public partial class MainTabsPage : TabbedPage
    {
        public MainTabsPage()
        {
            InitializeComponent();
            UpdateTitle();
        }

        /// <summary>
        /// Le titre de la barre suit l'onglet courant.
        ///
        /// La barre appartient à la NavigationPage qui enveloppe cet onglier ; sans
        /// cette reprise, elle afficherait indéfiniment le titre du premier écran.
        /// </summary>
        protected override void OnCurrentPageChanged()
        {
            base.OnCurrentPageChanged();
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            string? title = CurrentPage?.Title;
            if (!string.IsNullOrWhiteSpace(title))
                Title = title;
        }

        private async void OnSettingsClicked(object? sender, EventArgs e)
            => await Navigation.PushAsync(new SettingsPage());

        /// <summary>Ouvre l'onglet caféine, par exemple après un ajout rapide.</summary>
        public void SelectCaffeineTab()
        {
            Page? caffeine = Children.FirstOrDefault(p => p is CaffeinePage);
            if (caffeine is not null) CurrentPage = caffeine;
        }
    }
}
