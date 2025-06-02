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
            List<DoseEntry> savedDoses = await persistenceService.LoadDosesAsync();

            Doses.Clear();
            foreach (DoseEntry? dose in savedDoses.OrderByDescending(d => d.TimeTaken))
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
                DateTime selectedDate = DatePicker.Date;
                TimeSpan selectedTime = TimePicker.Time;
                DateTime dateTime = selectedDate.Add(selectedTime);

                DoseEntry dose = new DoseEntry(dateTime, doseMg);

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
                DoseEntry? dose = Doses.FirstOrDefault(d => d.Id == doseId);
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
            DateTime currentTime = DateTime.Now;
            double concentration = calculator.CalculateTotalConcentration(Doses.ToList(), currentTime);

            ConcentrationLabel.Text = $"{concentration:F2} unités";
            LastUpdateLabel.Text = $"Mise à jour: {currentTime:HH:mm:ss}";
        }

        private void UpdateChart()
        {
            ChartData.Clear();

            if (!Doses.Any())
            {
                // Optionnel: Configurer l'axe même sans données pour avoir une vue par défaut
                DateTime nowForEmptyChart = DateTime.Now;
                if (ConcentrationChart?.XAxes?.FirstOrDefault() is DateTimeAxis emptyXAxis)
                {
                    emptyXAxis.Minimum = nowForEmptyChart.AddDays(-7);
                    emptyXAxis.Maximum = nowForEmptyChart.AddDays(7);
                    emptyXAxis.ZoomFactor = (24.0 / (14.0 * 24.0)); // 24h visible sur 14 jours
                    emptyXAxis.ZoomPosition = ( (emptyXAxis.Maximum.Value - emptyXAxis.Minimum.Value).TotalHours * (1 - emptyXAxis.ZoomFactor) ) > 0 ?
                                              (nowForEmptyChart.AddHours(-12) - emptyXAxis.Minimum.Value).TotalHours /
                                              ( (emptyXAxis.Maximum.Value - emptyXAxis.Minimum.Value).TotalHours * (1 - emptyXAxis.ZoomFactor) )
                                              : 0;
                    emptyXAxis.ZoomPosition = Math.Max(0.0, Math.Min(1.0, emptyXAxis.ZoomPosition));
                }
                return;
            }

            DateTime currentTime = DateTime.Now;

            // 1. Plage de calcul des données du graphique
            DateTime graphDataStartTime = currentTime.AddDays(-7);
            DateTime graphDataEndTime = currentTime.AddDays(3);
            int numberOfPoints = 10 * 24 * 2; // 10 jours, 2 points par heure

            List<(DateTime Time, double Concentration)> graphPoints = calculator.GenerateGraph(Doses.ToList(), graphDataStartTime, graphDataEndTime, numberOfPoints);

            foreach ((DateTime Time, double Concentration) point in graphPoints)
            {
                ChartData.Add(new ChartDataPoint(point.Time, point.Concentration));
            }

            // Configurer l'axe X pour la vue initiale et la plage de défilement totale
            if (ConcentrationChart?.XAxes?.FirstOrDefault() is DateTimeAxis xAxis)
            {
                // 2. Plage totale de l'axe X (pour le défilement)
                xAxis.Minimum = graphDataStartTime;
                xAxis.Maximum = graphDataEndTime;

                // 3. Vue initiale visible sur l'axe X
                DateTime initialVisibleStartTime = currentTime.AddHours(-12);
                DateTime initialVisibleEndTime = currentTime.AddHours(24);

                double totalAxisRangeInHours = (graphDataEndTime - graphDataStartTime).TotalHours; 
                double desiredVisibleDurationInHours = (initialVisibleEndTime - initialVisibleStartTime).TotalHours;

                if (totalAxisRangeInHours > 0)
                {
                    xAxis.ZoomFactor = desiredVisibleDurationInHours / totalAxisRangeInHours;
                    // S'assurer que ZoomFactor est dans les limites valides (par exemple > 0 et <= 1)
                    // Utiliser une petite valeur au lieu de 0 pour éviter les problèmes
                    xAxis.ZoomFactor = Math.Max(0.00001, Math.Min(1.0, xAxis.ZoomFactor)); 

                    // Correction : centrer la vue autour de "now" sur la plage totale
                    double desiredStartOffsetInHours = (initialVisibleStartTime - graphDataStartTime).TotalHours;
                    xAxis.ZoomPosition = desiredStartOffsetInHours / totalAxisRangeInHours;
                    xAxis.ZoomPosition = Math.Max(0.0, Math.Min(1.0 - xAxis.ZoomFactor, xAxis.ZoomPosition));
                }
                else
                {
                    // Cas où la plage totale est nulle ou négative, comportement par défaut
                    // Ne devrait pas arriver avec les définitions actuelles de graphDataStartTime/EndTime
                    xAxis.ZoomFactor = 1;
                    xAxis.ZoomPosition = 0;
                }
            }
        }

        private void StartConcentrationTimer()
        {
            IDispatcherTimer timer = Application.Current.Dispatcher.CreateTimer();
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
                List<DoseEntry> doses = Doses.ToList();
                if (!doses.Any())
                {
                    await DisplayAlert("📂", "Aucune donnée à exporter", "OK");
                    return;
                }

                string json = JsonSerializer.Serialize(doses, new JsonSerializerOptions { WriteIndented = true });
                string defaultFileName = $"bromazepam_export_{DateTime.Now:yyyyMMdd_HHmm}.json";

                // Convertir la chaîne JSON en flux (Stream)
                using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

                // Utiliser FileSaver pour permettre à l'utilisateur de choisir l'emplacement et le nom du fichier
                // Cela ouvrira une boîte de dialogue "Enregistrer sous..."
                FileSaverResult fileSaverResult = await FileSaver.Default.SaveAsync(defaultFileName, stream, CancellationToken.None);

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
            DateTime now = DateTime.Now;
            VerticalLineAnnotation nowLine = new VerticalLineAnnotation
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
            foreach (DoseEntry dose in Doses)
            {
                TextAnnotation annotation = new TextAnnotation
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
            ChartDataPoint? point = ChartData?.FirstOrDefault(p => p.Time == time);
            return point?.Concentration ?? 0;
        }
        

    }
}
