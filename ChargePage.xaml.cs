using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using MoleculeEfficienceTracker.Core.Extensions;
using Syncfusion.Maui.Charts;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MoleculeEfficienceTracker
{
    public partial class ChargePage : ContentPage
    {
        private readonly IResidualLoadService _service = new ResidualLoadService();

        public ObservableRangeCollection<ChartDataPoint> CaffeineData { get; } = new();
        public ObservableRangeCollection<ChartDataPoint> BromazepamData { get; } = new();
        public ObservableCollection<AverageEntry> Averages { get; } = new();

        public ChargePage()
        {
            InitializeComponent();
            BindingContext = this;
            PeriodPicker.SelectedIndex = 1; // default 7j
            PeriodPicker.SelectedIndexChanged += async (s, e) => await RefreshAsync();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            var days = PeriodPicker.SelectedIndex == 0 ? 1 : 7;
            var end = DateTime.Now;
            var start = end.AddDays(-days);
            TimeSpan interval = days == 1 ? TimeSpan.FromHours(1) : TimeSpan.FromHours(6);

            var caffeine = await _service.GetSnapshots("caffeine", start, end, interval);
            var broma = await _service.GetSnapshots("bromazepam", start, end, interval);

            CaffeineData.ReplaceRange(caffeine.Select(s => new ChartDataPoint(s.Timestamp, s.ResidualAmount)));
            BromazepamData.ReplaceRange(broma.Select(s => new ChartDataPoint(s.Timestamp, s.ResidualAmount)));

            LoadChart.Series.Clear();
            LoadChart.Series.Add(new SplineSeries { ItemsSource = CaffeineData, XBindingPath = "Time", YBindingPath = "Concentration", StrokeWidth = 1.5, Label="Caféine" });
            LoadChart.Series.Add(new SplineSeries { ItemsSource = BromazepamData, XBindingPath = "Time", YBindingPath = "Concentration", StrokeWidth = 1.5, Label="Bromazépam" });

            Averages.Clear();
            double avgCafe = await _service.GetAverageLoadPerDay("caffeine", days);
            double avgBroma = await _service.GetAverageLoadPerDay("bromazepam", days);
            Averages.Add(new AverageEntry { MoleculeName = "Caféine", Average = avgCafe });
            Averages.Add(new AverageEntry { MoleculeName = "Bromazépam", Average = avgBroma });
            AverageCollection.ItemsSource = Averages;
        }

        public class AverageEntry
        {
            public string MoleculeName { get; set; } = string.Empty;
            public double Average { get; set; }
        }
    }
}
