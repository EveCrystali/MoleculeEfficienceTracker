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
        private readonly Dictionary<string, ObservableRangeCollection<ChartDataPoint>> _dailyTotals7d = new();
        private readonly Dictionary<string, ObservableRangeCollection<ChartDataPoint>> _dailyTotals30d = new();

        public ObservableCollection<AverageEntry> Averages7j { get; } = new();
        public ObservableCollection<AverageEntry> Averages24h { get; } = new();
        public ObservableCollection<StatsEntry> Stats30d { get; } = new();

        private readonly UsageStatsService _statsService;

        public ChargePage()
        {
            InitializeComponent();
            BindingContext = this;

            foreach (var m in _molecules)
            {
                _data24h[m] = new ObservableRangeCollection<ChartDataPoint>();
                _data7d[m] = new ObservableRangeCollection<ChartDataPoint>();
                _dailyTotals7d[m] = new ObservableRangeCollection<ChartDataPoint>();
                _dailyTotals30d[m] = new ObservableRangeCollection<ChartDataPoint>();
            }

            _statsService = new UsageStatsService(_molecules);
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

            await LoadDailyTotalsAsync(now.AddDays(-7).Date, now.Date, _dailyTotals7d);
            await LoadDailyTotalsAsync(now.AddDays(-30).Date, now.Date, _dailyTotals30d);

            UpdateChart(Chart24h, _data24h);
            UpdateChart(Chart7d, _data7d);
            UpdateColumnChart(DailyTotalsChart7d, _dailyTotals7d);
            UpdateColumnChart(DailyTotalsChart30d, _dailyTotals30d);

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

            Stats30d.Clear();
            foreach (var m in _molecules)
            {
                var daily = await _statsService.GetDailyStatsAsync(m, 30);
                var doseStats = UsageStatsService.ComputeStats(daily.Select(d => d.TotalDose));
                var countStats = UsageStatsService.ComputeStats(daily.Select(d => (double)d.Count));
                var doses = await _statsService.GetDosesAsync(m, now.AddDays(-30), now);
                double avgInterval = UsageStatsService.ComputeAverageIntervalHours(doses);
                Stats30d.Add(new StatsEntry
                {
                    MoleculeName = ToDisplayName(m),
                    AvgDose = doseStats.mean,
                    StdDose = doseStats.stdDev,
                    MinDose = doseStats.min,
                    MaxDose = doseStats.max,
                    AvgCount = countStats.mean,
                    StdCount = countStats.stdDev,
                    AvgInterval = avgInterval
                });
            }

            AverageCollection24h.ItemsSource = Averages24h;
            AverageCollection7j.ItemsSource = Averages7j;
            StatsCollection30d.ItemsSource = Stats30d;
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

        private async Task LoadDailyTotalsAsync(DateTime from, DateTime to, Dictionary<string, ObservableRangeCollection<ChartDataPoint>> target)
        {
            int days = (int)(to - from).TotalDays + 1;
            foreach (var m in _molecules)
            {
                var stats = await _statsService.GetDailyStatsAsync(m, days);
                target[m].ReplaceRange(stats.Select(s => new ChartDataPoint(s.Date, s.TotalDose)));
            }
        }

        private void UpdateColumnChart(SfCartesianChart chart, Dictionary<string, ObservableRangeCollection<ChartDataPoint>> source)
        {
            chart.Series.Clear();
            foreach (var m in _molecules)
            {
                chart.Series.Add(new ColumnSeries
                {
                    ItemsSource = source[m],
                    XBindingPath = "Time",
                    YBindingPath = "Concentration",
                    Label = ToDisplayName(m),
                    StrokeWidth = 1
                });

                var avgPoints = CalculateMovingAverage(source[m].Select(p => p.Concentration).ToList(), 3);
                chart.Series.Add(new LineSeries
                {
                    ItemsSource = source[m].Select((p, i) => new ChartDataPoint(p.Time, avgPoints[i])),
                    XBindingPath = "Time",
                    YBindingPath = "Concentration",
                    StrokeWidth = 1,
                    Label = ToDisplayName(m) + " trend",
                    IsVisibleOnLegend = false
                });
            }
        }

        private static List<double> CalculateMovingAverage(IList<double> values, int window)
        {
            var result = new List<double>();
            for (int i = 0; i < values.Count; i++)
            {
                int start = Math.Max(0, i - window + 1);
                var slice = values.Skip(start).Take(i - start + 1);
                result.Add(slice.Average());
            }
            return result;
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

        public class StatsEntry
        {
            public string MoleculeName { get; set; } = string.Empty;
            public double AvgDose { get; set; }
            public double StdDose { get; set; }
            public double MinDose { get; set; }
            public double MaxDose { get; set; }
            public double AvgCount { get; set; }
            public double StdCount { get; set; }
            public double AvgInterval { get; set; }
        }
    }
}