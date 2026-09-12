using System;
using System.Text.Json.Serialization;

namespace MoleculeEfficienceTracker.Core.Models
{
    /// <summary>
    /// Une prise enregistrée.
    ///
    /// Note sur <see cref="DoseMg"/> : le nom est historique et le champ est
    /// conservé tel quel pour rester compatible avec les fichiers existants et les
    /// exports déjà réalisés. Il contient des milligrammes pour toutes les molécules
    /// sauf l'alcool, compté en unités standard. <see cref="Unit"/> lève l'ambiguïté.
    /// </summary>
    public class DoseEntry
    {
        private DateTime _timeTaken;
        private string _moleculeKey = string.Empty;

        /// <summary>
        /// Instant de la prise. Toujours ramené en heure locale explicite : les
        /// anciens fichiers mêlaient des horodatages avec et sans décalage horaire.
        /// </summary>
        public DateTime TimeTaken
        {
            get => _timeTaken;
            set => _timeTaken = Services.PkTime.AsLocal(value);
        }

        /// <summary>Quantité prise, exprimée dans <see cref="Unit"/>.</summary>
        public double DoseMg { get; set; }

        /// <summary>Poids au moment de la saisie. Figé volontairement : changer de
        /// poids ne doit pas réécrire l'historique des concentrations passées.</summary>
        public double WeightKg { get; set; } = 72.0;

        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Clé canonique de la molécule. Les variantes historiques
        /// ("alcool", "ibuprofene") sont normalisées à l'affectation.</summary>
        public string MoleculeKey
        {
            get => _moleculeKey;
            set => _moleculeKey = MoleculeKeys.Normalize(value);
        }

        /// <summary>
        /// Type de boisson, pour l'alcool seulement. Il conditionne la vitesse
        /// d'absorption et doit donc suivre la prise : il vivait auparavant sur
        /// l'instance du calculateur, si bien que le dernier type choisi
        /// s'appliquait rétroactivement à tout l'historique.
        /// </summary>
        public string? BeverageType { get; set; }

        /// <summary>Unité de <see cref="DoseMg"/>, déduite de la molécule.</summary>
        [JsonIgnore]
        public string Unit => MoleculeKeys.DoseUnit(_moleculeKey);

        /// <summary>Quantité formatée avec son unité, pour l'affichage.</summary>
        [JsonIgnore]
        public string AmountDisplay => Unit == DoseUnits.StandardUnit
            ? $"{DoseMg:0.##} {Unit}"
            : $"{DoseMg:0.#} {Unit}";

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
