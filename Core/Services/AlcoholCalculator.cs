using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class AlcoholCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Alcool";
        public string DoseUnit => "u";
        public string ConcentrationUnit => "g/L";

        // Paramètres pharmacocinétiques de l'alcool
        private const double ELIMINATION_RATE_UNITS_PER_HOUR = 1.0; // 1 unité par heure (approximation)
        private const double ABSORPTION_TIME_HOURS = 0.75; // Temps pour atteindre le pic (45 min)

        // Conversion unité -> grammes et volume de distribution approximatif
        public const double GRAMS_PER_UNIT = 10.0;           // 1 unité ≈ 10 g d'alcool pur
        private const double BODY_WATER_RATIO = 0.68;         // Coefficient de Widmark moyen (L/kg)

        // Seuils d'effets exprimés en g/L
        public const double BAC_STRONG_THRESHOLD = 1.0;      // g/L : ivresse marquée
        public const double BAC_MODERATE_THRESHOLD = 0.5;    // g/L : limite légale FR
        public const double BAC_LIGHT_THRESHOLD = 0.2;       // g/L : effets légers
        public const double BAC_NEGLIGIBLE_THRESHOLD = 0.1;  // g/L : quasiment nul

        // Pour l'alcool, l'élimination suit une cinétique d'ordre zéro (linéaire)
        // mais on peut approximer avec un modèle de premier ordre pour simplifier
        private const double APPARENT_HALF_LIFE_HOURS = 4.5; // Approximation pour modélisation

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka

        public AlcoholCalculator()
        {
            eliminationConstant = Math.Log(2) / APPARENT_HALF_LIFE_HOURS; // ke = 0.154 h⁻¹
            absorptionConstant = Math.Log(2) / ABSORPTION_TIME_HOURS; // ka = 0.924 h⁻¹
        }

        // Calcule la concentration pour une dose unique à un moment donné
        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime currentTime)
        {
            double hoursElapsed = (currentTime - dose.TimeTaken).TotalHours;

            if (hoursElapsed < 0) return 0; // Dose future

            // Modèle simplifié d'ordre 1 pour l'alcool
            // Note: L'alcool suit normalement une cinétique d'ordre zéro, mais ce modèle
            // donne une approximation acceptable pour un usage personnel
            double concentration = (dose.DoseMg * absorptionConstant / (absorptionConstant - eliminationConstant)) *
                                  (Math.Exp(-eliminationConstant * hoursElapsed) -
                                   Math.Exp(-absorptionConstant * hoursElapsed));

            return Math.Max(0, concentration);
        }

        // Modèle alternatif plus précis avec élimination linéaire
        public double CalculateSingleDoseConcentrationLinear(DoseEntry dose, DateTime currentTime)
        {
            double hoursElapsed = (currentTime - dose.TimeTaken).TotalHours;

            if (hoursElapsed < 0) return 0;

            if (hoursElapsed <= ABSORPTION_TIME_HOURS)
            {
                return dose.DoseMg * (hoursElapsed / ABSORPTION_TIME_HOURS);
            }
            else
            {
                double absorbed = dose.DoseMg;
                double hoursSinceAbsorption = hoursElapsed - ABSORPTION_TIME_HOURS;
                double remaining = absorbed - (ELIMINATION_RATE_UNITS_PER_HOUR * hoursSinceAbsorption);
                return Math.Max(0, remaining);
            }
        }


        // Calcule le BAC total en g/L en tenant compte de toutes les doses
        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(d => CalculateSingleDoseBloodAlcohol(d, currentTime));
        }

        // Retourne la valeur de la dose en unité de concentration (unités pour l'alcool)
        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose)
        {
            // Si DoseUnit et ConcentrationUnit sont "u", DoseMg est la valeur correcte.
            return dose.DoseMg;
        }

        // Quantité totale restante en unités
        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(d => CalculateSingleDoseConcentrationLinear(d, currentTime));
        }

        // --- Nouveau calcul : concentration sanguine en g/L (BAC) ---
        public double CalculateSingleDoseBloodAlcohol(DoseEntry dose, DateTime currentTime)
        {
            double units = CalculateSingleDoseConcentrationLinear(dose, currentTime);
            double grams = units * GRAMS_PER_UNIT;
            double volume = dose.WeightKg * BODY_WATER_RATIO;
            return volume > 0 ? grams / volume : 0;
        }

        public double CalculateTotalBloodAlcohol(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(d => CalculateSingleDoseBloodAlcohol(d, currentTime));
        }

        public EffectLevel GetEffectLevelFromBAC(double bac)
        {
            if (bac >= BAC_STRONG_THRESHOLD) return EffectLevel.Strong;
            if (bac >= BAC_MODERATE_THRESHOLD) return EffectLevel.Moderate;
            if (bac >= BAC_LIGHT_THRESHOLD) return EffectLevel.Light;
            return EffectLevel.None;
        }

        // Prévision du retour sous le seuil légale
        public DateTime? PredictSoberTime(List<DoseEntry> doses, DateTime currentTime)
        {
            if (!doses.Any()) return currentTime;

            for (int minutes = 0; minutes <= 72 * 60; minutes += 15)
            {
                DateTime check = currentTime.AddMinutes(minutes);
                double bac = CalculateTotalBloodAlcohol(doses, check);
                if (bac < BAC_LIGHT_THRESHOLD)
                    return check;
            }

            return null;
        }

        // Génère des points pour un graphique sur une période donnée
        public List<(DateTime Time, double Concentration)> GenerateGraph(
            List<DoseEntry> doses, DateTime startTime, DateTime endTime, int pointCount = 200)
        {
            var points = new List<(DateTime, double)>();
            var timeSpan = endTime - startTime;
            var interval = timeSpan.TotalMinutes / pointCount;

            for (int i = 0; i <= pointCount; i++)
            {
                var currentTime = startTime.AddMinutes(i * interval);
                double bac = CalculateTotalBloodAlcohol(doses, currentTime);
                points.Add((currentTime, bac));
            }

            return points;
        }

        // Méthodes spécifiques à l'alcool
        public double GetEliminationRatePerHour() => ELIMINATION_RATE_UNITS_PER_HOUR;
        public double GetAbsorptionTimeHours() => ABSORPTION_TIME_HOURS;

        // Estimation du temps pour élimination complète d'une unité
        public double GetEliminationTimeForOneUnit() => 1.0; // 1 heure par unité

        // Pic de concentration estimé
        public DateTime GetPeakTime(DateTime doseTime) => doseTime.AddMinutes(45);

        // Estimation du temps pour retour à zéro
        public DateTime GetSoberTime(List<DoseEntry> doses)
        {
            if (!doses.Any()) return DateTime.Now;

            double totalUnits = doses.Sum(d => d.DoseMg);
            double hoursToEliminate = totalUnits; // 1 heure par unité
            DateTime lastDose = doses.Max(d => d.TimeTaken);

            return lastDose.AddHours(hoursToEliminate);
        }

    }
}
