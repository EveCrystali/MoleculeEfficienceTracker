using System;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Modèle pharmacodynamique simple de type Emax (coefficient de Hill n = 1).
    /// Relie la concentration en mg/L à un effet relatif exprimé en pourcentage.
    /// </summary>
    public class PharmacodynamicModel
    {
        /// <summary>
        /// Effet maximal en pourcentage (par exemple 100 = effet maximal).
        /// </summary>
        public const double E_MAX_PERCENT = 100.0;

        /// <summary>
        /// Concentration pour 50 % d'effet (EC50).
        /// </summary>
        public readonly double EC50;

        /// <summary>
        /// Initialise un nouveau modèle avec une valeur EC50 spécifique.
        /// </summary>
        /// <param name="ec50">Concentration en mg/L donnant 50 % d'effet.</param>
        public PharmacodynamicModel(double ec50)
        {
            EC50 = ec50;
        }

        /// <summary>
        /// Retourne l'effet en pourcentage pour la concentration donnée.
        /// </summary>
        /// <param name="concentrationMgPerL">Concentration plasmatique mg/L.</param>
        public double GetEffectPercent(double concentrationMgPerL)
        {
            if (concentrationMgPerL <= 0)
                return 0.0;

            double ratio = concentrationMgPerL / (concentrationMgPerL + EC50);
            return E_MAX_PERCENT * ratio;
        }

        /// <summary>
        /// Variante retournant l'effet sous forme fractionnaire (0 à 1).
        /// </summary>
        public double GetEffectFraction(double concentrationMgPerL)
        {
            if (concentrationMgPerL <= 0)
                return 0.0;

            return concentrationMgPerL / (concentrationMgPerL + EC50);
        }
    }
}
