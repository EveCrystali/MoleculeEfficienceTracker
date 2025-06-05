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
    double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose); // Nouvelle méthode
    /// <summary>
    /// Returns the remaining amount of molecule in the body expressed in the same unit as the dose.
    /// For molecules whose concentration unit is mg/L, this converts using the volume of distribution and weight.
    /// For molecules already expressed in mg or units, this simply returns the concentration value.
    /// </summary>
    double CalculateTotalAmount(List<DoseEntry> doses, DateTime time);
}
