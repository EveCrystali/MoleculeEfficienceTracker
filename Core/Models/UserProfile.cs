namespace MoleculeEfficienceTracker.Core.Models
{
    public enum Sex { Male, Female }

    /// <summary>
    /// Profil utilisé par les calculs, sans dépendance à la plateforme.
    ///
    /// Les calculateurs lisaient auparavant directement le stockage MAUI : la
    /// couche de calcul dépendait donc de l'appareil, et aucun test ne pouvait
    /// l'exercer. Elle lit désormais ces valeurs, que la couche de préférences
    /// alimente au démarrage et à chaque enregistrement.
    /// </summary>
    public static class UserProfile
    {
        public const double DefaultWeightKg = 72.0;

        public static double WeightKg { get; set; } = DefaultWeightKg;

        public static Sex Sex { get; set; } = Sex.Male;
    }
}
