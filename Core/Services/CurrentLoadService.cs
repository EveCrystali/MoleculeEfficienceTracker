using System.Diagnostics;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>Ce qu'il reste d'une molécule, à l'instant où l'on regarde.</summary>
    public sealed class MoleculeLoad
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public double Amount { get; init; }
        public string AmountUnit { get; init; } = DoseUnits.Milligram;
        public double Concentration { get; init; }
        public string ConcentrationUnit { get; init; } = "mg/L";
        public EffectLevel Level { get; init; }
        public DateTime? LastIntake { get; init; }
        public int TakesToday { get; init; }
    }

    /// <summary>
    /// L'état du moment, toutes molécules.
    ///
    /// Les seuils de chaque molécule vivaient jusqu'ici dans sa page, hors de
    /// portée de tout autre écran. Les voici rassemblés : l'écran Aujourd'hui peut
    /// nommer un niveau sans redéclarer quatre constantes, et il n'y a qu'un
    /// endroit à corriger le jour où l'un d'eux bouge.
    /// </summary>
    public class CurrentLoadService
    {
        private readonly Dictionary<string, IMoleculeCalculator> _calculators = new()
        {
            [MoleculeKeys.Caffeine] = new CaffeineCalculator(),
            [MoleculeKeys.Bromazepam] = new BromazepamCalculator(),
            [MoleculeKeys.Paracetamol] = new ParacetamolCalculator(),
            [MoleculeKeys.Ibuprofen] = new IbuprofeneCalculator(),
            [MoleculeKeys.Alcohol] = new AlcoholCalculator()
        };

        /// <summary>Seuils du plus fort au plus faible, dans l'unité du calculateur.</summary>
        public static (double Strong, double Moderate, double Light, double Negligible) Thresholds(string key)
            => MoleculeKeys.Normalize(key) switch
            {
                MoleculeKeys.Caffeine => (CaffeineCalculator.STRONG_THRESHOLD,
                                          CaffeineCalculator.MODERATE_THRESHOLD,
                                          CaffeineCalculator.LIGHT_THRESHOLD,
                                          CaffeineCalculator.NEGLIGIBLE_THRESHOLD),

                MoleculeKeys.Bromazepam => (BromazepamCalculator.STRONG_THRESHOLD,
                                            BromazepamCalculator.MODERATE_THRESHOLD,
                                            BromazepamCalculator.LIGHT_THRESHOLD,
                                            BromazepamCalculator.NEGLIGIBLE_THRESHOLD),

                MoleculeKeys.Paracetamol => (ParacetamolCalculator.STRONG_THRESHOLD,
                                             ParacetamolCalculator.MODERATE_THRESHOLD,
                                             ParacetamolCalculator.LIGHT_THRESHOLD,
                                             ParacetamolCalculator.NEGLIGIBLE_THRESHOLD),

                MoleculeKeys.Ibuprofen => (IbuprofeneCalculator.STRONG_THRESHOLD,
                                           IbuprofeneCalculator.MODERATE_THRESHOLD,
                                           IbuprofeneCalculator.LIGHT_THRESHOLD,
                                           IbuprofeneCalculator.NEGLIGIBLE_THRESHOLD),

                MoleculeKeys.Alcohol => (AlcoholCalculator.BAC_STRONG_THRESHOLD,
                                         AlcoholCalculator.BAC_MODERATE_THRESHOLD,
                                         AlcoholCalculator.BAC_LIGHT_THRESHOLD,
                                         AlcoholCalculator.BAC_NEGLIGIBLE_THRESHOLD),

                _ => (double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue)
            };

        public static EffectLevel LevelFor(string key, double concentration)
        {
            (double strong, double moderate, double light, _) = Thresholds(key);

            if (concentration >= strong) return EffectLevel.Strong;
            if (concentration >= moderate) return EffectLevel.Moderate;
            if (concentration >= light) return EffectLevel.Light;
            return EffectLevel.None;
        }

        /// <summary>Une molécule pèse encore si elle dépasse son seuil de perception.</summary>
        public static bool IsPresent(string key, double concentration)
            => concentration >= Thresholds(key).Negligible;

        /// <summary>
        /// Lit les cinq fichiers une fois chacun et rend l'état courant. Les
        /// molécules sans rien en circulation sont écartées : l'écran ne liste que
        /// ce qui est là.
        /// </summary>
        public async Task<List<MoleculeLoad>> GetPresentAsync(DateTime now)
        {
            var loads = new List<MoleculeLoad>();

            foreach ((string key, IMoleculeCalculator calculator) in _calculators)
            {
                List<DoseEntry> doses;
                try
                {
                    doses = await new DataPersistenceService(key).LoadDosesAsync();
                }
                catch (DoseDataCorruptedException ex)
                {
                    // Un fichier illisible ne doit pas faire tomber un écran qui en
                    // agrège cinq.
                    Debug.WriteLine($"[Aujourd'hui] {key} illisible : {ex.Message}");
                    continue;
                }

                if (doses.Count == 0) continue;

                double concentration = calculator.CalculateTotalConcentration(doses, now);
                if (!IsPresent(key, concentration)) continue;

                loads.Add(new MoleculeLoad
                {
                    Key = key,
                    Name = MoleculeKeys.DisplayName(key),
                    Amount = calculator.CalculateTotalAmount(doses, now),
                    AmountUnit = calculator.DoseUnit,
                    Concentration = concentration,
                    ConcentrationUnit = calculator.ConcentrationUnit,
                    Level = LevelFor(key, concentration),
                    // Max() sur une séquence de DateTime? vide rend null : pas
                    // besoin de valeur par défaut.
                    LastIntake = doses.Where(d => d.TimeTaken <= now)
                                      .Select(d => (DateTime?)d.TimeTaken)
                                      .Max(),
                    TakesToday = doses.Count(d => d.TimeTaken.Date == now.Date && d.TimeTaken <= now)
                });
            }

            return loads.OrderByDescending(l => l.Level).ThenBy(l => l.Name).ToList();
        }
    }
}
