using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class CaffeineCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Caféine";
        public string DoseUnit => "u";
        public string ConcentrationUnit => "mg";

        // Paramètres pharmacocinétiques de la caféine
        private const double HALF_LIFE_HOURS = 5.0; // Demi-vie moyenne en heures (3-7h)
        private const double ABSORPTION_TIME_HOURS = 0.75; // Temps pour atteindre le pic (45 min)

        private const double CAFFEINE_MG_PER_UNIT = 80.0; // ✅ 1 unité = 80mg (Nespresso standard)

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka


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

            double doseMg = dose.DoseMg * CAFFEINE_MG_PER_UNIT;

            // Modèle pharmacocinétique à un compartiment avec absorption d'ordre 1
            // Adapté pour la caféine avec absorption rapide
            double concentration = (doseMg * absorptionConstant / (absorptionConstant - eliminationConstant)) *
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
            return dose.DoseMg * CAFFEINE_MG_PER_UNIT; // Convertit les unités entrées en mg
        }

        // Génère des points pour un graphique sur une période donnée
        // Période plus courte pour la caféine (élimination plus rapide)
        public List<(DateTime Time, double Concentration)> GenerateGraph(
            List<DoseEntry> doses, DateTime startTime, DateTime endTime, int pointCount = 200)
        {
            var points = new List<(DateTime, double)>();
            var timeSpan = endTime - startTime;
            var interval = timeSpan.TotalMinutes / pointCount;

            for (int i = 0; i <= pointCount; i++)
            {
                var currentTime = startTime.AddMinutes(i * interval);
                var concentration = CalculateTotalConcentration(doses, currentTime);
                points.Add((currentTime, concentration));
            }

            return points;
        }

        // Calculer quand la concentration tombera sous le seuil
        public DateTime? GetIneffectiveTime(List<DoseEntry> doses, DateTime currentTime)
        {
            if (!doses.Any()) return null;

            // Chercher dans les prochaines 24h quand ça tombe sous le seuil
            for (int minutes = 0; minutes < 24 * 60; minutes += 15) // Check toutes les 15 min
            {
                var checkTime = currentTime.AddMinutes(minutes);
                var concentration = CalculateTotalConcentration(doses, checkTime);

                if (concentration < GetEffectivenessThreshold())
                {
                    return checkTime;
                }
            }

            return null; // Reste efficace dans les 24h
        }

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
