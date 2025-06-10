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
            SexPicker.SelectedItem = UserPreferences.GetSex();
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (double.TryParse(WeightEntry.Text, out double weight) && weight > 0 &&   SexPicker.SelectedItem is string sex)
            {
                UserPreferences.SetWeightKg(weight);
                UserPreferences.SetSex(sex);
                await DisplayAlert("✅", "Poids et sexe enregistrés", "OK");
            }
            else
            {
                await DisplayAlert("❌", "Entrez un poids valide", "OK");
            }
        }
    }
}
