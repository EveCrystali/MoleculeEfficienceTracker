using Microsoft.Maui.Controls;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            WeightEntry.Text = UserPreferences.GetWeightKg().ToString("F1");
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (double.TryParse(WeightEntry.Text, out double weight) && weight > 0)
            {
                UserPreferences.SetWeightKg(weight);
                await DisplayAlert("✅", "Poids enregistré", "OK");
            }
            else
            {
                await DisplayAlert("❌", "Entrez un poids valide", "OK");
            }
        }
    }
}
