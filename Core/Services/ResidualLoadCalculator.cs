using System;

namespace MoleculeEfficienceTracker.Core.Services
{
    public static class ResidualLoadCalculator
    {
        /// <summary>
        /// Returns the remaining amount of a molecule after a given time using its half-life.
        /// Q(t) = Q0 * exp(-ln(2) * t / halfLife)
        /// </summary>
        public static double GetResidualAmount(double initialDose, double halfLifeHours, double hoursElapsed)
        {
            return initialDose * Math.Exp(-0.693 * hoursElapsed / halfLifeHours);
        }
    }
}