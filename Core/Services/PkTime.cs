using System;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Arithmétique temporelle des calculs pharmacocinétiques.
    ///
    /// Le dépôt ne connaissait aucune notion d'UTC : les doses saisies au sélecteur
    /// portaient un Kind Unspecified, celles créées par DateTime.Now un Kind Local,
    /// et les deux cohabitaient dans le même fichier. Soustraire deux DateTime en
    /// heure murale ignore le changement d'heure — une heure d'erreur deux fois par
    /// an sur tous les écarts, donc sur toutes les concentrations.
    ///
    /// Tout écart de temps passe désormais par ici : les deux bornes sont ramenées
    /// à un instant absolu avant d'être soustraites.
    /// </summary>
    public static class PkTime
    {
        /// <summary>
        /// Qualifie un DateTime ambigu. Un Kind Unspecified — le cas des dates
        /// issues d'un DatePicker et des horodatages sans décalage lus dans les
        /// anciens fichiers — est interprété comme une heure locale.
        /// </summary>
        public static DateTime AsLocal(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value.ToLocalTime(),
            DateTimeKind.Local => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local)
        };

        /// <summary>Instant absolu correspondant à un DateTime éventuellement ambigu.</summary>
        public static DateTimeOffset ToOffset(DateTime value) => new DateTimeOffset(AsLocal(value));

        /// <summary>
        /// Heures écoulées entre une prise et un instant d'observation, corrigées
        /// du changement d'heure. C'est la seule façon autorisée de calculer un
        /// « temps depuis la prise » dans les calculateurs.
        /// </summary>
        public static double ElapsedHours(DateTime doseTime, DateTime currentTime)
            => (ToOffset(currentTime) - ToOffset(doseTime)).TotalHours;

        /// <summary>Maintenant, en heure locale explicitement qualifiée.</summary>
        public static DateTime Now => DateTime.Now;
    }
}
