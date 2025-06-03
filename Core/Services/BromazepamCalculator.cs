using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class BromazepamCalculator : IMoleculeCalculator
    {

        public string DisplayName => "Bromazépam";
        public string Unit => "mg";

        // Paramètres pharmacocinétiques du bromazépam
        private const double HALF_LIFE_HOURS = 14.0; // Demi-vie moyenne en heures
        private const double ABSORPTION_TIME_HOURS = 2.0; // Temps pour atteindre le pic

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka

        public BromazepamCalculator()
        {
            eliminationConstant = Math.Log(2) / HALF_LIFE_HOURS;
            absorptionConstant = Math.Log(2) / ABSORPTION_TIME_HOURS;
        }

        // Calcule la concentration pour une dose unique à un moment donné
        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime currentTime)
        {
            double hoursElapsed = (currentTime - dose.TimeTaken).TotalHours;

            if (hoursElapsed < 0) return 0; // Dose future

            // Modèle pharmacocinétique à un compartiment avec absorption d'ordre 1
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
        public List<(DateTime Time, double Concentration)> GenerateGraph(
            List<DoseEntry> doses, DateTime startTime, DateTime endTime, int pointCount = 100)
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
    }
}
