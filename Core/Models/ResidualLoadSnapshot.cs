using System;

namespace MoleculeEfficienceTracker.Core.Models
{
    /// <summary>
    /// Charge résiduelle d'une molécule à un instant donné.
    ///
    /// Porte désormais la concentration en plus de la quantité : les deux étaient
    /// auparavant reconstituées par une division approximative côté statistiques,
    /// avec un volume de distribution deviné depuis la clé — et une valeur par
    /// défaut de 1 L/kg quand la clé n'était pas reconnue, soit un facteur 6,7
    /// d'erreur pour l'ibuprofène.
    /// </summary>
    public class ResidualLoadSnapshot
    {
        public DateTime Timestamp { get; set; }
        public string MoleculeName { get; set; } = string.Empty;

        /// <summary>Quantité encore présente, dans l'unité de saisie de la molécule.</summary>
        public double ResidualAmount { get; set; }

        /// <summary>Concentration correspondante, dans l'unité du calculateur.</summary>
        public double Concentration { get; set; }

        public string AmountUnit { get; set; } = DoseUnits.Milligram;
        public string ConcentrationUnit { get; set; } = "mg/L";

        public ResidualLoadSnapshot() { }

        public ResidualLoadSnapshot(DateTime timestamp, string moleculeName, double amount,
                                    double concentration = 0,
                                    string amountUnit = DoseUnits.Milligram,
                                    string concentrationUnit = "mg/L")
        {
            Timestamp = timestamp;
            MoleculeName = moleculeName;
            ResidualAmount = amount;
            Concentration = concentration;
            AmountUnit = amountUnit;
            ConcentrationUnit = concentrationUnit;
        }
    }
}
