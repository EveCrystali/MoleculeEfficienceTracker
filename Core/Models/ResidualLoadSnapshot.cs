using System;

namespace MoleculeEfficienceTracker.Core.Models
{
    /// <summary>
    /// Represents the estimated remaining amount of a molecule at a specific time.
    /// </summary>
    public class ResidualLoadSnapshot
    {
        public DateTime Timestamp { get; set; }
        public string MoleculeName { get; set; } = string.Empty;
        public double ResidualAmount { get; set; }

        public ResidualLoadSnapshot() { }

        public ResidualLoadSnapshot(DateTime timestamp, string moleculeName, double amount)
        {
            Timestamp = timestamp;
            MoleculeName = moleculeName;
            ResidualAmount = amount;
        }
    }
}