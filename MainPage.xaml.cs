using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using System.Collections.ObjectModel;
using Syncfusion.Maui.Charts;
using Microsoft.Maui.Graphics;
using CommunityToolkit.Mvvm.Collections;
using MoleculeEfficienceTracker.Core.Extensions;

namespace MoleculeEfficienceTracker
{
    public partial class MainPage : ContentPage
    {
        private readonly BromazepamCalculator calculator;
        public ObservableCollection<DoseEntry> Doses { get; set; }
        public ObservableRangeCollection<ChartDataPoint> ChartData { get; set; }

        public MainPage()
        {
            InitializeComponent();
            calculator = new BromazepamCalculator();
            Doses = new ObservableCollection<DoseEntry>();
            ChartData = new ObservableRangeCollection<ChartDataPoint>();

            BindingContext = this;

            // Initialiser les contrôles
            DatePicker.Date = DateTime.Today;
            TimePicker.Time = DateTime.Now.TimeOfDay;

            // Mise à jour automatique toutes les minutes
            StartConcentrationTimer();
            UpdateConcentrationDisplay();
            UpdateChart();
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
                UpdateChart(); // Mise à jour du graphique

                await DisplayAlert("✅", $"Dose de {doseMg}mg ajoutée pour {dateTime:dd/MM HH:mm}", "OK");
            }
            else
            {
                await DisplayAlert("❌", "Veuillez entrer une dose valide (nombre positif)", "OK");
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
                        UpdateChart(); // Mise à jour du graphique
                    }
                }
            }
        }

        private void UpdateConcentrationDisplay()
        {
            var currentTime = DateTime.Now;
            var concentration = calculator.CalculateTotalConcentration(Doses.ToList(), currentTime);

            ConcentrationLabel.Text = $"{concentration:F2} unités";
            LastUpdateLabel.Text = $"Mise à jour: {currentTime:HH:mm:ss}";
        }

        private void UpdateChart()
        {
            if (!Doses.Any())
            {
                ChartData.Clear();
                return;
            }

            var startTime = DateTime.Now.AddHours(-24);
            var endTime = DateTime.Now.AddHours(12);
            var currentTime = DateTime.Now;

            var graphPoints = calculator.GenerateGraph(Doses.ToList(), startTime, endTime, 200);

            var newData = graphPoints.Select(p => new ChartDataPoint(p.Time, p.Concentration)).ToList();


            ChartData.ReplaceRange(newData);

            AddCurrentTimeAnnotation(currentTime);
        }

        private void AddCurrentTimeAnnotation(DateTime currentTime)
        {
            // Effacer les annotations existantes
            ConcentrationChart.Annotations.Clear();

            // Ajouter une ligne verticale pour "maintenant"
            var annotation = new VerticalLineAnnotation // ✅ Pas de "chart:" en C#
            {
                X1 = currentTime,
                Stroke = Brush.Red,
                StrokeWidth = 2,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                Text = "Maintenant",
                LabelStyle = new ChartAnnotationLabelStyle
                {
                    FontSize = 12,
                    TextColor = Colors.Red,
                    Background = Brush.White
                }
            };

            ConcentrationChart.Annotations.Add(annotation);
        }


        private void StartConcentrationTimer()
        {
            var timer = Application.Current.Dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMinutes(1);
            timer.Tick += (s, e) =>
            {
                UpdateConcentrationDisplay();
                UpdateChart(); // Mise à jour du graphique aussi
            };
            timer.Start();
        }
    }
}
