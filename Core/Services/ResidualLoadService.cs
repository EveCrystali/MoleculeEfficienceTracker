using MoleculeEfficienceTracker.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class ResidualLoadService : IResidualLoadService
    {
        private readonly Dictionary<string, double> _halfLives = new()
        {
            ["caffeine"] = 5.0,
            ["bromazepam"] = 14.0,
            ["paracetamol"] = 2.5,
            ["ibuprofene"] = 2.0,
            ["ibuprofen"] = 2.0,
            ["alcool"] = 1.0
        };

        private readonly Dictionary<string, DataPersistenceService> _persistence;

        public ResidualLoadService()
        {
            _persistence = _halfLives.Keys.Distinct().ToDictionary(k => k, k => new DataPersistenceService(k));
        }

        public async Task<IReadOnlyList<ResidualLoadSnapshot>> GetSnapshots(string moleculeKey, DateTime from, DateTime to, TimeSpan interval)
        {
            if (!_halfLives.TryGetValue(moleculeKey.ToLowerInvariant(), out double halfLife))
                return Array.Empty<ResidualLoadSnapshot>();

            var doses = await _persistence[moleculeKey.ToLowerInvariant()].LoadDosesAsync();
            return CalculateSnapshots(doses, moleculeKey, halfLife, from, to, interval).ToList();
        }

        public async Task<IReadOnlyList<ResidualLoadSnapshot>> GetSnapshotsForAllMolecules(DateTime from, DateTime to, TimeSpan interval)
        {
            var list = new List<ResidualLoadSnapshot>();
            foreach (var kvp in _halfLives)
            {
                var doses = await _persistence[kvp.Key].LoadDosesAsync();
                list.AddRange(CalculateSnapshots(doses, kvp.Key, kvp.Value, from, to, interval));
            }
            return list.OrderBy(s => s.Timestamp).ToList();
        }

        public async Task<double> GetAverageLoadPerDay(string moleculeKey, int lastNDays)
        {
            var end = DateTime.Now;
            var start = end.AddDays(-lastNDays);
            var snapshots = await GetSnapshots(moleculeKey, start, end, TimeSpan.FromHours(1));
            var groups = snapshots.GroupBy(s => s.Timestamp.Date).Select(g => g.Average(x => x.ResidualAmount));
            return groups.Any() ? groups.Average() : 0.0;
        }

        private static IEnumerable<ResidualLoadSnapshot> CalculateSnapshots(IEnumerable<DoseEntry> doses, string molecule, double halfLife, DateTime from, DateTime to, TimeSpan interval)
        {
            var snapshots = new List<ResidualLoadSnapshot>();
            for (var t = from; t <= to; t = t.Add(interval))
            {
                double amount = 0;
                foreach (var d in doses)
                {
                    double h = (t - d.TimeTaken).TotalHours;
                    if (h >= 0)
                        amount += ResidualLoadCalculator.GetResidualAmount(d.DoseMg, halfLife, h);
                }
                snapshots.Add(new ResidualLoadSnapshot(t, molecule, amount));
            }
            return snapshots;
        }
    }
}