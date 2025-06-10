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

        // Seuils d'effet exprimés en mg/L
        public const double STRONG_THRESHOLD = 8.5;      // mg/L : effet fort, pic après 1g
        public const double MODERATE_THRESHOLD = 5.0;    // mg/L : effet net
        public const double LIGHT_THRESHOLD = 2.0;        // mg/L : effet léger
        public const double NEGLIGIBLE_THRESHOLD = 0.8;   // mg/L : effet négligeable

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

        // Détermine le niveau d'effet subjectif en fonction de la concentration
        public EffectLevel GetEffectLevel(double concentration)
        {
            if (concentration >= STRONG_THRESHOLD) return EffectLevel.Strong;
            if (concentration >= MODERATE_THRESHOLD) return EffectLevel.Moderate;
            if (concentration >= LIGHT_THRESHOLD) return EffectLevel.Light;
            return EffectLevel.None;
        }

        // Estime quand la concentration passera sous le seuil négligeable
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

        // Indique si l'effet est négligeable pour une concentration donnée
        public bool IsEffectNegligible(double concentration) => concentration < NEGLIGIBLE_THRESHOLD;
    }
}
