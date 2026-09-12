﻿using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public enum EffectLevel
    {
        None,
        Light,
        Moderate,
        Strong
    }

    public class BromazepamCalculator : IMoleculeCalculator
    {

        public string DisplayName => "Bromazépam";
        public string DoseUnit => DoseUnits.Milligram;
        public string ConcentrationUnit => "mg/L";

        // Paramètres pharmacocinétiques du bromazépam
        private const double HALF_LIFE_HOURS = 14.0; // Demi-vie moyenne en heures
        private const double ABSORPTION_HALF_LIFE_HOURS = 0.385; // demi-vie d'absorption : pic à 2 h 03
        private const double BIOAVAILABILITY = 0.84; // Fraction absorbée
        /// <summary>
        /// Volume de distribution, ramené de 1,5 à 1,0 L/kg. Le commentaire des
        /// seuils affirmait déjà les avoir calculés pour 1 L/kg, et la littérature
        /// situe le bromazépam autour de cette valeur : le modèle et ses seuils
        /// reposent enfin sur la même hypothèse.
        /// </summary>
        public const double VOLUME_DISTRIBUTION_L_PER_KG = 1.0;

        /// <summary>Concentration produisant la moitié de l'effet maximal (modèle Emax).</summary>
        public const double EC50_MG_PER_L = 0.05;

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka

        // Seuils : pic réellement atteint par une dose de référence, pour 72 kg.
        // Les anciennes valeurs étaient la concentration instantanée F·D/V, sans
        // absorption ni élimination — le seuil « fort » annoncé pour 4,5 mg passait
        // 11 % au-dessus de ce que 4,5 mg peut produire, donc inatteignable.
        public const double STRONG_THRESHOLD = 0.0474;      // pic de 4,5 mg
        public const double MODERATE_THRESHOLD = 0.0316;    // pic de 3 mg
        public const double LIGHT_THRESHOLD = 0.0158;       // pic de 1,5 mg
        public const double NEGLIGIBLE_THRESHOLD = 0.0053;  // pic de 0,5 mg



        public BromazepamCalculator()
        {
            eliminationConstant = Math.Log(2) / HALF_LIFE_HOURS;
            absorptionConstant = Math.Log(2) / ABSORPTION_HALF_LIFE_HOURS;
        }

        // Calcule la concentration pour une dose unique à un moment donné
        public double CalculateSingleDoseConcentration(DoseEntry dose, DateTime currentTime)
        {
            double hoursElapsed = PkTime.ElapsedHours(dose.TimeTaken, currentTime);

            if (hoursElapsed < 0) return 0; // Dose future

            // Modèle pharmacocinétique à un compartiment avec absorption d'ordre 1
            double volume = dose.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG;
            double concentration = (dose.DoseMg * BIOAVAILABILITY * absorptionConstant / (volume * (absorptionConstant - eliminationConstant))) *
                                  (Math.Exp(-eliminationConstant * hoursElapsed) -
                                   Math.Exp(-absorptionConstant * hoursElapsed));

            return Math.Max(0, concentration);
        }

        // Calcule la concentration totale en tenant compte de toutes les doses
        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(dose => CalculateSingleDoseConcentration(dose, currentTime));
        }

        // Retourne la valeur de la dose en unité de concentration (mg pour le bromazépam)
        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose)
        {
            return dose.DoseMg; // L'unité de dose est déjà en mg
        }

        // Calcule la quantité totale restante (en mg) en fonction de la concentration
        public double CalculateTotalAmount(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(d =>
            {
                double conc = CalculateSingleDoseConcentration(d, currentTime);
                double volume = d.WeightKg * VOLUME_DISTRIBUTION_L_PER_KG;
                return conc * volume;
            });
        }

        // Génère des points pour un graphique sur une période donnée
        public List<(DateTime Time, double Concentration)> GenerateGraph(
            List<DoseEntry> doses, DateTime startTime, DateTime endTime, int pointCount = 100)
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

        /// <summary>
        /// Détermine un niveau d'effet à partir du pourcentage de saturation des récepteurs.
        /// Les seuils sont inspirés de la littérature :
        /// 0-30 % = léger, 30-60 % = modéré, 60-80 % = marqué, >80 % = danger.
        /// </summary>
        public EffectLevel GetEffectLevelFromSaturation(double saturationPercent)
        {
            if (saturationPercent >= 80)
                return EffectLevel.Strong;
            if (saturationPercent >= 60)
                return EffectLevel.Moderate;
            if (saturationPercent >= 30)
                return EffectLevel.Light;
            return EffectLevel.None;
        }

        // Prévoit le moment où la concentration passera sous le seuil négligeable
        public DateTime? PredictEffectEndTime(List<DoseEntry> doses, DateTime currentTime)
        {
            if (!doses.Any()) return currentTime;

            for (int minutes = 0; minutes <= 14 * 24 * 60; minutes += 30)
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
