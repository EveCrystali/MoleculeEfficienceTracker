using MoleculeEfficienceTracker.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace MoleculeEfficienceTracker.Core.Services;

public class ResidualLoadService : IResidualLoadService
{
    private readonly Dictionary<string, (IMoleculeCalculator Calc, DataPersistenceService Persist)> _registry;

    public ResidualLoadService()
    {
        _registry = new Dictionary<string, (IMoleculeCalculator, DataPersistenceService)>(StringComparer.OrdinalIgnoreCase)
        {
            ["caffeine"] = (new CaffeineCalculator(), new DataPersistenceService("caffeine")),
            ["bromazepam"] = (new BromazepamCalculator(), new DataPersistenceService("bromazepam")),
            ["alcohol"] = (new AlcoholCalculator(), new DataPersistenceService("alcohol")),
            ["paracetamol"] = (new ParacetamolCalculator(), new DataPersistenceService("paracetamol")),
            ["ibuprofen"] = (new IbuprofeneCalculator(), new DataPersistenceService("ibuprofen"))
        };
    }

    public IEnumerable<ResidualLoadSnapshot> GetSnapshots(string moleculeKey, DateTime from, DateTime to, TimeSpan interval)
    {
        if (!_registry.TryGetValue(moleculeKey, out var entry))
            yield break;

        List<DoseEntry> doses = entry.Persist.LoadDosesAsync().Result;
        for (DateTime t = from; t <= to; t = t.Add(interval))
        {
            double amount = entry.Calc.CalculateTotalAmount(doses, t);
            yield return new ResidualLoadSnapshot(t, moleculeKey, amount);
        }
    }

    public IEnumerable<ResidualLoadSnapshot> GetSnapshotsForAllMolecules(DateTime from, DateTime to, TimeSpan interval)
    {
        foreach (var key in _registry.Keys)
        {
            foreach (var snap in GetSnapshots(key, from, to, interval))
            {
                yield return snap;
            }
        }
    }

    public double GetAverageLoadPerDay(string moleculeKey, int lastNDays)
    {
        DateTime end = DateTime.Now;
        DateTime start = end.AddDays(-lastNDays);
        TimeSpan step = TimeSpan.FromHours(1);
        var snaps = GetSnapshots(moleculeKey, start, end, step).ToList();
        if (snaps.Count == 0) return 0;
        double avg = snaps.Average(s => s.ResidualAmount);
        return avg;
    }
}
