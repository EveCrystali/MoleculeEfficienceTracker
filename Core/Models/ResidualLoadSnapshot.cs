namespace MoleculeEfficienceTracker.Core.Models;

public class ResidualLoadSnapshot
{
    public DateTime Timestamp { get; set; }
    public string MoleculeName { get; set; } = string.Empty;
    public double ResidualAmount { get; set; }

    public ResidualLoadSnapshot() {}

    public ResidualLoadSnapshot(DateTime timestamp, string moleculeName, double residualAmount)
    {
        Timestamp = timestamp;
        MoleculeName = moleculeName;
        ResidualAmount = residualAmount;
    }
}
