using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class CaffeineCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Caféine";
        public string DoseUnit => "mg";
        public string ConcentrationUnit => "mg/L";

        // Paramètres pharmacocinétiques de la caféine
        private const double HALF_LIFE_HOURS = 5.0; // Demi-vie moyenne en heures (3-7h)
        private const double ABSORPTION_TIME_HOURS = 0.75; // Temps pour atteindre le pic (45 min)

        public const double MG_PER_UNIT = 80.0; // 1 unité = 80mg (Nespresso standard)
        public const double VOLUME_DISTRIBUTION_L_PER_KG = 0.65; // Volume de distribution moyen
        private const double BIOAVAILABILITY = 1.0; // Fraction absorbée (≈100 %)

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka

        // Seuils d'effet exprimés en mg/L
        public const double STRONG_THRESHOLD = 8.0;      // mg/L : effet fort/toxique
        public const double MODERATE_THRESHOLD = 3.0;     // mg/L : effet net
        public const double LIGHT_THRESHOLD = 1;        // mg/L : effet léger
        public const double NEGLIGIBLE_THRESHOLD = 0.3;   // mg/L : effet négligeable


        private const double MINIMUM_EFFECTIVE_DOSE_MG_PER_KG = 0.5; // 0.5 mg/kg
        private const double AVERAGE_BODY_WEIGHT_KG = 70.0; // Poids moyen

        // Seuil calculé dynamiquement
        public static double GetEffectivenessThreshold(double bodyWeightKg = AVERAGE_BODY_WEIGHT_KG)
        {
            return MINIMUM_EFFECTIVE_DOSE_MG_PER_KG * bodyWeightKg; // 35mg pour 70kg
        }

        public CaffeineCalculator()
        {
            eliminationConstant = Math.Log(2) / HALF_LIFE_HOURS; // ke = 0.139 h⁻¹
            absorptionConstant = Math.Log(2) / ABSORPTION_TIME_HOURS; // ka = 0.924 h⁻¹
        }

        // Calcule la concentration pour une dose unique à un moment donné
        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime currentTime)
        {
            double hoursElapsed = (currentTime - dose.TimeTaken).TotalHours;

            if (hoursElapsed < 0) return 0; // Dose future

            double doseMg = dose.DoseMg;

            double volume = dose.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG;

            // Modèle pharmacocinétique à un compartiment avec absorption d'ordre 1
            // Adapté pour la caféine avec absorption rapide
            double concentration = (doseMg * BIOAVAILABILITY * absorptionConstant /
                                  (volume * (absorptionConstant - eliminationConstant))) *
                                 (Math.Exp(-eliminationConstant * hoursElapsed) -
                                  Math.Exp(-absorptionConstant * hoursElapsed));

            return Math.Max(0, concentration);
        }

        // Calcule la concentration totale en tenant compte de toutes les doses
        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(dose => CalculateSingleDoseConcentration(dose, currentTime));
        }

        // Retourne la valeur de la dose en unité de concentration (mg pour la caféine)
        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose)
        {
            // La dose est désormais saisie directement en mg
            return dose.DoseMg;
        }

        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime currentTime)
        {
            // Convertit la concentration totale (mg/L) en quantité restante (mg)
            double totalMg = doses.Sum(d =>
            {
                double conc = CalculateSingleDoseConcentration(d, currentTime);
                double volume = d.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG;
                return conc * volume;
            });

            return totalMg; // Résultat en mg
        }

        // Génère des points pour un graphique sur une période donnée
        // Période plus courte pour la caféine (élimination plus rapide)
        public List<(DateTime Time, double Concentration)> GenerateGraph(
            List<DoseEntry> doses, DateTime startTime, DateTime endTime, int pointCount = 200)
        {
            var points = new List<(DateTime, double)>();
            var timeSpan = endTime - startTime;
            var interval = timeSpan.TotalMinutes / pointCount;

            var doseParams = doses.Select(d => new
            {
                d.TimeTaken,
                A = (d.DoseMg * BIOAVAILABILITY * absorptionConstant) /
                    (d.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG * (absorptionConstant - eliminationConstant))
            }).ToList();

            for (int i = 0; i <= pointCount; i++)
            {
                var currentTime = startTime.AddMinutes(i * interval);
                double total = 0;
                foreach (var p in doseParams)
                {
                    double hoursElapsed = (currentTime - p.TimeTaken).TotalHours;
                    if (hoursElapsed < 0) continue;
                    double conc = p.A * (Math.Exp(-eliminationConstant * hoursElapsed) - Math.Exp(-absorptionConstant * hoursElapsed));
                    if (conc > 0) total += conc;
                }
                points.Add((currentTime, total));
            }

            return points;
        }

        // Détermine le niveau d'effet subjectif
        public EffectLevel GetEffectLevel(double concentration)
        {
            if (concentration >= STRONG_THRESHOLD) return EffectLevel.Strong;
            if (concentration >= MODERATE_THRESHOLD) return EffectLevel.Moderate;
            if (concentration >= LIGHT_THRESHOLD) return EffectLevel.Light;
            return EffectLevel.None;
        }

        // Calculer quand la concentration tombera sous le seuil négligeable
        public DateTime? PredictEffectEndTime(List<DoseEntry> doses, DateTime currentTime)
        {
            if (!doses.Any()) return currentTime;

            for (int minutes = 0; minutes <= 24 * 60; minutes += 15)
            {
                DateTime checkTime = currentTime.AddMinutes(minutes);
                double conc = CalculateTotalConcentration(doses, checkTime);
                if (conc < NEGLIGIBLE_THRESHOLD)
                    return checkTime;
            }

            return null;
        }

        // Compatibilité avec l'ancien nom de méthode
        public DateTime? GetIneffectiveTime(List<DoseEntry> doses, DateTime currentTime) =>
            PredictEffectEndTime(doses, currentTime);

        // Indique si l'effet est négligeable pour une concentration donnée
        public bool IsEffectNegligible(double concentration) => concentration < NEGLIGIBLE_THRESHOLD;

        public static class CaffeineUnits
        {
            public const double NESPRESSO_STANDARD = 1.0;      // 80mg
            public const double NESPRESSO_LUNGO = 1.2;         // ~95mg  
            public const double NESPRESSO_KAZAAR = 1.8;        // 142mg (le plus fort)
            public const double COFFEE_CUP_REGULAR = 1.2;      // ~95mg
            public const double TEA_CUP = 0.6;                 // ~47mg
            public const double COLA_CAN = 0.4;                // ~35mg
        }

        // Méthodes spécifiques à la caféine
        public double GetHalfLifeHours() => HALF_LIFE_HOURS;
        public double GetAbsorptionTimeHours() => ABSORPTION_TIME_HOURS;

        // Estimation du temps pour élimination quasi-complète (5 demi-vies)
        public double GetEliminationTimeHours() => HALF_LIFE_HOURS * 5; // ~25 heures

        // Pic de concentration estimé
        public DateTime GetPeakTime(DateTime doseTime) => doseTime.AddMinutes(45);

    }
}
