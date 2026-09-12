namespace MoleculeEfficienceTracker
{
    public partial class MainTabsPage : TabbedPage
    {
        public MainTabsPage()
        {
            InitializeComponent();
        }

        /// <summary>Ouvre l'onglet caféine, par exemple après un ajout rapide.</summary>
        public void SelectCaffeineTab()
        {
            if (Children.Count > 0)
                CurrentPage = Children[0];
        }
    }
}
