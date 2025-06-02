using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using System.Collections.ObjectModel;
using Syncfusion.Maui.Charts;
using Microsoft.Maui.Graphics;
using System.Text.Json; // Déjà présent
using CommunityToolkit.Maui.Storage; // <-- Ajouter pour FileSaver
using System.Text; // <-- Ajouter pour Encoding


namespace MoleculeEfficienceTracker
{
    public partial class MainPage : ContentPage
    {
        private readonly BromazepamCalculator calculator;
        private readonly DataPersistenceService persistenceService; // ✅ Nouveau service

        public ObservableCollection<DoseEntry> Doses { get; set; }
        public ObservableCollection<ChartDataPoint> ChartData { get; set; }

        private readonly IAlertService alertService;

        public BromazepamCalculator Calculator => calculator; // ✅ Exposer pour le binding

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
            // LoadDataAsync(); // Déplacé vers OnAppearing

            StartConcentrationTimer();
            // UpdateConcentrationDisplay(); // Appelé dans LoadDataAsync
            // UpdateChart(); // Appelé dans LoadDataAsync
            // UpdateDoseAnnotations(); // Appelé dans LoadDataAsync après le chargement des données
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDataAsync();
        }

        // ✅ Nouvelle méthode pour charger les données
        private async Task LoadDataAsync() // Changé de async void à async Task
        {
            var savedDoses = await persistenceService.LoadDosesAsync();

            Doses.Clear();
            foreach (var dose in savedDoses.OrderByDescending(d => d.TimeTaken))
            {
                Doses.Add(dose);
            }

            // Mettre à jour l'affichage
            UpdateConcentrationDisplay();
            UpdateChart();
            UpdateDoseAnnotations();
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

            UpdateDoseAnnotations();
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

            UpdateDoseAnnotations();
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

            // Étendre la période pour charger plus de données, par exemple 7 jours en arrière et 1 jour en avant.
            var startTime = DateTime.Now.AddDays(-7);
            var endTime = DateTime.Now.AddDays(1);
            var currentTime = DateTime.Now;

            // Augmenter le nombre de points pour une meilleure résolution sur la période étendue.
            // Original: 200 points pour 36h. Nouvelle période: ~8 jours (192h).
            // Suggestion: 800 points (environ 4 points/heure).
            var numberOfPoints = 800;
            var graphPoints = calculator.GenerateGraph(Doses.ToList(), startTime, endTime, numberOfPoints);

            foreach (var point in graphPoints)
            {
                ChartData.Add(new ChartDataPoint(point.Time, point.Concentration));
            }

            // Configurer l'axe X pour la vue initiale et la plage de défilement totale
            if (ConcentrationChart?.XAxes?.FirstOrDefault() is DateTimeAxis xAxis)
            {
                // Définir la plage totale de données que l'axe peut afficher (pour le défilement)
                xAxis.Minimum = startTime;
                xAxis.Maximum = endTime;

                // Définir la vue initiale visible, par exemple les dernières 24 heures jusqu'aux prochaines 12 heures.
                // L'utilisateur pourra ensuite défiler pour voir le reste des données (jusqu'à -7 jours).
                var initialVisibleStartTime = DateTime.Now.AddHours(-24);
                var initialVisibleEndTime = DateTime.Now.AddHours(12);

                // xAxis.VisibleMinimum = initialVisibleStartTime;
                // xAxis.VisibleMaximum = initialVisibleEndTime;
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
                var defaultFileName = $"bromazepam_export_{DateTime.Now:yyyyMMdd_HHmm}.json";

                // Convertir la chaîne JSON en flux (Stream)
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

                // Utiliser FileSaver pour permettre à l'utilisateur de choisir l'emplacement et le nom du fichier
                // Cela ouvrira une boîte de dialogue "Enregistrer sous..."
                var fileSaverResult = await FileSaver.Default.SaveAsync(defaultFileName, stream, CancellationToken.None);

                if (fileSaverResult.IsSuccessful)
                {
                    // Le chemin peut être null sur certaines plateformes ou si l'utilisateur annule.
                    // fileSaverResult.FilePath contient le chemin complet où le fichier a été sauvegardé.
                    await DisplayAlert("✅", $"Fichier sauvegardé." + (string.IsNullOrWhiteSpace(fileSaverResult.FilePath) ? "" : $"\nChemin: {fileSaverResult.FilePath}"), "OK");
                }
                else
                {
                    string errorMessage = "Sauvegarde annulée ou échouée.";
                    if (fileSaverResult.Exception != null) // Vérifier si une exception spécifique a été levée
                    {
                        errorMessage += $"\nErreur: {fileSaverResult.Exception.Message}";
                    }
                    await DisplayAlert("❌", errorMessage, "OK");
                }
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

            UpdateDoseAnnotations();
        }

        private void UpdateDoseAnnotations()
        {
            ConcentrationChart?.Annotations.Clear();

            // Ligne verticale "Maintenant"
            var now = DateTime.Now;
            var nowLine = new VerticalLineAnnotation
            {
                CoordinateUnit = ChartCoordinateUnit.Axis,
                X1 = now,
                Stroke = new SolidColorBrush(Colors.Red),
                StrokeWidth = 2,
                // Optionnel : Afficher un label "Maintenant"
                Text = "Maintenant",
                LabelStyle = new ChartAnnotationLabelStyle
                {
                    TextColor = Colors.Red,
                    FontSize = 12,
                    Margin = new Thickness(4, 0, 0, 0)
                }
            };
            ConcentrationChart?.Annotations.Add(nowLine);

            // Annotations fixes pour chaque dose
            foreach (var dose in Doses)
            {
                var annotation = new TextAnnotation
                {
                    CoordinateUnit = ChartCoordinateUnit.Axis,
                    X1 = dose.TimeTaken,
                    Y1 = GetConcentrationAtTime(dose.TimeTaken), // ou dose.DoseMg si vous préférez
                    Text = $"{dose.DoseMg}mg💊\n🕐:{dose.TimeTaken:HH:mm}"
                };
                ConcentrationChart?.Annotations.Add(annotation);
            }
        }

        // Méthode utilitaire pour obtenir la concentration au moment de la dose (optionnel)
        private double GetConcentrationAtTime(DateTime time)
        {
            // Si vos points du graphique sont dans ChartData, trouvez la concentration correspondante
            var point = ChartData?.FirstOrDefault(p => p.Time == time);
            return point?.Concentration ?? 0;
        }
        

    }
}
