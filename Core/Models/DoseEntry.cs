using System;

namespace MoleculeEfficienceTracker.Core.Models
{
    public class DoseEntry
    {
        public DateTime TimeTaken { get; set; }
        public double DoseMg { get; set; }
        public double WeightKg { get; set; } = 72.0; // Poids par défaut si non renseigné
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Ajout pour MAUI
        public string MoleculeKey { get; set; } = string.Empty; // Identifie la molécule

        public DoseEntry(DateTime timeTaken, double doseMg, double weightKg = 72.0, string moleculeKey = "")
        {
            TimeTaken = timeTaken;
            DoseMg = doseMg;
            WeightKg = weightKg;
            MoleculeKey = moleculeKey;
        }

        public DoseEntry() { }
    }
}
