using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class ParacetamolCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Paracétamol";
        public string DoseUnit => "mg";
        public string ConcentrationUnit => "mg/L";

        private const double HALF_LIFE_HOURS = 2.5; // Demi-vie moyenne
        private const double ABSORPTION_TIME_HOURS = 0.5; // Temps d'absorption
        private const double BIOAVAILABILITY = 0.92; // Fraction absorbée
        private const double VOLUME_DISTRIBUTION_L_PER_KG = 0.95; // Volume de distribution

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka

        public ParacetamolCalculator()
        {
            eliminationConstant = Math.Log(2) / HALF_LIFE_HOURS;
            absorptionConstant = Math.Log(2) / ABSORPTION_TIME_HOURS;
        }

        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime currentTime)
        {
            double hoursElapsed = (currentTime - dose.TimeTaken).TotalHours;
            if (hoursElapsed < 0) return 0;

            double volume = dose.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG;
            double concentration = (dose.DoseMg * BIOAVAILABILITY * absorptionConstant / (volume * (absorptionConstant - eliminationConstant))) *
                                  (Math.Exp(-eliminationConstant * hoursElapsed) -
                                   Math.Exp(-absorptionConstant * hoursElapsed));

            return Math.Max(0, concentration);
        }

        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(d => CalculateSingleDoseConcentration(d, currentTime));
        }

        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose)
        {
            return dose.DoseMg;
        }

        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(d =>
            {
                double conc = CalculateSingleDoseConcentration(d, currentTime);
                double volume = d.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG;
                return conc * volume;
            });
        }

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
    }
}
