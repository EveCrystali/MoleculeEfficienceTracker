using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services;

public interface IResidualLoadService
{
    IEnumerable<ResidualLoadSnapshot> GetSnapshots(string moleculeKey, DateTime from, DateTime to, TimeSpan interval);
    IEnumerable<ResidualLoadSnapshot> GetSnapshotsForAllMolecules(DateTime from, DateTime to, TimeSpan interval);
    double GetAverageLoadPerDay(string moleculeKey, int lastNDays);
}
