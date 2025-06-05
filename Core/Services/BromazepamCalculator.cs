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
        public string DoseUnit => "mg";
        public string ConcentrationUnit => "mg";

        // Paramètres pharmacocinétiques du bromazépam
        private const double HALF_LIFE_HOURS = 14.0; // Demi-vie moyenne en heures
        private const double ABSORPTION_TIME_HOURS = 2.0; // Temps pour atteindre le pic

        private readonly double eliminationConstant; // ke
        private readonly double absorptionConstant; // ka

        // Seuils d'effet subjectif (mg)
        public const double STRONG_THRESHOLD = 3.0;
        public const double MODERATE_THRESHOLD = 1.5;
        public const double LIGHT_THRESHOLD = 0.5;
        public const double NEGLIGIBLE_THRESHOLD = 0.2;

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

        // Retourne la valeur de la dose en unité de concentration (mg pour le bromazépam)
        public double GetDoseDisplayValueInConcentrationUnit(DoseEntry dose)
        {
            return dose.DoseMg; // L'unité de dose est déjà en mg
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

        // Détermine le niveau d'effet subjectif en fonction de la concentration
        public EffectLevel GetEffectLevel(double concentration)
        {
            if (concentration >= STRONG_THRESHOLD) return EffectLevel.Strong;
            if (concentration >= MODERATE_THRESHOLD) return EffectLevel.Moderate;
            if (concentration >= LIGHT_THRESHOLD) return EffectLevel.Light;
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
