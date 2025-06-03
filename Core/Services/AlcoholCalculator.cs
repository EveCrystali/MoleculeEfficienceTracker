using System;
using System.Collections.Generic;
using System.Linq;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class AlcoholCalculator
    {
        public string DisplayName => "Alcool";

        // Paramètres pharmacocinétiques de l'alcool
        private const double ELIMINATION_RATE_UNITS_PER_HOUR = 1.0; // 1 unité par heure (approximation)
        private const double ABSORPTION_TIME_HOURS = 0.75; // Temps pour atteindre le pic (45 min)
        
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

            if (hoursElapsed < 0) return 0; // Dose future

            // Phase d'absorption (0-1.5h environ)
            if (hoursElapsed <= ABSORPTION_TIME_HOURS * 2)
            {
                // Absorption avec pic à ABSORPTION_TIME_HOURS
                double absorptionFactor = Math.Min(1.0, hoursElapsed / ABSORPTION_TIME_HOURS);
                double peakConcentration = dose.DoseMg * 0.8; // Facteur de conversion unité -> concentration
                
                // Élimination linéaire dès le début
                double eliminated = ELIMINATION_RATE_UNITS_PER_HOUR * hoursElapsed;
                double currentLevel = (peakConcentration * absorptionFactor) - eliminated;
                
                return Math.Max(0, currentLevel);
            }
            else
            {
                // Phase d'élimination pure (linéaire)
                double peakConcentration = dose.DoseMg * 0.8;
                double eliminated = ELIMINATION_RATE_UNITS_PER_HOUR * hoursElapsed;
                double currentLevel = peakConcentration - eliminated;
                
                return Math.Max(0, currentLevel);
            }
        }

        // Calcule la concentration totale en tenant compte de toutes les doses
        public double CalculateTotalConcentration(List<DoseEntry> doses, DateTime currentTime)
        {
            return doses.Sum(dose => CalculateSingleDoseConcentrationLinear(dose, currentTime));
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
                var concentration = CalculateTotalConcentration(doses, currentTime);
                points.Add((currentTime, concentration));
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

        // Conversion utilitaire
        public static class AlcoholUnits
        {
            public const double BEER_330ML = 1.3;      // Bière 5% - 33cl
            public const double WINE_GLASS = 1.5;      // Vin 12% - 12.5cl
            public const double SPIRITS_SHOT = 1.0;    // Spiritueux 40% - 2.5cl
            public const double CHAMPAGNE_GLASS = 1.5; // Champagne 12% - 12.5cl
            public const double BEER_500ML = 2.0;      // Bière 5% - 50cl
            public const double WINE_BOTTLE = 9.0;     // Bouteille de vin 75cl
        }
    }
}
