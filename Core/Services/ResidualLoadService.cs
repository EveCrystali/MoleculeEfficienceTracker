using MoleculeEfficienceTracker.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Charge résiduelle, molécule par molécule.
    ///
    /// L'ancienne version tenait sa propre table de demi-vies et appliquait une
    /// décroissance mono-exponentielle nue — sans absorption, sans
    /// biodisponibilité, sans volume de distribution. Elle divergeait donc des
    /// courbes affichées sur les pages, et elle modélisait l'alcool par une
    /// demi-vie de 1 h quand le calculateur l'élimine à l'ordre zéro. Elle
    /// s'appuie désormais sur les mêmes calculateurs que le reste de
    /// l'application : une seule vérité pharmacocinétique.
    /// </summary>
    public class ResidualLoadService : IResidualLoadService
    {
        private readonly Dictionary<string, IMoleculeCalculator> _calculators;
        private readonly Dictionary<string, DataPersistenceService> _persistence;

        public ResidualLoadService()
        {
            _calculators = new Dictionary<string, IMoleculeCalculator>
            {
                [MoleculeKeys.Caffeine] = new CaffeineCalculator(),
                [MoleculeKeys.Bromazepam] = new BromazepamCalculator(),
                [MoleculeKeys.Paracetamol] = new ParacetamolCalculator(),
                [MoleculeKeys.Ibuprofen] = new IbuprofeneCalculator(),
                [MoleculeKeys.Alcohol] = new AlcoholCalculator()
            };

            _persistence = _calculators.Keys.ToDictionary(k => k, k => new DataPersistenceService(k));
        }

        public IReadOnlyCollection<string> KnownMolecules => _calculators.Keys;

        /// <summary>
        /// Lecture tolérante : un fichier illisible ne doit pas faire tomber la page
        /// de synthèse, qui agrège cinq molécules.
        /// </summary>
        private async Task<List<DoseEntry>> LoadSafelyAsync(string key)
        {
            try
            {
                return await _persistence[key].LoadDosesAsync();
            }
            catch (DoseDataCorruptedException ex)
            {
                Debug.WriteLine($"[Charge] {key} illisible : {ex.Message}");
                return new List<DoseEntry>();
            }
        }

        public async Task<IReadOnlyList<ResidualLoadSnapshot>> GetSnapshots(
            string moleculeKey, DateTime from, DateTime to, TimeSpan interval)
        {
            string key = MoleculeKeys.Normalize(moleculeKey);
            if (!_calculators.TryGetValue(key, out IMoleculeCalculator? calculator))
                return Array.Empty<ResidualLoadSnapshot>();

            List<DoseEntry> doses = await LoadSafelyAsync(key);
            return CalculateSnapshots(doses, key, calculator, from, to, interval);
        }

        public async Task<IReadOnlyList<ResidualLoadSnapshot>> GetSnapshotsForAllMolecules(
            DateTime from, DateTime to, TimeSpan interval)
        {
            var list = new List<ResidualLoadSnapshot>();

            foreach (KeyValuePair<string, IMoleculeCalculator> entry in _calculators)
            {
                List<DoseEntry> doses = await LoadSafelyAsync(entry.Key);
                list.AddRange(CalculateSnapshots(doses, entry.Key, entry.Value, from, to, interval));
            }

            return list.OrderBy(s => s.Timestamp).ToList();
        }

        public async Task<double> GetAverageLoadPerDay(string moleculeKey, int lastNDays)
        {
            DateTime end = DateTime.Now;
            DateTime start = end.Date.AddDays(-lastNDays + 1);

            IReadOnlyList<ResidualLoadSnapshot> snapshots =
                await GetSnapshots(moleculeKey, start, end, TimeSpan.FromHours(1));

            // Moyenne pondérée par le nombre de relevés, et non moyenne des
            // moyennes journalières : le premier et le dernier jour de la fenêtre
            // sont toujours partiels et pesaient autrement autant qu'un jour plein.
            return snapshots.Count > 0 ? snapshots.Average(s => s.ResidualAmount) : 0.0;
        }

        private static List<ResidualLoadSnapshot> CalculateSnapshots(
            List<DoseEntry> doses, string molecule, IMoleculeCalculator calculator,
            DateTime from, DateTime to, TimeSpan interval)
        {
            var snapshots = new List<ResidualLoadSnapshot>();
            if (interval <= TimeSpan.Zero) return snapshots;

            string amountUnit = MoleculeKeys.DoseUnit(molecule);

            for (DateTime t = from; t <= to; t = t.Add(interval))
            {
                snapshots.Add(new ResidualLoadSnapshot(
                    t, molecule,
                    calculator.CalculateTotalAmount(doses, t),
                    calculator.CalculateTotalConcentration(doses, t),
                    amountUnit,
                    calculator.ConcentrationUnit));
            }

            return snapshots;
        }
    }
}
