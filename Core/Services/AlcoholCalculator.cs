using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Modèle de Widmark : absorption linéaire jusqu'au pic, puis élimination
    /// d'ordre zéro.
    ///
    /// La version précédente appliquait l'élimination <em>dose par dose</em> puis
    /// sommait le tout. Avec quatre verres actifs, le foie éliminait quatre fois
    /// plus vite — et la courbe redescendait d'autant plus brutalement qu'on avait
    /// bu. L'organisme n'a qu'un compartiment : l'élimination s'applique désormais
    /// une seule fois, sur le total.
    ///
    /// Les doses sont comptées en unités standard (1 u = 10 g d'éthanol pur).
    /// </summary>
    public class AlcoholCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Alcool";
        public string DoseUnit => DoseUnits.StandardUnit;
        public string ConcentrationUnit => "g/L";

        // Coefficients de diffusion de Widmark
        private const double DIFFUSION_HOMME = 0.7;
        private const double DIFFUSION_FEMME = 0.6;

        /// <summary>Élimination d'ordre zéro, appliquée au total et non par dose.</summary>
        private const double ELIMINATION_RATE = 0.15; // g/L·h

        public const double BAC_STRONG_THRESHOLD = 1.2;
        public const double BAC_MODERATE_THRESHOLD = 0.5;   // limite légale de conduite
        public const double BAC_LIGHT_THRESHOLD = 0.2;
        public const double BAC_NEGLIGIBLE_THRESHOLD = 0.1;

        private const double GRAMS_PER_UNIT = 10.0;
        private const double ALCOHOL_DENSITY = 0.8; // g/mL

        /// <summary>Durée d'absorption par type de boisson, en heures.</summary>
        private static readonly Dictionary<string, double> AbsTime = new(StringComparer.OrdinalIgnoreCase)
        {
            ["biere"] = 0.33,
            ["cidre"] = 0.4,
            ["vin"] = 0.5,
            ["spiritueux"] = 0.25,
            ["cocktail"] = 0.5,
            ["champagne"] = 0.45,
            ["liqueur"] = 0.25
        };

        private const double DEFAULT_ABSORPTION_HOURS = 0.5;

        /// <summary>Au-delà, la contribution d'une prise est nécessairement éliminée.</summary>
        private const double RELEVANCE_WINDOW_HOURS = 48.0;

        public static IEnumerable<string> KnownBeverageTypes => AbsTime.Keys;

        /// <summary>
        /// Type proposé par défaut à la saisie. Il n'intervient plus dans le calcul :
        /// chaque prise porte le sien.
        /// </summary>
        public string BeverageType { get; set; } = KnownBeverageTypes.First();

        /// <summary>Convertit un volume et un degré en unités standard.</summary>
        public static double VolumePercentToUnits(double volumeMl, double percent)
        {
            if (volumeMl <= 0 || percent <= 0) return 0;
            return volumeMl * (percent / 100.0) * ALCOHOL_DENSITY / GRAMS_PER_UNIT;
        }

        private static double GetAbsorptionTime(string? beverage)
            => !string.IsNullOrWhiteSpace(beverage) && AbsTime.TryGetValue(beverage, out double t)
                ? t
                : DEFAULT_ABSORPTION_HOURS;

        private static double GetDiffCoeff()
            => UserProfile.Sex == Sex.Female ? DIFFUSION_FEMME : DIFFUSION_HOMME;

        private sealed class AbsorptionWindow
        {
            public DateTimeOffset Start;
            public DateTimeOffset End;
            public double RatePerHour;
        }

        /// <summary>
        /// Évalue l'alcoolémie aux instants demandés, en une seule passe.
        ///
        /// L'apport est linéaire par morceaux et l'élimination constante : la courbe
        /// est donc affine entre deux ruptures. En prenant pour ruptures les débuts
        /// et fins d'absorption ainsi que les instants interrogés, le résultat est
        /// exact à chacun d'eux, sans pas d'intégration arbitraire.
        /// </summary>
        private double[] Evaluate(List<DoseEntry> doses, IReadOnlyList<DateTimeOffset> queries)
        {
            var result = new double[queries.Count];
            if (doses == null || doses.Count == 0 || queries.Count == 0)
                return result;

            DateTimeOffset firstQuery = queries.Min();
            double diffusion = GetDiffCoeff();

            var windows = new List<AbsorptionWindow>();
            foreach (DoseEntry d in doses)
            {
                if (d.DoseMg <= 0 || d.WeightKg <= 0) continue;

                DateTimeOffset start = PkTime.ToOffset(d.TimeTaken);
                double hours = GetAbsorptionTime(d.BeverageType);
                DateTimeOffset end = start.AddHours(hours);

                if (end < firstQuery.AddHours(-RELEVANCE_WINDOW_HOURS)) continue;

                double peak = d.DoseMg * GRAMS_PER_UNIT / (d.WeightKg * diffusion);
                windows.Add(new AbsorptionWindow { Start = start, End = end, RatePerHour = peak / hours });
            }

            if (windows.Count == 0) return result;

            var breakpoints = new SortedSet<DateTimeOffset>();
            foreach (AbsorptionWindow w in windows) { breakpoints.Add(w.Start); breakpoints.Add(w.End); }
            foreach (DateTimeOffset q in queries) breakpoints.Add(q);

            List<DateTimeOffset> ordered = breakpoints.ToList();
            var levels = new double[ordered.Count];

            double bac = 0;
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                double segmentHours = (ordered[i + 1] - ordered[i]).TotalHours;

                double rateIn = 0;
                foreach (AbsorptionWindow w in windows)
                {
                    if (w.Start <= ordered[i] && w.End > ordered[i])
                        rateIn += w.RatePerHour;
                }

                double net = rateIn - ELIMINATION_RATE;

                // À jeun, l'élimination ne creuse pas sous zéro.
                bac = (bac <= 0 && net <= 0) ? 0 : Math.Max(0, bac + net * segmentHours);
                levels[i + 1] = bac;
            }

            for (int i = 0; i < queries.Count; i++)
            {
                int index = ordered.BinarySearch(queries[i]);
                result[i] = index >= 0 ? levels[index] : 0;
            }

            return result;
        }

        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime now)
            => Evaluate(doses, new[] { PkTime.ToOffset(now) })[0];

        public List<(DateTime Time, double Concentration)> GenerateGraph(
            List<DoseEntry> doses, DateTime start, DateTime end, int points = 200)
        {
            double stepMinutes = (end - start).TotalMinutes / points;

            var times = new List<DateTime>(points + 1);
            for (int i = 0; i <= points; i++)
                times.Add(start.AddMinutes(i * stepMinutes));

            double[] values = Evaluate(doses, times.Select(PkTime.ToOffset).ToList());

            var list = new List<(DateTime, double)>(times.Count);
            for (int i = 0; i < times.Count; i++)
                list.Add((times[i], values[i]));

            return list;
        }

        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose) => dose.DoseMg;

        /// <summary>
        /// Quantité encore présente, en unités standard — et non la concentration,
        /// que l'ancienne version retournait, si bien que l'écran affichait deux
        /// fois la même valeur sous deux unités différentes.
        /// </summary>
        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime time)
        {
            double bac = CalculateTotalConcentration(doses, time);
            double volume = UserProfile.WeightKg * GetDiffCoeff();
            return bac * volume / GRAMS_PER_UNIT;
        }

        /// <summary>
        /// Contribution isolée d'une prise, à titre indicatif. Elle ne peut pas être
        /// sommée pour reconstituer le total : l'élimination est commune.
        /// </summary>
        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime time)
            => Evaluate(new List<DoseEntry> { dose }, new[] { PkTime.ToOffset(time) })[0];

        public double CalculateTotalBloodAlcohol(List<DoseEntry> doses, DateTime time)
            => CalculateTotalConcentration(doses, time);

        public EffectLevel GetEffectLevelFromBAC(double bac)
        {
            if (bac >= BAC_STRONG_THRESHOLD) return EffectLevel.Strong;
            if (bac >= BAC_MODERATE_THRESHOLD) return EffectLevel.Moderate;
            if (bac >= BAC_LIGHT_THRESHOLD) return EffectLevel.Light;
            return EffectLevel.None;
        }

        /// <summary>Instant où l'alcoolémie repasse sous le seuil léger.</summary>
        public DateTime? PredictSoberTime(List<DoseEntry> doses, DateTime now)
        {
            if (doses == null || !doses.Any()) return now;

            for (int minutes = 0; minutes <= 72 * 60; minutes += 15)
            {
                DateTime check = now.AddMinutes(minutes);
                if (CalculateTotalConcentration(doses, check) < BAC_LIGHT_THRESHOLD)
                    return check;
            }

            return null;
        }
    }
}
