using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Calculator implementing a simple alcohol pharmacokinetic model with
    /// bicompartmental absorption and zero order elimination.
    /// Dose values are expressed in "units" (1 unit = 10 g of pure alcohol).
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


        // Seuils d'effet subjectif exprimés en mg/L pour le nouveau modèle
        // Ces valeurs correspondent à une dose de 4.5 mg (effet fort) ingérée par
        // défaut chez un patient de 72 kg avec un Vd de 1 L/kg et une
        // biodisponibilité de 84 %.
        public const double BAC_STRONG_THRESHOLD = 0.0525;    // ≈ 4,5 mg
        public const double BAC_MODERATE_THRESHOLD = 0.035;  // ≈ 3 mg
        public const double BAC_LIGHT_THRESHOLD = 0.0175;     // ≈ 1,5 mg
        public const double BAC_NEGLIGIBLE_THRESHOLD = 0.00583; // ≈ 0,5 mg



        // Absorption time depending on beverage type
        private static readonly Dictionary<string, double> _absorptionTimes = new()
        {
            ["cocktail"] = 2.0,
            ["vin"] = 1.0,
            ["biere"] = 0.5
        };

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
            double t = (time - dose.TimeTaken).TotalHours;
            if (t <= 0) return 0;
            double volume = dose.WeightKg * GetDiffusionCoefficient();
            double totalGrams = dose.DoseMg * GRAMS_PER_UNIT;
            double absorbed = CalculateAbsorbedGrams(totalGrams, t);
            double eliminationRate = DEFAULT_ELIMINATION_BAC_RATE * volume; // g/h
            double eliminated = eliminationRate * t;
            double gramsLeft = Math.Max(0, absorbed - eliminated);
            return gramsLeft / GRAMS_PER_UNIT;
        }

        private double CalculateSingleDoseBAC(DoseEntry dose, DateTime time)
        {
            double t = (time - dose.TimeTaken).TotalHours;
            if (t <= 0) return 0;
            double volume = dose.WeightKg * GetDiffusionCoefficient();
            double totalGrams = dose.DoseMg * GRAMS_PER_UNIT;
            double absorbed = CalculateAbsorbedGrams(totalGrams, t);
            double eliminationRate = DEFAULT_ELIMINATION_BAC_RATE * volume; // g/h
            double eliminated = eliminationRate * t;
            double gramsLeft = Math.Max(0, absorbed - eliminated);
            return volume > 0 ? gramsLeft / volume : 0;
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
            if (bac >= 1.0) return EffectLevel.Strong;
            if (bac >= 0.5) return EffectLevel.Moderate;
            if (bac >= 0.2) return EffectLevel.Light;
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
