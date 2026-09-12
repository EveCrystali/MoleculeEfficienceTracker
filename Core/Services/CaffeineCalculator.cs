using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class CaffeineCalculator : IMoleculeCalculator
    {
        public string DisplayName => "Caféine";
        public string DoseUnit => DoseUnits.Milligram;
        public string ConcentrationUnit => "mg/L";

        private const double HALF_LIFE_HOURS = 5.0;

        /// <summary>
        /// Demi-vie d'absorption, et non délai du pic.
        ///
        /// L'ancienne constante valait 0,75 h avec le commentaire « temps pour
        /// atteindre le pic (45 min) », mais elle était injectée dans ka = ln2/T.
        /// Le pic d'un modèle de Bateman vaut ln(ka/ke)/(ka−ke) : la courbe
        /// culminait en réalité à 2 h 25, pendant que GetPeakTime annonçait 45 min.
        /// La valeur ci-dessous est celle qui place effectivement le pic à 45 min.
        /// </summary>
        private const double ABSORPTION_HALF_LIFE_HOURS = 0.141768;

        public const double MG_PER_UNIT = 80.0; // 1 Nespresso standard
        public const double VOLUME_DISTRIBUTION_L_PER_KG = 0.65;
        private const double BIOAVAILABILITY = 1.0;

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant;  // ka

        // Seuils d'effet, en mg/L. Repère : un espresso de 80 mg culmine à
        // 1,54 mg/L pour 72 kg.
        public const double STRONG_THRESHOLD = 8.0;
        public const double MODERATE_THRESHOLD = 3.0;
        public const double LIGHT_THRESHOLD = 1.0;
        public const double NEGLIGIBLE_THRESHOLD = 0.3;

        /// <summary>Concentration au-delà de laquelle l'endormissement est gêné.</summary>
        public const double DEFAULT_SLEEP_THRESHOLD = 1.0;

        public CaffeineCalculator()
        {
            eliminationConstant = Math.Log(2) / HALF_LIFE_HOURS;
            absorptionConstant = Math.Log(2) / ABSORPTION_HALF_LIFE_HOURS;
        }

        /// <summary>Délai réel du pic après la prise, déduit du modèle.</summary>
        public double PeakDelayHours =>
            Math.Log(absorptionConstant / eliminationConstant) / (absorptionConstant - eliminationConstant);

        /// <summary>Concentration produite par une dose, t heures après la prise.</summary>
        public double ConcentrationAfterHours(double doseMg, double weightKg, double hoursElapsed)
        {
            if (hoursElapsed < 0) return 0;

            double volume = weightKg * VOLUME_DISTRIBUTION_L_PER_KG;
            double concentration = (doseMg * BIOAVAILABILITY * absorptionConstant /
                                   (volume * (absorptionConstant - eliminationConstant))) *
                                  (Math.Exp(-eliminationConstant * hoursElapsed) -
                                   Math.Exp(-absorptionConstant * hoursElapsed));

            return Math.Max(0, concentration);
        }

        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime currentTime)
            => ConcentrationAfterHours(dose.DoseMg, dose.WeightKg, PkTime.ElapsedHours(dose.TimeTaken, currentTime));

        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime currentTime)
            => doses.Sum(dose => CalculateSingleDoseConcentration(dose, currentTime));

        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose) => dose.DoseMg;

        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime currentTime)
            => doses.Sum(d => CalculateSingleDoseConcentration(d, currentTime) * d.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG);

        public List<(DateTime Time, double Concentration)> GenerateGraph(
            List<DoseEntry> doses, DateTime startTime, DateTime endTime, int pointCount = 200)
        {
            var points = new List<(DateTime, double)>(pointCount + 1);
            double interval = (endTime - startTime).TotalMinutes / pointCount;

            // Les instants absolus sont résolus une fois, hors de la boucle.
            var doseParams = doses.Select(d => new
            {
                Instant = PkTime.ToOffset(d.TimeTaken),
                A = (d.DoseMg * BIOAVAILABILITY * absorptionConstant) /
                    (d.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG * (absorptionConstant - eliminationConstant))
            }).ToList();

            for (int i = 0; i <= pointCount; i++)
            {
                DateTime currentTime = startTime.AddMinutes(i * interval);
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

        public EffectLevel GetEffectLevel(double concentration)
        {
            if (concentration >= STRONG_THRESHOLD) return EffectLevel.Strong;
            if (concentration >= MODERATE_THRESHOLD) return EffectLevel.Moderate;
            if (concentration >= LIGHT_THRESHOLD) return EffectLevel.Light;
            return EffectLevel.None;
        }

        public DateTime? PredictEffectEndTime(List<DoseEntry> doses, DateTime currentTime)
        {
            if (!doses.Any()) return currentTime;

            for (int minutes = 0; minutes <= 24 * 60; minutes += 15)
            {
                DateTime checkTime = currentTime.AddMinutes(minutes);
                if (CalculateTotalConcentration(doses, checkTime) < NEGLIGIBLE_THRESHOLD)
                    return checkTime;
            }

            return null;
        }

        /// <summary>
        /// Heure limite de la prochaine prise pour être sous <paramref name="threshold"/>
        /// à l'heure du coucher.
        ///
        /// C'est la réponse que l'écran affiche en une phrase. Retourne null si le
        /// socle déjà en place dépasse à lui seul le seuil — auquel cas aucune
        /// heure ne convient, et le dire est plus utile que proposer un horaire faux.
        /// </summary>
        public DateTime? LatestIntakeTimeBefore(
            List<DoseEntry> existingDoses,
            DateTime bedTime,
            double plannedDoseMg,
            double weightKg,
            DateTime notEarlierThan,
            double threshold = DEFAULT_SLEEP_THRESHOLD)
        {
            double baseline = CalculateTotalConcentration(existingDoses, bedTime);
            double headroom = threshold - baseline;

            if (headroom <= 0) return null;

            // La contribution d'une dose au coucher décroît quand on avance sa
            // prise : on cherche la prise la plus tardive qui tienne dans la marge.
            DateTime? best = null;
            for (int minutes = 0; ; minutes += 5)
            {
                DateTime candidate = bedTime.AddMinutes(-minutes);
                if (candidate < notEarlierThan) break;

                double hours = PkTime.ElapsedHours(candidate, bedTime);
                if (ConcentrationAfterHours(plannedDoseMg, weightKg, hours) <= headroom)
                {
                    best = candidate;
                    break;
                }

                if (minutes > 24 * 60) break;
            }

            return best;
        }

        public bool IsEffectNegligible(double concentration) => concentration < NEGLIGIBLE_THRESHOLD;

        public double GetHalfLifeHours() => HALF_LIFE_HOURS;
        public double GetEliminationTimeHours() => HALF_LIFE_HOURS * 5;

        /// <summary>Instant du pic pour une prise donnée, cohérent avec la courbe tracée.</summary>
        public DateTime GetPeakTime(DateTime doseTime) => doseTime.AddHours(PeakDelayHours);
    }
}
