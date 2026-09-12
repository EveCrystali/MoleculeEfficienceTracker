namespace MoleculeEfficienceTracker.Core.Models
{
    /// <summary>
    /// Clés canoniques des molécules.
    ///
    /// Historique : les clés ont dérivé au fil du temps — la page Alcool persistait
    /// sous "alcohol" pendant que ChargePage et ResidualLoadService lisaient "alcool",
    /// si bien que la ligne Alcool des statistiques affichait zéro depuis l'origine.
    /// Même flottement entre "ibuprofen" et "ibuprofene". Cette classe est désormais
    /// l'unique source de vérité : aucun littéral de clé ne doit subsister ailleurs.
    /// </summary>
    public static class MoleculeKeys
    {
        public const string Caffeine = "caffeine";
        public const string Bromazepam = "bromazepam";
        public const string Paracetamol = "paracetamol";
        public const string Ibuprofen = "ibuprofen";
        public const string Alcohol = "alcohol";

        /// <summary>Clé du fichier agrégé de la page anti-douleur.</summary>
        public const string PainRelief = "pain_relief";

        public static readonly string[] All =
        {
            Caffeine, Bromazepam, Paracetamol, Ibuprofen, Alcohol
        };

        /// <summary>
        /// Ramène une clé écrite sous n'importe quelle variante historique vers sa
        /// forme canonique. Retourne <paramref name="fallback"/> si la clé est vide.
        /// </summary>
        public static string Normalize(string? key, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback;

            return key.Trim().ToLowerInvariant() switch
            {
                "caffeine" or "cafeine" or "caféine" => Caffeine,
                "bromazepam" or "bromazépam" => Bromazepam,
                "paracetamol" or "paracétamol" or "acetaminophen" => Paracetamol,
                "ibuprofen" or "ibuprofene" or "ibuprofène" => Ibuprofen,
                "alcohol" or "alcool" => Alcohol,
                "pain_relief" or "painrelief" or "antidouleur" => PainRelief,
                var other => other
            };
        }

        /// <summary>Libellé français affichable.</summary>
        public static string DisplayName(string? key) => Normalize(key) switch
        {
            Caffeine => "Caféine",
            Bromazepam => "Bromazépam",
            Paracetamol => "Paracétamol",
            Ibuprofen => "Ibuprofène",
            Alcohol => "Alcool",
            PainRelief => "Anti-douleur",
            _ => key ?? string.Empty
        };

        /// <summary>
        /// Unité dans laquelle la dose est saisie et stockée.
        /// L'alcool est compté en unités standard (1 u = 10 g d'éthanol), pas en
        /// milligrammes — le champ historique DoseMg mentait sur son contenu.
        /// </summary>
        public static string DoseUnit(string? key) => Normalize(key) switch
        {
            Alcohol => DoseUnits.StandardUnit,
            _ => DoseUnits.Milligram
        };
    }

    public static class DoseUnits
    {
        public const string Milligram = "mg";
        public const string StandardUnit = "u";
    }
}
