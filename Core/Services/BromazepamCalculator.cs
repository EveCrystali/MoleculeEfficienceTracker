﻿﻿using System;
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
        public string DoseUnit => "mg";
        public string ConcentrationUnit => "mg/L";

        // Paramètres pharmacocinétiques du bromazépam
        private const double HALF_LIFE_HOURS = 14.0; // Demi-vie moyenne en heures
        private const double ABSORPTION_TIME_HOURS = 2.0; // Temps pour atteindre le pic
        private const double BIOAVAILABILITY = 0.84; // Fraction absorbée
        public const double VOLUME_DISTRIBUTION_L_PER_KG = 1.5; // Volume de distribution

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka

        // Seuils d'effet subjectif exprimés en mg/L pour le nouveau modèle
        // Ces valeurs correspondent à une dose de 4.5 mg (effet fort) ingérée par
        // défaut chez un patient de 72 kg avec un Vd de 1 L/kg et une
        // biodisponibilité de 84 %.
        public const double STRONG_THRESHOLD = 0.0525;    // ≈ 4,5 mg
        public const double MODERATE_THRESHOLD = 0.035;  // ≈ 3 mg
        public const double LIGHT_THRESHOLD = 0.0175;     // ≈ 1,5 mg
        public const double NEGLIGIBLE_THRESHOLD = 0.00583; // ≈ 0,5 mg



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
