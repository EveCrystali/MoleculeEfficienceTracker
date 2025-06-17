using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public interface IResidualLoadService
    {
        Task<IReadOnlyList<ResidualLoadSnapshot>> GetSnapshots(string moleculeKey, DateTime from, DateTime to, TimeSpan interval);
        Task<IReadOnlyList<ResidualLoadSnapshot>> GetSnapshotsForAllMolecules(DateTime from, DateTime to, TimeSpan interval);
        Task<double> GetAverageLoadPerDay(string moleculeKey, int lastNDays);
    }
}