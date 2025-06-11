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

        // Volume de distribution
        private const double DIFFUSION_HOMME = 0.7;
        private const double DIFFUSION_FEMME = 0.6;

        // Élimination (zéro-ordre)
        private const double ELIMINATION_RATE = 0.15; // g/L·h

        // Seuils d'effet
        public const double BAC_STRONG_THRESHOLD = 1.2;  // g/L
        public const double BAC_MODERATE_THRESHOLD = 0.5;  // g/L
        public const double BAC_LIGHT_THRESHOLD = 0.2;  // g/L
        public const double BAC_NEGLIGIBLE_THRESHOLD = 0.1;  // g/L


        // Conversion dose → grammes
        private const double GRAMS_PER_UNIT = 10.0;
        private const double ALCOHOL_DENSITY = 0.8; // g/mL

        // Temps d’absorption par boisson (h)
        private static readonly Dictionary<string, double> AbsTime = new()
        {
            ["biere"] = 0.33,
            ["cidre"] = 0.4,
            ["vin"] = 0.5,
            ["spiritueux"] = 0.25,
            ["cocktail"] = 0.5,
            ["champagne"] = 0.45, 
            ["liqueur"] = 0.25
        };


        // Liste exposée à l’UI
        public static IEnumerable<string> KnownBeverageTypes => AbsTime.Keys;

        // Propriété choisie par l’utilisateur
        public string BeverageType { get; set; } = KnownBeverageTypes.First();

        /// <summary>
        /// Convert volume (mL) and alcohol percentage into standard units (10 g of pure alcohol).
        /// </summary>
        public static double VolumePercentToUnits(double volumeMl, double percent)
        {
            if (volumeMl <= 0 || percent <= 0) return 0;
            double grams = volumeMl * (percent / 100.0) * ALCOHOL_DENSITY;
            return grams / GRAMS_PER_UNIT;
        }

        private double GetAbsorptionTime(string bev)
            => AbsTime.TryGetValue(bev.ToLower(), out var t) ? t : 0.5;

        private double GetDiffCoeff()
        {
            var s = UserPreferences.GetSex()?.ToLower();
            return s == "femme" ? DIFFUSION_FEMME : DIFFUSION_HOMME;
        }

        private double SingleDoseBAC(DoseEntry d, DateTime now)
        {
            double t = (now - d.TimeTaken).TotalHours;
            if (t <= 0) return 0;

            double D = d.DoseMg * GRAMS_PER_UNIT; // en g
            double V = d.WeightKg * GetDiffCoeff(); // en L
            double T_abs = GetAbsorptionTime(BeverageType);

            // montée linéaire
            if (t < T_abs)
                return (D / V) * (t / T_abs);

            // élimination zéro-ordre
            double C_peak = D / V;
            double C = C_peak - ELIMINATION_RATE * (t - T_abs);
            return Math.Max(0, C);
        }

        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime now)
            => doses.Sum(d => SingleDoseBAC(d, now));

        public List<(DateTime Time, double Concentration)> GenerateGraph(
            List<DoseEntry> doses, DateTime start, DateTime end, int points = 200)
        {
            var list = new List<(DateTime, double)>();
            var span = (end - start).TotalMinutes / points;
            for (int i = 0; i <= points; i++)
            {
                var t = start.AddMinutes(i * span);
                list.Add((t, CalculateTotalConcentration(doses, t)));
            }
            return list;
        }

        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose) => dose.DoseMg;

        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime time)
        {
            return doses.Sum(d => CalculateSingleDoseConcentration(d, time));
        }

        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime time)
    => SingleDoseBAC(dose, time);


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
