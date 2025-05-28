using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using System.Collections.ObjectModel;
using Syncfusion.Maui.Charts;
using Microsoft.Maui.Graphics;
using System.Text.Json;

namespace MoleculeEfficienceTracker
{
    public partial class MainPage : ContentPage
    {
        private readonly BromazepamCalculator calculator;
        private readonly DataPersistenceService persistenceService; // ✅ Nouveau service

        public ObservableCollection<DoseEntry> Doses { get; set; }
        public ObservableCollection<ChartDataPoint> ChartData { get; set; }

        private readonly IAlertService alertService;

        public MainPage()
        {
            InitializeComponent();
            calculator = new BromazepamCalculator();
            persistenceService = new DataPersistenceService();
            alertService = new AlertService();

            Doses = new ObservableCollection<DoseEntry>();
            ChartData = new ObservableCollection<ChartDataPoint>();
            BindingContext = this;

            DatePicker.Date = DateTime.Today;
            TimePicker.Time = DateTime.Now.TimeOfDay;

            // ✅ Charger les données au démarrage
            LoadDataAsync();

            StartConcentrationTimer();
            UpdateConcentrationDisplay();
            UpdateChart();
        }

        // ✅ Nouvelle méthode pour charger les données
        private async void LoadDataAsync()
        {
            var savedDoses = await persistenceService.LoadDosesAsync();

            // Vider et recharger la collection
            Doses.Clear();
            foreach (var dose in savedDoses.OrderByDescending(d => d.TimeTaken))
            {
                Doses.Add(dose);
            }

            // Mettre à jour l'affichage
            UpdateConcentrationDisplay();
            UpdateChart();
        }

        // ✅ Nouvelle méthode pour sauvegarder
        private async Task SaveDataAsync()
        {
            try
            {
                await persistenceService.SaveDosesAsync(Doses.ToList());
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌", $"Erreur lors de la sauvegarde: {ex.Message}", "OK");
            }
        }

        private async void OnAddDoseClicked(object sender, EventArgs e)
        {
            if (double.TryParse(DoseEntry.Text, out double doseMg) && doseMg > 0)
            {
                var selectedDate = DatePicker.Date;
                var selectedTime = TimePicker.Time;
                var dateTime = selectedDate.Add(selectedTime);

                var dose = new DoseEntry(dateTime, doseMg);

                Doses.Insert(0, dose);
                DoseEntry.Text = "";

                UpdateConcentrationDisplay();
                UpdateChart();

                // ✅ Sauvegarder automatiquement après ajout
                await SaveDataAsync();

                await alertService.ShowAlertAsync("✅", $"Dose de {doseMg}mg ajoutée pour {dateTime:dd/MM HH:mm}");
            }
            else
            {
                await alertService.ShowAlertAsync("❌", "Veuillez entrer une dose valide");
            }
        }

        private async void OnDeleteDoseClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string doseId)
            {
                var dose = Doses.FirstOrDefault(d => d.Id == doseId);
                if (dose != null)
                {
                    bool confirm = await DisplayAlert("Supprimer",
                        $"Supprimer la dose de {dose.DoseMg}mg du {dose.TimeTaken:dd/MM HH:mm} ?",
                        "Oui", "Non");

                    if (confirm)
                    {
                        Doses.Remove(dose);
                        UpdateConcentrationDisplay();
                        UpdateChart();

                        // ✅ Sauvegarder automatiquement après suppression
                        await SaveDataAsync();
                    }
                }
            }
        }

        // Tes autres méthodes restent identiques...
        private void UpdateConcentrationDisplay()
        {
            var currentTime = DateTime.Now;
            var concentration = calculator.CalculateTotalConcentration(Doses.ToList(), currentTime);

            ConcentrationLabel.Text = $"{concentration:F2} unités";
            LastUpdateLabel.Text = $"Mise à jour: {currentTime:HH:mm:ss}";
        }

        private void UpdateChart()
        {
            ChartData.Clear();

            if (!Doses.Any())
            {
                return;
            }

            var startTime = DateTime.Now.AddHours(-24);
            var endTime = DateTime.Now.AddHours(12);
            var currentTime = DateTime.Now;

            var graphPoints = calculator.GenerateGraph(Doses.ToList(), startTime, endTime, 200);

            foreach (var point in graphPoints)
            {
                ChartData.Add(new ChartDataPoint(point.Time, point.Concentration));
            }
        }

        private void StartConcentrationTimer()
        {
            var timer = Application.Current.Dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMinutes(1);
            timer.Tick += (s, e) =>
            {
                UpdateConcentrationDisplay();
                UpdateChart();
            };
            timer.Start();
        }

        private async void OnExportDataClicked(object sender, EventArgs e)
        {
            try
            {
                var doses = Doses.ToList();
                if (!doses.Any())
                {
                    await DisplayAlert("📂", "Aucune donnée à exporter", "OK");
                    return;
                }

                var json = JsonSerializer.Serialize(doses, new JsonSerializerOptions { WriteIndented = true });
                var fileName = $"bromazepam_export_{DateTime.Now:yyyyMMdd_HHmm}.json";
                var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

                await File.WriteAllTextAsync(filePath, json);

                await DisplayAlert("✅", $"Données exportées vers:\n{fileName}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌", $"Erreur d'export: {ex.Message}", "OK");
            }
        }

        private async void OnClearAllDataClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("⚠️ Attention",
                "Supprimer toutes les données ?\nCette action est irréversible.",
                "Oui", "Annuler");

            if (confirm)
            {
                Doses.Clear();
                await persistenceService.DeleteAllDataAsync();
                UpdateConcentrationDisplay();
                UpdateChart();

                await DisplayAlert("✅", "Toutes les données ont été supprimées", "OK");
            }
        }

    }
}
