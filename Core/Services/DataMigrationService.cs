using MoleculeEfficienceTracker.Core.Models;
using System.Diagnostics;
using System.Text.Json;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Migration unique et versionnée des fichiers de données.
    ///
    /// Elle répare ce que quinze mois d'usage ont laissé : des clés de molécule
    /// vides, deux orthographes pour l'ibuprofène, deux pour l'alcool, des
    /// horodatages tantôt datés tantôt nus, et des flottants à seize décimales.
    /// Elle sauvegarde avant d'écrire, et ne s'exécute qu'une fois.
    /// </summary>
    public static class DataMigrationService
    {
        public const int CurrentSchemaVersion = 2;
        private const string VersionKey = "data_schema_version";

        private static Task? _running;
        private static readonly object _lock = new();

        /// <summary>Résumé de ce que la migration a fait, pour la page Paramètres.</summary>
        public static MigrationReport? LastReport { get; private set; }

        /// <summary>
        /// Fichiers écrits sous une orthographe abandonnée, à reverser dans le
        /// fichier canonique. Sans cela, le passage à la clé canonique ferait
        /// disparaître l'historique d'ibuprofène de l'écran.
        /// </summary>
        private static readonly Dictionary<string, string[]> LegacyFiles = new()
        {
            [MoleculeKeys.Alcohol] = new[] { "alcool" },
            [MoleculeKeys.Ibuprofen] = new[] { "ibuprofene" },
            [MoleculeKeys.Caffeine] = new[] { "cafeine" },
        };

        /// <summary>Idempotent : la migration ne tourne qu'une fois par processus.</summary>
        public static Task EnsureMigratedAsync()
        {
            lock (_lock)
            {
                _running ??= RunAsync();
                return _running;
            }
        }

        private static async Task RunAsync()
        {
            try
            {
                int version = Preferences.Get(VersionKey, 0);
                if (version >= CurrentSchemaVersion)
                    return;

                var report = new MigrationReport();

                // L'agrégat anti-douleur est d'abord réparti vers les deux fichiers
                // molécule, avant que ceux-ci ne soient normalisés.
                await RedistributePainReliefAsync(report);

                foreach (string key in MoleculeKeys.All)
                {
                    await MigrateMoleculeAsync(key, report);
                }

                Preferences.Set(VersionKey, CurrentSchemaVersion);
                LastReport = report;
                Debug.WriteLine($"[Migration] {report}");
            }
            catch (Exception ex)
            {
                // Une migration qui échoue ne doit pas empêcher l'application de
                // démarrer : les fichiers d'origine sont intacts, la version n'est
                // pas avancée, la migration sera retentée au prochain lancement.
                Debug.WriteLine($"[Migration] échec : {ex}");
            }
        }

        /// <summary>
        /// Reverse le fichier agrégé de l'anti-douleur dans les fichiers
        /// paracétamol et ibuprofène.
        ///
        /// Chaque prise y était stockée deux fois — dans l'agrégat et dans le
        /// fichier de sa molécule — et une fusion était rejouée à chaque affichage
        /// de l'onglet. Une seule copie subsiste désormais, celle de la molécule.
        /// Les entrées dont la molécule est indéterminable ne sont pas devinées :
        /// le fichier d'origine est conservé sous un nom explicite.
        /// </summary>
        private static async Task RedistributePainReliefAsync(MigrationReport report)
        {
            string aggregatePath = Path.Combine(FileSystem.AppDataDirectory, $"{MoleculeKeys.PainRelief}_dose_data.json");
            if (!File.Exists(aggregatePath))
                return;

            List<DoseEntry> aggregate;
            try
            {
                string json = await File.ReadAllTextAsync(aggregatePath);
                aggregate = JsonSerializer.Deserialize<List<DoseEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                }) ?? new List<DoseEntry>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Migration] agrégat anti-douleur illisible : {ex.Message}");
                return;
            }

            foreach (string target in new[] { MoleculeKeys.Paracetamol, MoleculeKeys.Ibuprofen })
            {
                List<DoseEntry> incoming = aggregate.Where(d => d.MoleculeKey == target).ToList();
                if (incoming.Count == 0) continue;

                var store = new DataPersistenceService(target);
                List<DoseEntry> existing;
                try { existing = await store.LoadDosesAsync(); }
                catch (DoseDataCorruptedException) { continue; }

                var known = new HashSet<string>(existing.Select(d => d.Id));
                List<DoseEntry> added = incoming.Where(d => !known.Contains(d.Id)).ToList();

                if (added.Count == 0) continue;

                existing.AddRange(added);
                await store.SaveDosesAsync(existing.OrderByDescending(d => d.TimeTaken).ToList());
                report.MergedLegacyFiles++;
            }

            int orphans = aggregate.Count(d => d.MoleculeKey != MoleculeKeys.Paracetamol &&
                                               d.MoleculeKey != MoleculeKeys.Ibuprofen);
            report.UnattributedPainRelief = orphans;

            File.Move(aggregatePath, aggregatePath + ".migre", overwrite: true);
        }

        private static async Task MigrateMoleculeAsync(string canonicalKey, MigrationReport report)
        {
            var service = new DataPersistenceService(canonicalKey);
            var collected = new List<DoseEntry>();

            // 1. Le fichier canonique, s'il existe.
            if (service.HasData())
            {
                await service.BackupAsync("pre-migration-v2");
                try
                {
                    collected.AddRange(await service.LoadDosesAsync());
                }
                catch (DoseDataCorruptedException ex)
                {
                    report.CorruptedFiles.Add(ex.QuarantinePath);
                    return; // on ne touche pas à un fichier qu'on ne sait pas lire
                }
            }

            // 2. Les fichiers écrits sous une orthographe abandonnée.
            if (LegacyFiles.TryGetValue(canonicalKey, out string[]? legacyKeys))
            {
                foreach (string legacy in legacyKeys)
                {
                    string legacyPath = Path.Combine(FileSystem.AppDataDirectory, $"{legacy}_dose_data.json");
                    if (!File.Exists(legacyPath))
                        continue;

                    try
                    {
                        string json = await File.ReadAllTextAsync(legacyPath);
                        var legacyDoses = JsonSerializer.Deserialize<List<DoseEntry>>(json, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            PropertyNameCaseInsensitive = true
                        });

                        if (legacyDoses is { Count: > 0 })
                        {
                            collected.AddRange(legacyDoses);
                            report.MergedLegacyFiles++;
                        }

                        File.Move(legacyPath, legacyPath + ".migre", overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Migration] fichier hérité {legacy} ignoré : {ex.Message}");
                    }
                }
            }

            if (collected.Count == 0)
                return;

            var cleaned = Normalize(collected, canonicalKey, report);

            if (report.Changed)
                await service.SaveDosesAsync(cleaned);
        }

        /// <summary>
        /// Normalisation pure — testable sans système de fichiers.
        /// Les clés et les fuseaux sont déjà réparés par les accesseurs de
        /// <see cref="DoseEntry"/> ; restent la clé absente, l'arrondi et les
        /// double-saisies.
        /// </summary>
        public static List<DoseEntry> Normalize(IEnumerable<DoseEntry> doses, string fallbackKey, MigrationReport report)
        {
            var result = new List<DoseEntry>();
            var seen = new HashSet<(string, long, double)>();

            foreach (var dose in doses)
            {
                if (string.IsNullOrWhiteSpace(dose.MoleculeKey))
                {
                    dose.MoleculeKey = fallbackKey;
                    report.KeysFilled++;
                }

                double rounded = Math.Round(dose.DoseMg, 4);
                if (Math.Abs(rounded - dose.DoseMg) > double.Epsilon)
                {
                    dose.DoseMg = rounded;
                    report.RoundedAmounts++;
                }

                var signature = (dose.MoleculeKey, dose.TimeTaken.Ticks, dose.DoseMg);
                if (!seen.Add(signature))
                {
                    report.DuplicatesDropped++;
                    continue;
                }

                result.Add(dose);
            }

            report.TotalProcessed += result.Count;
            return result.OrderByDescending(d => d.TimeTaken).ToList();
        }
    }

    public class MigrationReport
    {
        public int TotalProcessed { get; set; }
        public int KeysFilled { get; set; }
        public int RoundedAmounts { get; set; }
        public int DuplicatesDropped { get; set; }
        public int MergedLegacyFiles { get; set; }
        public int UnattributedPainRelief { get; set; }
        public List<string> CorruptedFiles { get; } = new();

        public bool Changed => KeysFilled > 0 || RoundedAmounts > 0 || DuplicatesDropped > 0 || MergedLegacyFiles > 0;

        public override string ToString() =>
            $"{TotalProcessed} prises, {KeysFilled} clés reconstruites, {RoundedAmounts} arrondis, " +
            $"{DuplicatesDropped} doublons écartés, {MergedLegacyFiles} fichiers hérités fusionnés, " +
            $"{CorruptedFiles.Count} fichiers illisibles";
    }
}
