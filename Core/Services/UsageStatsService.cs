using MoleculeEfficienceTracker.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class DailyStats
    {
        public DateTime Date { get; set; }
        public double TotalDose { get; set; }
        public int Count { get; set; }
    }

    public class PeakInfo
    {
        public double PeakAmount { get; set; }
        public DateTime PeakTime { get; set; }
        public double HoursAboveThreshold { get; set; }
    }

    public class UsageStatsService
    {
        private readonly Dictionary<string, DataPersistenceService> _persistence;
        private readonly IResidualLoadService _residualService = new ResidualLoadService();

        public UsageStatsService(IEnumerable<string> moleculeKeys)
        {
            _persistence = moleculeKeys.ToDictionary(k => k.ToLowerInvariant(), k => new DataPersistenceService(k));
        }

        public async Task<List<DoseEntry>> GetDosesAsync(string key, DateTime start, DateTime end)
        {
            key = key.ToLowerInvariant();
            if (!_persistence.ContainsKey(key)) return new List<DoseEntry>();
            var doses = await _persistence[key].LoadDosesAsync();
            return doses.Where(d => d.TimeTaken >= start && d.TimeTaken <= end).OrderBy(d => d.TimeTaken).ToList();
        }

        public async Task<List<DailyStats>> GetDailyStatsAsync(string key, int days)
        {
            key = key.ToLowerInvariant();
            if (!_persistence.ContainsKey(key)) return new List<DailyStats>();

            DateTime end = DateTime.Now.Date;
            DateTime start = end.AddDays(-days + 1);

            var doses = await _persistence[key].LoadDosesAsync();
            var relevant = doses.Where(d => d.TimeTaken.Date >= start && d.TimeTaken.Date <= end);

            var grouped = relevant.GroupBy(d => d.TimeTaken.Date)
                                  .ToDictionary(g => g.Key,
                                                g => new DailyStats
                                                {
                                                    Date = g.Key,
                                                    TotalDose = g.Sum(x => x.DoseMg),
                                                    Count = g.Count()
                                                });
            var list = new List<DailyStats>();
            for (DateTime d = start; d <= end; d = d.AddDays(1))
            {
                if (grouped.TryGetValue(d, out var val))
                    list.Add(val);
                else
                    list.Add(new DailyStats { Date = d, TotalDose = 0, Count = 0 });
            }
            return list;
        }

        public static (double mean, double stdDev, double min, double max) ComputeStats(IEnumerable<double> values)
        {
            var arr = values.ToList();
            if (!arr.Any()) return (0, 0, 0, 0);
            double mean = arr.Average();
            double min = arr.Min();
            double max = arr.Max();
            double variance = arr.Sum(v => Math.Pow(v - mean, 2)) / arr.Count;
            double sd = Math.Sqrt(variance);
            return (mean, sd, min, max);
        }

        public static double ComputeAverageIntervalHours(IEnumerable<DoseEntry> doses)
        {
            var ordered = doses.OrderBy(d => d.TimeTaken).ToList();
            if (ordered.Count < 2) return double.NaN;
            var intervals = ordered.Zip(ordered.Skip(1), (a, b) => (b.TimeTaken - a.TimeTaken).TotalHours);
            return intervals.Average();
        }

        public async Task<PeakInfo> GetPeakInfoAsync(
    string key, DateTime from, DateTime to, double thresholdMgPerL)
        {
            var snapshots = await _residualService.GetSnapshots(key, from, to, TimeSpan.FromHours(1));
            if (!snapshots.Any())
                return new PeakInfo { PeakAmount = 0, PeakTime = from, HoursAboveThreshold = 0 };

            // 1) Pic simple en mg
            var peak = snapshots.OrderByDescending(s => s.ResidualAmount).First();

            // 2) Calcul des heures > seuil (mg/L) **uniquement si on a un Vd et un poids**
            double hours = 0;
            // Exemple de récupération Vd et poids (à adapter) :
            double vd = GetVdForMolecule(key);        // ex. ParacetamolCalculator.VOLUME_DISTRIBUTION_L_PER_KG
            double wt = UserPreferences.GetWeightKg(); // à définir dans ta config utilisateur

            if (vd > 0 && wt > 0 && thresholdMgPerL > 0)
            {
                for (int i = 1; i < snapshots.Count; i++)
                {
                    // concentration mg/L
                    double concPrev = PharmacokineticsUtils
                                         .ResidualMgToConcentration(
                                             snapshots[i - 1].ResidualAmount,
                                             vd, wt);
                    if (concPrev >= thresholdMgPerL)
                        hours += (snapshots[i].Timestamp - snapshots[i - 1].Timestamp).TotalHours;
                }
            }
            else
            {
                hours = double.NaN; // on ne peut pas calculer
            }

            return new PeakInfo
            {
                PeakAmount = peak.ResidualAmount,
                PeakTime = peak.Timestamp,
                HoursAboveThreshold = hours
            };
        }

        public static double GetVdForMolecule(string molecule)
        {
            return molecule.ToLowerInvariant() switch
            {
                "paracetamol" => ParacetamolCalculator.VOLUME_DISTRIBUTION_L_PER_KG,
                "ibuprofen" => IbuprofeneCalculator.VOLUME_DISTRIBUTION_L_PER_KG,
                "caffeine" => CaffeineCalculator.VOLUME_DISTRIBUTION_L_PER_KG,
                "bromazepam" => BromazepamCalculator.VOLUME_DISTRIBUTION_L_PER_KG,

                _ => 1.0
            };
        }

    }
}
