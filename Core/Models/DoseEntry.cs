using System;

namespace MoleculeEfficienceTracker.Core.Models
{
    public class DoseEntry
    {
        public DateTime TimeTaken { get; set; }
        public double DoseMg { get; set; }
        public double WeightKg { get; set; } = 72.0; // Poids par défaut si non renseigné
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Ajout pour MAUI

        public DoseEntry(DateTime timeTaken, double doseMg, double weightKg = 72.0)
        {
            TimeTaken = timeTaken;
            DoseMg = doseMg;
            WeightKg = weightKg;
        }

        public DoseEntry() { }
    }
}
