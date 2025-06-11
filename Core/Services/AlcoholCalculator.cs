using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Calculator implementing a simple alcohol pharmacokinetic model with
    /// bicompartmental absorption and zero order elimination.
    /// Dose values are expressed in "units" (1 unit = 10 g of pure alcohol).
    /// </summary>
    public class AlcoholCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Alcool";
        public string DoseUnit => "u";
        public string ConcentrationUnit => "g/L";

        // Elimination rate of blood alcohol concentration (g/L per hour)
        private const double DEFAULT_ELIMINATION_BAC_RATE = 0.15;

        // Default absorption time used when no beverage type is provided (hours)
        private const double DEFAULT_ABSORPTION_TIME = 0.75;

        // Conversion constants
        private const double GRAMS_PER_UNIT = 10.0; // 1 unit = 10 g

        // Fractions for the fast and slow absorption compartments
        private const double FAST_FRACTION = 0.3;
        private const double SLOW_FRACTION = 1.0 - FAST_FRACTION;

        // Subjective effect thresholds for BAC expressed in g/L
        // These values reflect increasing behavioral and cognitive impairment
        // and align with French legal thresholds and clinical observations

        public const double BAC_STRONG_THRESHOLD = 1.2;   // Effets nets : Altérations nettes, ivresse visible, incoordination, parole pâteuse
        public const double BAC_MODERATE_THRESHOLD = 0.5; // Légèrement euphorique, réflexes ralentis (limite légale FR)
        public const double BAC_LIGHT_THRESHOLD = 0.2;    // Début de désinhibition, légers effets cognitifs
        public const double BAC_NEGLIGIBLE_THRESHOLD = 0.1; // Effet quasi-nul, seuil physiologique bas


        // Absorption time depending on beverage type (hours)
        private static readonly Dictionary<string, double> _absorptionTimes = new()
        {
            ["cocktail"] = 2.0,
            ["vin"] = 1.0,
            ["biere"] = 0.5,
            ["cidre"] = 0.75,
            ["spiritueux"] = 1.5
        };

        /// <summary>
        /// List of supported beverage types exposed for the UI.
        /// </summary>
        public static IEnumerable<string> KnownBeverageTypes => _absorptionTimes.Keys;

        /// <summary>
        /// Default beverage type used for all doses. UI does not expose this yet
        /// so the same value is applied to every entry.
        /// </summary>
        public string BeverageType { get; set; } = string.Empty;

        private double GetAbsorptionTime()
        {
            return _absorptionTimes.TryGetValue(BeverageType?.ToLowerInvariant() ?? string.Empty, out var t)
                ? t
                : DEFAULT_ABSORPTION_TIME;
        }

        private static double GetDiffusionCoefficient()
        {
            var sex = UserPreferences.GetSex()?.ToLowerInvariant();
            return sex switch
            {
                "homme" => 0.7,
                "femme" => 0.6,
                _ => 0.68
            };
        }

        private double CalculateAbsorbedGrams(double totalGrams, double timeHours)
        {
            double tAbs = GetAbsorptionTime();
            // Two-compartment first order absorption
            double kFast = Math.Log(2) / (tAbs * 0.5);
            double kSlow = Math.Log(2) / tAbs;
            double absorbed = totalGrams * (
                FAST_FRACTION * (1.0 - Math.Exp(-kFast * timeHours)) +
                SLOW_FRACTION * (1.0 - Math.Exp(-kSlow * timeHours))
            );
            return Math.Min(absorbed, totalGrams);
        }

        private double CalculateRemainingUnits(DoseEntry dose, DateTime time)
        {
            double gramsLeft = CalculateRemainingGrams(dose, time);
            return gramsLeft / GRAMS_PER_UNIT;
        }

        private double CalculateSingleDoseBAC(DoseEntry dose, DateTime time)
        {
            double gramsLeft = CalculateRemainingGrams(dose, time);
            double volume = dose.WeightKg * GetDiffusionCoefficient();
            return volume > 0 ? gramsLeft / volume : 0;
        }

        /// <summary>
        /// Numerically integrates absorbed grams and linear elimination to avoid
        /// negative concentrations when absorption is slow.
        /// </summary>
        private double CalculateRemainingGrams(DoseEntry dose, DateTime time)
        {
            double totalHours = (time - dose.TimeTaken).TotalHours;
            if (totalHours <= 0) return 0;

            double tAbs = GetAbsorptionTime();
            double kFast = Math.Log(2) / (tAbs * 0.5);
            double kSlow = Math.Log(2) / tAbs;
            double totalGrams = dose.DoseMg * GRAMS_PER_UNIT;
            double volume = dose.WeightKg * GetDiffusionCoefficient();
            double eliminationRate = DEFAULT_ELIMINATION_BAC_RATE * volume; // g/h

            double dt = 1.0 / 60.0; // 1 minute steps
            int steps = (int)Math.Ceiling(totalHours / dt);
            double grams = 0.0;
            double prevAbs = 0.0;

            for (int i = 1; i <= steps; i++)
            {
                double t = Math.Min(i * dt, totalHours);
                double absorbed = totalGrams * (
                    FAST_FRACTION * (1.0 - Math.Exp(-kFast * t)) +
                    SLOW_FRACTION * (1.0 - Math.Exp(-kSlow * t)));
                double deltaAbs = absorbed - prevAbs;
                if (deltaAbs < 0) deltaAbs = 0;
                grams += deltaAbs;
                prevAbs = absorbed;

                grams -= eliminationRate * dt;
                if (grams < 0) grams = 0;
            }

            return grams;
        }

        // --- IMoleculeCalculator implementation ---
        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime time)
        {
            return doses.Sum(d => CalculateSingleDoseBAC(d, time));
        }

        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime time)
        {
            return CalculateSingleDoseBAC(dose, time);
        }

        public List<(DateTime Time, double Concentration)> GenerateGraph(List<DoseEntry> doses, DateTime startTime, DateTime endTime, int points = 200)
        {
            var list = new List<(DateTime, double)>();
            var span = endTime - startTime;
            double step = span.TotalMinutes / points;

            for (int i = 0; i <= points; i++)
            {
                var t = startTime.AddMinutes(i * step);
                var bac = CalculateTotalConcentration(doses, t);
                list.Add((t, bac));
            }

            return list;
        }

        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose) => dose.DoseMg;

        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime time)
        {
            return doses.Sum(d => CalculateRemainingUnits(d, time));
        }

        // --- Additional helpers specific to alcohol ---
        public double CalculateTotalBloodAlcohol(List<DoseEntry> doses, DateTime time) => CalculateTotalConcentration(doses, time);

        public EffectLevel GetEffectLevelFromBAC(double bac)
        {
            if (bac >= BAC_STRONG_THRESHOLD) return EffectLevel.Strong;
            if (bac >= BAC_MODERATE_THRESHOLD) return EffectLevel.Moderate;
            if (bac >= BAC_LIGHT_THRESHOLD) return EffectLevel.Light;
            if (bac >= BAC_NEGLIGIBLE_THRESHOLD) return EffectLevel.Light;
            return EffectLevel.None;
        }

        public DateTime? PredictSoberTime(List<DoseEntry> doses, DateTime now)
        {
            if (!doses.Any()) return now;
            for (int m = 0; m <= 72 * 60; m += 15)
            {
                var check = now.AddMinutes(m);
                if (CalculateTotalConcentration(doses, check) < 0.2)
                    return check;
            }
            return null;
        }
    }
}
