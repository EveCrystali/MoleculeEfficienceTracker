using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using System.Collections.ObjectModel;
using System.Linq;
using Syncfusion.Maui.Charts;

namespace MoleculeEfficienceTracker;

public partial class ChargePage : ContentPage
{
    private readonly IResidualLoadService _service = new ResidualLoadService();
    private readonly Dictionary<string, ObservableCollection<ChartDataPoint>> _seriesData = new();

    public ChargePage()
    {
        InitializeComponent();
        BindingContext = this;
        SetupChart();
        LoadData();
    }

    private void SetupChart()
    {
        LoadChart.XAxes.Add(new DateTimeAxis());
        LoadChart.YAxes.Add(new NumericalAxis());
    }

    private void LoadData()
    {
        DateTime end = DateTime.Now;
        DateTime start = end.AddDays(-7);
        TimeSpan step = TimeSpan.FromHours(3);
        var snapshots = _service.GetSnapshotsForAllMolecules(start, end, step);
        foreach (var group in snapshots.GroupBy(s => s.MoleculeName))
        {
            ObservableCollection<ChartDataPoint> list = new();
            foreach (var snap in group)
            {
                list.Add(new ChartDataPoint(snap.Timestamp, snap.ResidualAmount));
            }
            _seriesData[group.Key] = list;
        }
        foreach (var kv in _seriesData)
        {
            LineSeries series = new()
            {
                ItemsSource = kv.Value,
                XBindingPath = nameof(ChartDataPoint.Time),
                YBindingPath = nameof(ChartDataPoint.Concentration),
                Label = kv.Key
            };
            LoadChart.Series.Add(series);
        }

        var averages = _seriesData.Select(kv => new
        {
            Molecule = kv.Key,
            Average = kv.Value.Any() ? kv.Value.Average(p => p.Concentration) : 0
        }).ToList();
        AverageCollection.ItemsSource = averages;
    }
}
