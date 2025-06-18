using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using MoleculeEfficienceTracker.Core.Extensions;
using Syncfusion.Maui.Charts;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
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

        private readonly Dictionary<string, Color> _colors = new()
        {
            ["caffeine"] = Color.FromArgb("#ff7f0e"),
            ["bromazepam"] = Color.FromArgb("#1f77b4"),
            ["paracetamol"] = Color.FromArgb("#2ca02c"),
            ["ibuprofene"] = Color.FromArgb("#9467bd"),
            ["alcool"] = Color.FromArgb("#d62728")
        };

        private readonly Dictionary<string, string> _icons = new()
        {
            ["caffeine"] = "🍵",
            ["bromazepam"] = "💊",
            ["paracetamol"] = "💊",
            ["ibuprofene"] = "💊",
            ["alcool"] = "🍾"
        };

        private readonly Dictionary<string, double> _lightThresholds = new()
        {
            ["caffeine"] = CaffeineCalculator.LIGHT_THRESHOLD,
            ["bromazepam"] = BromazepamCalculator.LIGHT_THRESHOLD,
            ["paracetamol"] = ParacetamolCalculator.LIGHT_THRESHOLD,
            ["ibuprofene"] = IbuprofeneCalculator.LIGHT_THRESHOLD,
            ["alcool"] = AlcoholCalculator.BAC_LIGHT_THRESHOLD
        };

        private readonly Dictionary<string, ObservableRangeCollection<ChartDataPoint>> _data24h = new();
        private readonly Dictionary<string, ObservableRangeCollection<ChartDataPoint>> _data7d = new();
        private readonly Dictionary<string, ObservableRangeCollection<ChartDataPoint>> _dailyTotals7d = new();
        private readonly Dictionary<string, ObservableRangeCollection<ChartDataPoint>> _dailyTotals30d = new();

        // public ObservableCollection<AverageEntry> Averages7j { get; } = new();
        // public ObservableCollection<AverageEntry> Averages24h { get; } = new();

        public ObservableCollection<StatsEntry> Stats1d { get; } = new();
        public ObservableCollection<StatsEntry> Stats7d { get; } = new();
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

            // UpdateChart(Chart24h, _data24h);
            // UpdateChart(Chart7d, _data7d);
            // UpdateColumnChart(DailyTotalsChart7d, _dailyTotals7d);
            // UpdateColumnChart(DailyTotalsChart30d, _dailyTotals30d);

            // Averages24h.Clear();
            // Averages7j.Clear();

            // foreach (var m in _molecules)
            // {
            //     double avg = await _service.GetAverageLoadPerDay(m, 7);
            //     Averages7j.Add(new AverageEntry { MoleculeName = ToDisplayName(m), Average = avg });
            // }
            // foreach (var m in _molecules)
            // {
            //     double avg = await _service.GetAverageLoadPerDay(m, 1);
            //     Averages24h.Add(new AverageEntry { MoleculeName = ToDisplayName(m), Average = avg });
            // }
            // foreach (var m in _molecules)
            // {
            //     double avg = await _service.GetAverageLoadPerDay(m, 7);
            //     Averages7j.Add(new AverageEntry { MoleculeName = ToDisplayName(m), Average = avg });
            // }

            Stats1d.Clear();
            Stats7d.Clear();
            Stats30d.Clear();
            foreach (var m in _molecules)
            {
                Stats1d.Add(await ComputeStatsForPeriod(m, 1));
                Stats7d.Add(await ComputeStatsForPeriod(m, 7));
                Stats30d.Add(await ComputeStatsForPeriod(m, 30));
            }

            StatsCollection1d.ItemsSource = Stats1d;
            // AverageCollection7j.ItemsSource = Averages7j;
            StatsCollection7d.ItemsSource = Stats7d;
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
                    Label = ToDisplayName(m),
                    Fill = _colors[m]
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
                    StrokeWidth = 1,
                    Fill = _colors[m]
                });

                var avgPoints = CalculateMovingAverage(source[m].Select(p => p.Concentration).ToList(), 3);
                chart.Series.Add(new LineSeries
                {
                    ItemsSource = source[m].Select((p, i) => new ChartDataPoint(p.Time, avgPoints[i])),
                    XBindingPath = "Time",
                    YBindingPath = "Concentration",
                    StrokeWidth = 1,
                    Fill = _colors[m],
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

        private async Task<StatsEntry> ComputeStatsForPeriod(string molecule, int days)
        {
            var now = DateTime.Now;

            // 1) récupère les totaux journaliers
            var dailyStats = await _statsService.GetDailyStatsAsync(molecule, days);

            // 2) moyenne / écart-type / min / max des totaux journaliers
            // Récupération des snapshots horaires
            var snapshots = await _service.GetSnapshots(molecule, now.AddDays(-days), now, TimeSpan.FromHours(1));

            // Moyenne journalière de residualAmount
            var groups = snapshots
                .GroupBy(s => s.Timestamp.Date)
                .Select(g => g.Average(s => s.ResidualAmount))
                .ToList();

            var doseStats = UsageStatsService.ComputeStats(groups);

            // 3) même logique pour le nombre de prises par jour
            var countStats = UsageStatsService
                .ComputeStats(dailyStats.Select(d => (double)d.Count));

            // 4) intervalle moyen entre prises peut rester ou être retiré
            var doses = await _statsService.GetDosesAsync(molecule, now.AddDays(-days), now);
            double avgInterval = UsageStatsService.ComputeAverageIntervalHours(doses);

            // 5) calcul de la variation de semaine en semaine
            var history = await _statsService.GetDailyStatsAsync(molecule, days * 2);
            double prevAvg = history.Take(days).Average(d => d.TotalDose);
            double currAvg = history.Skip(days).Average(d => d.TotalDose);
            double variation = prevAvg > 0
                ? 100.0 * (currAvg - prevAvg) / prevAvg
                : double.NaN;

            // 6) pic et heures > seuil — inchangé si tu l’as déjà corrigé
            double threshold = _lightThresholds[molecule];
            var peak = await _statsService.GetPeakInfoAsync(molecule, now.AddDays(-days), now, threshold);

            return new StatsEntry
            {
                MoleculeName = ToDisplayName(molecule),
                Icon = _icons[molecule],
                PeriodDays = days,
                AvgDose = doseStats.mean,      // ← devient moyenne journalière
                StdDose = doseStats.stdDev,
                MinDose = doseStats.min,
                MaxDose = doseStats.max,
                AvgCount = countStats.mean,
                StdCount = countStats.stdDev,
                MinCount = countStats.min,
                MaxCount = countStats.max,
                AvgInterval = avgInterval,
                VariationPercent = variation,
                Peak = peak.PeakAmount,
                PeakTime = peak.PeakTime,
                HoursAboveThreshold = peak.HoursAboveThreshold
            };
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
            public string Icon { get; set; } = string.Empty;
            public string MoleculeName { get; set; } = string.Empty;
            public int PeriodDays { get; set; }
            public double AvgDose { get; set; }
            public double StdDose { get; set; }
            public double MinDose { get; set; }
            public double MaxDose { get; set; }
            public double AvgCount { get; set; }
            public double StdCount { get; set; }
            public double MinCount { get; set; }
            public double MaxCount { get; set; }
            public double AvgInterval { get; set; }
            public double VariationPercent { get; set; }
            public double Peak { get; set; }
            public DateTime PeakTime { get; set; }
            public double HoursAboveThreshold { get; set; }
            public string VariationText => double.IsNaN(VariationPercent)
                ? "N/A"
                : VariationPercent > 0 ? $"▲ {VariationPercent:F1}%"
                : VariationPercent < 0 ? $"▼ {Math.Abs(VariationPercent):F1}%"
                : "➖ 0%";
            public Color VariationColor => double.IsNaN(VariationPercent)
                ? Colors.Gray
                : VariationPercent > 0 ? Colors.Red
                : VariationPercent < 0 ? Colors.Green
                : Colors.Gray;
            public string PeakInfoText =>
                $"Pic {Peak:F1} le {PeakTime:dd/MM HH:mm}, {HoursAboveThreshold:F1} h > seuil";
        }
    }
}