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

        private readonly string[] _molecules = new[] { "caffeine", "bromazepam", "paracetamol", "ibuprofene", "alcool" };

        private readonly Dictionary<string, ObservableRangeCollection<ChartDataPoint>> _data24h = new();
        private readonly Dictionary<string, ObservableRangeCollection<ChartDataPoint>> _data7d = new();

        public ObservableCollection<AverageEntry> Averages7j { get; } = new();
        public ObservableCollection<AverageEntry> Averages24h { get; } = new();

        public ChargePage()
        {
            InitializeComponent();
            BindingContext = this;

            foreach (var m in _molecules)
            {
                _data24h[m] = new ObservableRangeCollection<ChartDataPoint>();
                _data7d[m] = new ObservableRangeCollection<ChartDataPoint>();
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            var now = DateTime.Now;

            await LoadPeriodAsync(now.AddDays(-1), now, TimeSpan.FromHours(1), _data24h);
            await LoadPeriodAsync(now.AddDays(-7), now, TimeSpan.FromHours(6), _data7d);

            UpdateChart(Chart24h, _data24h);
            UpdateChart(Chart7d, _data7d);

            Averages24h.Clear();
            Averages7j.Clear();

            foreach (var m in _molecules)
            {
                double avg = await _service.GetAverageLoadPerDay(m, 1);
                Averages24h.Add(new AverageEntry { MoleculeName = ToDisplayName(m), Average = avg });
            }
            foreach (var m in _molecules)
            {
                double avg = await _service.GetAverageLoadPerDay(m, 7);
                Averages7j.Add(new AverageEntry { MoleculeName = ToDisplayName(m), Average = avg });
            }

            AverageCollection24h.ItemsSource = Averages24h;
            AverageCollection7j.ItemsSource = Averages7j;
        }

        private async Task LoadPeriodAsync(DateTime from, DateTime to, TimeSpan interval,
            Dictionary<string, ObservableRangeCollection<ChartDataPoint>> target)
        {
            foreach (var m in _molecules)
            {
                var data = await _service.GetSnapshots(m, from, to, interval);
                target[m].ReplaceRange(data.Select(s => new ChartDataPoint(s.Timestamp, s.ResidualAmount)));
            }
        }

        private void UpdateChart(SfCartesianChart chart, Dictionary<string, ObservableRangeCollection<ChartDataPoint>> source)
        {
            chart.Series.Clear();
            foreach (var m in _molecules)
            {
                chart.Series.Add(new SplineSeries
                {
                    ItemsSource = source[m],
                    XBindingPath = "Time",
                    YBindingPath = "Concentration",
                    StrokeWidth = 1.5,
                    Label = ToDisplayName(m)
                });
            }
        }

        private static string ToDisplayName(string key) => key switch
        {
            "caffeine" => "Caféine",
            "bromazepam" => "Bromazépam",
            "paracetamol" => "Paracétamol",
            "ibuprofene" or "ibuprofen" => "Ibuprofène",
            "alcool" => "Alcool",
            _ => key
        };

        public class AverageEntry
        {
            public string MoleculeName { get; set; } = string.Empty;
            public double Average { get; set; }
        }
    }
}