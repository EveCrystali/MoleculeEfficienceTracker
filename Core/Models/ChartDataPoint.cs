namespace MoleculeEfficienceTracker.Core.Models;

public class ChartDataPoint
{
    public DateTime Time { get; set; }
    public double Concentration { get; set; }
    public double EffectPercent { get; set; }

    public ChartDataPoint(DateTime time, double concentration, double effectPercent = 0)
    {
        Time = time;
        Concentration = concentration;
        EffectPercent = effectPercent;
    }
}

