using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class ParacetamolCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Paracétamol";
        public string DoseUnit => DoseUnits.Milligram;
        public string ConcentrationUnit => "mg/L";

        private const double HALF_LIFE_HOURS = 2.5; // Demi-vie moyenne
        /// <summary>
        /// Demi-vie d'absorption, et non délai du pic. L'ancienne constante valait
        /// 0,5 h sous le libellé « temps d'absorption » alors qu'elle alimentait
        /// ka = ln2/T : le pic tombait en réalité bien plus tard. Cette valeur place
        /// effectivement le pic à 30 min.
        /// </summary>
        private const double ABSORPTION_HALF_LIFE_HOURS = 0.104636;
        private const double BIOAVAILABILITY = 0.92; // Fraction absorbée
        public const double VOLUME_DISTRIBUTION_L_PER_KG = 0.95; // Volume de distribution

        // Seuils ancrés sur le pic que produit réellement une dose de référence,
        // pour 72 kg. Les anciennes valeurs dataient du modèle mal calibré : le
        // seuil « fort » était inatteignable avec la dose qui le nommait.
        public const double STRONG_THRESHOLD = 11.71;   // pic d'une prise de 1 g
        public const double MODERATE_THRESHOLD = 5.85;   // pic de 500 mg
        public const double LIGHT_THRESHOLD = 2.34;   // pic de 200 mg
        public const double NEGLIGIBLE_THRESHOLD = 0.88;   // pic de 75 mg

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka

        public ParacetamolCalculator()
        {
            eliminationConstant = Math.Log(2) / HALF_LIFE_HOURS;
            absorptionConstant = Math.Log(2) / ABSORPTION_HALF_LIFE_HOURS;
        }

        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime currentTime)
        {
            double hoursElapsed = PkTime.ElapsedHours(dose.TimeTaken, currentTime);
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
                Instant = PkTime.ToOffset(d.TimeTaken),
                A = (d.DoseMg * BIOAVAILABILITY * absorptionConstant) /
                    (d.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG * (absorptionConstant - eliminationConstant))
            }).ToList();

            for (int i = 0; i <= pointCount; i++)
            {
                var currentTime = startTime.AddMinutes(i * interval);
                DateTimeOffset currentInstant = PkTime.ToOffset(currentTime);
                double total = 0;
                foreach (var p in doseParams)
                {
                    double hoursElapsed = (currentInstant - p.Instant).TotalHours;
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
