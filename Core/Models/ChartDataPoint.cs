namespace MoleculeEfficienceTracker.Core.Models;

public class ChartDataPoint
{
    public DateTime Time { get; set; }
    public double Concentration { get; set; }

    public ChartDataPoint(DateTime time, double concentration)
    {
        Time = time;
        Concentration = concentration;
    }
}

