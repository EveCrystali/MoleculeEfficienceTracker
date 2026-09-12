using MoleculeEfficienceTracker.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        /// <summary>
        /// Cache de lecture à durée de vie courte.
        ///
        /// Un affichage de la page de synthèse déclenchait près de cent lectures
        /// et désérialisations complètes des mêmes fichiers, toutes séquentielles.
        /// Le contenu ne change pas pendant un rafraîchissement : il est lu une
        /// fois par molécule.
        /// </summary>
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(20);
        private readonly Dictionary<string, (DateTime LoadedAt, List<DoseEntry> Doses)> _cache = new();

        public UsageStatsService(IEnumerable<string> moleculeKeys)
        {
            _persistence = moleculeKeys
                .Select(k => MoleculeKeys.Normalize(k))
                .Distinct()
                .ToDictionary(k => k, k => new DataPersistenceService(k));
        }

        /// <summary>Vide le cache : à appeler quand une dose vient d'être saisie.</summary>
        public void Invalidate() => _cache.Clear();

        private async Task<List<DoseEntry>> LoadAsync(string key)
        {
            if (!_persistence.ContainsKey(key))
                return new List<DoseEntry>();

            if (_cache.TryGetValue(key, out var cached) && DateTime.Now - cached.LoadedAt < CacheLifetime)
                return cached.Doses;

            List<DoseEntry> doses;
            try
            {
                doses = await _persistence[key].LoadDosesAsync();
            }
            catch (DoseDataCorruptedException ex)
            {
                Debug.WriteLine($"[Stats] {key} illisible : {ex.Message}");
                doses = new List<DoseEntry>();
            }

            _cache[key] = (DateTime.Now, doses);
            return doses;
        }

        public async Task<List<DoseEntry>> GetDosesAsync(string key, DateTime start, DateTime end)
        {
            List<DoseEntry> doses = await LoadAsync(MoleculeKeys.Normalize(key));
            return doses.Where(d => d.TimeTaken >= start && d.TimeTaken <= end)
                        .OrderBy(d => d.TimeTaken)
                        .ToList();
        }

        public async Task<List<DailyStats>> GetDailyStatsAsync(string key, int days)
        {
            List<DoseEntry> doses = await LoadAsync(MoleculeKeys.Normalize(key));

            DateTime end = DateTime.Now.Date;
            DateTime start = end.AddDays(-days + 1);

            Dictionary<DateTime, DailyStats> grouped = doses
                .Where(d => d.TimeTaken.Date >= start && d.TimeTaken.Date <= end)
                .GroupBy(d => d.TimeTaken.Date)
                .ToDictionary(g => g.Key, g => new DailyStats
                {
                    Date = g.Key,
                    TotalDose = g.Sum(x => x.DoseMg),
                    Count = g.Count()
                });

            var list = new List<DailyStats>();
            for (DateTime d = start; d <= end; d = d.AddDays(1))
                list.Add(grouped.TryGetValue(d, out DailyStats? val)
                         ? val
                         : new DailyStats { Date = d, TotalDose = 0, Count = 0 });

            return list;
        }

        /// <summary>Écart-type d'échantillon (n−1) : les jours observés estiment une habitude.</summary>
        public static (double mean, double stdDev, double min, double max) ComputeStats(IEnumerable<double> values)
        {
            List<double> arr = values.ToList();
            if (arr.Count == 0) return (0, 0, 0, 0);

            double mean = arr.Average();
            if (arr.Count == 1) return (mean, 0, mean, mean);

            double variance = arr.Sum(v => (v - mean) * (v - mean)) / (arr.Count - 1);
            return (mean, Math.Sqrt(variance), arr.Min(), arr.Max());
        }

        public static double ComputeAverageIntervalHours(IEnumerable<DoseEntry> doses)
        {
            List<DoseEntry> ordered = doses.OrderBy(d => d.TimeTaken).ToList();
            if (ordered.Count < 2) return double.NaN;

            return ordered.Zip(ordered.Skip(1),
                               (a, b) => PkTime.ElapsedHours(a.TimeTaken, b.TimeTaken))
                          .Average();
        }

        /// <summary>
        /// Pic de charge et temps passé au-dessus d'un seuil de concentration.
        ///
        /// La concentration vient désormais du calculateur de la molécule. Elle
        /// était auparavant reconstituée en divisant une charge résiduelle par un
        /// volume de distribution deviné d'après la clé — avec 1 L/kg par défaut
        /// dès que la clé n'était pas reconnue, ce qui était le cas de
        /// « ibuprofene » et de « alcool ».
        /// </summary>
        public async Task<PeakInfo> GetPeakInfoAsync(string key, DateTime from, DateTime to, double thresholdConcentration)
        {
            IReadOnlyList<ResidualLoadSnapshot> snapshots =
                await _residualService.GetSnapshots(key, from, to, TimeSpan.FromHours(1));

            if (snapshots.Count == 0)
                return new PeakInfo { PeakAmount = 0, PeakTime = from, HoursAboveThreshold = 0 };

            ResidualLoadSnapshot peak = snapshots.MaxBy(s => s.ResidualAmount)!;

            double hours = 0;
            if (thresholdConcentration > 0)
            {
                for (int i = 1; i < snapshots.Count; i++)
                {
                    if (snapshots[i - 1].Concentration >= thresholdConcentration)
                        hours += (snapshots[i].Timestamp - snapshots[i - 1].Timestamp).TotalHours;
                }
            }

            return new PeakInfo
            {
                PeakAmount = peak.ResidualAmount,
                PeakTime = peak.Timestamp,
                HoursAboveThreshold = hours
            };
        }
    }
}
