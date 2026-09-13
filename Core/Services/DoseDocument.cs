using System.Text.Json;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>Ce qu'une importation a fait, en clair.</summary>
    public sealed class ImportReport
    {
        public int Added { get; set; }
        public int AlreadyPresent { get; set; }
        public int Unattributed { get; set; }
        public Dictionary<string, int> AddedByMolecule { get; } = new();

        public bool IsEmpty => Added == 0 && AlreadyPresent == 0 && Unattributed == 0;

        public override string ToString()
        {
            if (IsEmpty) return "Le fichier ne contenait aucune prise exploitable.";

            var lines = new List<string>();

            if (Added > 0)
            {
                lines.Add($"{Added} prise{(Added > 1 ? "s" : "")} ajoutée{(Added > 1 ? "s" : "")}.");

                foreach ((string key, int count) in AddedByMolecule.OrderByDescending(p => p.Value))
                    lines.Add($"  · {MoleculeKeys.DisplayName(key)} : {count}");
            }

            if (AlreadyPresent > 0)
                lines.Add($"{AlreadyPresent} déjà présente{(AlreadyPresent > 1 ? "s" : "")}, ignorée{(AlreadyPresent > 1 ? "s" : "")}.");

            if (Unattributed > 0)
                lines.Add($"{Unattributed} sans molécule identifiable, écartée{(Unattributed > 1 ? "s" : "")}.");

            return string.Join("\n", lines);
        }
    }

    /// <summary>
    /// Le document de prises : sa lecture et sa fusion, sans aucune dépendance à la
    /// plateforme.
    ///
    /// Ces deux opérations sont les seules que l'on ait vraiment envie de vérifier —
    /// qu'un fichier réimporté n'ajoute rien, qu'une clé historique soit ramenée à sa
    /// forme canonique, que le PascalCase des exports anciens soit relu aussi bien
    /// que le camelCase du stockage. Elles vivent donc à l'écart du sélecteur de
    /// fichiers et du système de fichiers, qui eux exigent un appareil.
    ///
    /// Le lecteur accepte les trois formes qui circulent :
    ///   · un tableau de prises, tel que l'exporte un écran molécule ;
    ///   · le document complet de la sauvegarde sortante, { molecules: { clé: [...] } } ;
    ///   · un objet qui associe directement une clé de molécule à un tableau.
    /// </summary>
    public static class DoseDocument
    {
        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Découpe un document en prises, molécule par molécule. Pure : aucune
        /// dépendance à la plateforme, donc testable hors appareil.
        /// </summary>
        /// <param name="fallbackKey">
        /// Molécule à supposer pour les prises qui n'en déclarent aucune. Elle est
        /// typiquement devinée depuis le nom du fichier.
        /// </param>
        public static Dictionary<string, List<DoseEntry>> Parse(string json, string fallbackKey = "")
        {
            var result = new Dictionary<string, List<DoseEntry>>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                Collect(root, fallbackKey, result);
                return result;
            }

            if (root.ValueKind != JsonValueKind.Object)
                return result;

            // Document de la sauvegarde sortante : les prises vivent sous
            // "molecules". Le reste du document est de l'horodatage.
            if (root.TryGetProperty("molecules", out JsonElement molecules) &&
                molecules.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in molecules.EnumerateObject())
                    if (property.Value.ValueKind == JsonValueKind.Array)
                        Collect(property.Value, MoleculeKeys.Normalize(property.Name), result);

                return result;
            }

            foreach (JsonProperty property in root.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.Array)
                    Collect(property.Value, MoleculeKeys.Normalize(property.Name), result);

            return result;
        }

        private static void Collect(JsonElement array, string fallbackKey, Dictionary<string, List<DoseEntry>> into)
        {
            List<DoseEntry>? entries = array.Deserialize<List<DoseEntry>>(ReadOptions);
            if (entries is null) return;

            foreach (DoseEntry entry in entries)
            {
                if (entry.DoseMg <= 0) continue;

                // MoleculeKey normalise à l'affectation ; il reste à combler le vide.
                if (string.IsNullOrWhiteSpace(entry.MoleculeKey))
                    entry.MoleculeKey = fallbackKey;

                string key = entry.MoleculeKey;
                if (string.IsNullOrWhiteSpace(key)) key = string.Empty;

                if (!into.TryGetValue(key, out List<DoseEntry>? list))
                    into[key] = list = new List<DoseEntry>();

                list.Add(entry);
            }
        }

        /// <summary>
        /// Fusionne des prises dans une liste existante, sans doublon.
        ///
        /// Deux filets : l'identifiant, et — pour les fichiers dont l'export a
        /// régénéré les identifiants — le triplet molécule, minute et quantité. Un
        /// même fichier réimporté deux fois n'ajoute donc rien.
        /// </summary>
        public static (List<DoseEntry> Merged, int Added, int Skipped) Merge(
            List<DoseEntry> existing, IEnumerable<DoseEntry> incoming)
        {
            var merged = new List<DoseEntry>(existing);
            var ids = new HashSet<string>(existing.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
            var signatures = new HashSet<string>(existing.Select(Signature), StringComparer.OrdinalIgnoreCase);

            int added = 0, skipped = 0;

            foreach (DoseEntry entry in incoming)
            {
                string signature = Signature(entry);

                if ((!string.IsNullOrEmpty(entry.Id) && ids.Contains(entry.Id)) || signatures.Contains(signature))
                {
                    skipped++;
                    continue;
                }

                ids.Add(entry.Id);
                signatures.Add(signature);
                merged.Add(entry);
                added++;
            }

            return (merged.OrderByDescending(d => d.TimeTaken).ToList(), added, skipped);
        }

        private static string Signature(DoseEntry entry)
            => $"{entry.MoleculeKey}|{entry.TimeTaken:yyyyMMddHHmm}|{entry.DoseMg:0.###}";

        /// <summary>
        /// Devine la molécule depuis un nom de fichier. Les exports anciens portent
        /// des noms translittérés — « caf_ine », « bromaz_pam » — dont les accents
        /// ont été remplacés par des soulignés : la comparaison se fait donc par
        /// fragment, pas par égalité.
        /// </summary>
        public static string GuessKeyFromFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;

            string name = new string(fileName.ToLowerInvariant().Where(char.IsLetter).ToArray());

            if (name.Contains("caf")) return MoleculeKeys.Caffeine;
            if (name.Contains("bromaz")) return MoleculeKeys.Bromazepam;
            if (name.Contains("alco")) return MoleculeKeys.Alcohol;
            if (name.Contains("paracet")) return MoleculeKeys.Paracetamol;
            if (name.Contains("ibupro")) return MoleculeKeys.Ibuprofen;
            if (name.Contains("douleur") || name.Contains("antalg")) return MoleculeKeys.PainRelief;

            return string.Empty;
        }
    }
}
