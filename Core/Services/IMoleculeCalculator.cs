using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services;

public interface IMoleculeCalculator
{
    double CalculateTotalConcentration(List<DoseEntry> doses, DateTime time);
    double CalculateSingleDoseConcentration(DoseEntry dose, DateTime time);
    List<(DateTime Time, double Concentration)> GenerateGraph(List<DoseEntry> doses, DateTime startTime, DateTime endTime, int points = 200);
    string DisplayName { get; }
    string DoseUnit { get; }
    string ConcentrationUnit { get; }
}
