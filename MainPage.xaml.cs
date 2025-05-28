using BromazepamTracker.Models;
using BromazepamTracker.Services;
using System.Collections.ObjectModel;

namespace BromazepamTracker;

public partial class MainPage : ContentPage
{
    private readonly BromazepamCalculator calculator;
    public ObservableCollection<DoseEntry> Doses { get; set; }

    public MainPage()
    {
        InitializeComponent();
        calculator = new BromazepamCalculator();
        Doses = new ObservableCollection<DoseEntry>();
        BindingContext = this;

        // Mise à jour automatique toutes les minutes
        StartConcentrationTimer();
    }

    private async void OnAddDoseClicked(object sender, EventArgs e)
    {
        if (double.TryParse(DoseEntry.Text, out double doseMg))
        {
            var selectedDate = DatePicker.Date;
            var selectedTime = TimePicker.Time;
            var dateTime = selectedDate.Add(selectedTime);

            var dose = new DoseEntry
            {
                DoseMg = doseMg,
                TimeTaken = dateTime
            };

            Doses.Insert(0, dose); // Ajouter en première position
            DoseEntry.Text = "";

            UpdateConcentrationDisplay();

            await DisplayAlert("✅", $"Dose de {doseMg}mg ajoutée", "OK");
        }
        else
        {
            await DisplayAlert("❌", "Veuillez entrer une dose valide", "OK");
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
                }
            }
        }
    }

    private void UpdateConcentrationDisplay()
    {
        var currentTime = DateTime.Now;
        var concentration = calculator.CalculateTotalConcentration(Doses.ToList(), currentTime);

        ConcentrationLabel.Text = $"{concentration:F2} unités";
        LastUpdateLabel.Text = $"Mise à jour: {currentTime:HH:mm}";
    }

    private void StartConcentrationTimer()
    {
        var timer = Application.Current.Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMinutes(1);
        timer.Tick += (s, e) => UpdateConcentrationDisplay();
        timer.Start();
    }
}
