using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Importation d'un fichier de prises.
    ///
    /// L'application savait exporter depuis l'origine, jamais relire. Un fichier
    /// exporté n'avait donc nulle part où revenir — et un changement d'identifiant
    /// de paquet, qui donne à Android un nouveau répertoire de données, laisse
    /// l'historique intact mais hors de portée.
    ///
    /// La lecture et la fusion elles-mêmes vivent dans <see cref="DoseDocument"/>,
    /// qui ne dépend d'aucune API de plateforme. Ici ne restent que le sélecteur de
    /// fichiers et l'écriture.
    /// </summary>
    public class DataImportService
    {
        /// <summary>
        /// Demande un fichier, le lit, et range chaque prise dans le fichier de sa
        /// molécule. Retourne null si l'utilisateur annule.
        /// </summary>
        public async Task<ImportReport?> PickAndImportAsync()
        {
            // Aucun filtre de type : sur Android, un fournisseur de fichiers annonce
            // volontiers un JSON en « application/octet-stream », et un filtre trop
            // strict grise alors le fichier que l'on vient chercher.
            FileResult? picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choisir un fichier de prises (.json)"
            });

            if (picked is null) return null;

            using Stream stream = await picked.OpenReadAsync();
            using var reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();

            return await ImportAsync(json, DoseDocument.GuessKeyFromFileName(picked.FileName));
        }

        /// <summary>Importe un document déjà lu.</summary>
        public async Task<ImportReport> ImportAsync(string json, string fallbackKey = "")
        {
            var report = new ImportReport();
            Dictionary<string, List<DoseEntry>> parsed = DoseDocument.Parse(json, fallbackKey);

            foreach ((string key, List<DoseEntry> entries) in parsed)
            {
                // « pain_relief » était l'agrégat de deux molécules : sans clé par
                // prise, rien ne dit laquelle. On le signale plutôt que de trancher
                // au hasard.
                if (string.IsNullOrWhiteSpace(key) || key == MoleculeKeys.PainRelief ||
                    !MoleculeKeys.All.Contains(key))
                {
                    report.Unattributed += entries.Count;
                    continue;
                }

                var store = new DataPersistenceService(key);

                List<DoseEntry> existing;
                try
                {
                    existing = await store.LoadDosesAsync();
                }
                catch (DoseDataCorruptedException)
                {
                    // Le fichier en place est illisible : surtout ne pas écrire
                    // par-dessus, ce serait achever ce que l'import vient sauver.
                    report.Unattributed += entries.Count;
                    continue;
                }

                (List<DoseEntry> merged, int added, int skipped) = DoseDocument.Merge(existing, entries);

                if (added > 0)
                {
                    await store.SaveDosesAsync(merged);
                    report.AddedByMolecule[key] = added;
                }

                report.Added += added;
                report.AlreadyPresent += skipped;
            }

            return report;
        }
    }
}
