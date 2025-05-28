using System;

namespace MoleculeEfficienceTracker.Core.Models
{
    public class DoseEntry
    {
        public DateTime TimeTaken { get; set; }
        public double DoseMg { get; set; }
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Ajout pour MAUI

        public DoseEntry(DateTime timeTaken, double doseMg)
        {
            TimeTaken = timeTaken;
            DoseMg = doseMg;
        }

        public DoseEntry() { }
    }
}
