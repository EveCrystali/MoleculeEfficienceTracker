using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class CaffeineCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Caféine";
        public string Unit => "mg";

        // Paramètres pharmacocinétiques de la caféine
        private const double HALF_LIFE_HOURS = 5.0; // Demi-vie moyenne en heures (3-7h)
        private const double ABSORPTION_TIME_HOURS = 0.75; // Temps pour atteindre le pic (45 min)

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka

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

            // Modèle pharmacocinétique à un compartiment avec absorption d'ordre 1
            // Adapté pour la caféine avec absorption rapide
            double concentration = (dose.DoseMg * absorptionConstant / (absorptionConstant - eliminationConstant)) *
                                  (Math.Exp(-eliminationConstant * hoursElapsed) -
                                   Math.Exp(-absorptionConstant * hoursElapsed));

            return Math.Max(0, concentration);
        }

        // Calcule la concentration totale en tenant compte de toutes les doses
        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(dose => CalculateSingleDoseConcentration(dose, currentTime));
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

        // Méthodes spécifiques à la caféine
        public double GetHalfLifeHours() => HALF_LIFE_HOURS;
        public double GetAbsorptionTimeHours() => ABSORPTION_TIME_HOURS;

        // Estimation du temps pour élimination quasi-complète (5 demi-vies)
        public double GetEliminationTimeHours() => HALF_LIFE_HOURS * 5; // ~25 heures

        // Pic de concentration estimé
        public DateTime GetPeakTime(DateTime doseTime) => doseTime.AddMinutes(45);
    }
}
