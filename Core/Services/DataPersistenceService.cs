using MoleculeEfficienceTracker.Core.Models;
using System.Text.Json;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Levée quand le fichier de données existe mais ne peut pas être relu.
    /// Elle doit remonter jusqu'à l'appelant : l'ancienne version attrapait
    /// l'exception et rendait une liste vide, que la couche supérieure
    /// réécrivait ensuite par-dessus le fichier abîmé. Un JSON tronqué ne se
    /// signalait pas — il s'effaçait.
    /// </summary>
    public class DoseDataCorruptedException : Exception
    {
        public string QuarantinePath { get; }

        public DoseDataCorruptedException(string quarantinePath, Exception inner)
            : base($"Fichier de données illisible. Copie mise de côté : {quarantinePath}", inner)
            => QuarantinePath = quarantinePath;
    }

    public class DataPersistenceService
    {
        private readonly string _moleculeKey;
        private readonly string dataFilePath;
        private readonly JsonSerializerOptions jsonOptions;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public string FilePath => dataFilePath;
        public string MoleculeKey => _moleculeKey;

        public DataPersistenceService(string moleculeKey)
        {
            _moleculeKey = MoleculeKeys.Normalize(moleculeKey).ToLowerInvariant();
            dataFilePath = Path.Combine(FileSystem.AppDataDirectory, $"{_moleculeKey}_dose_data.json");
            jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                // Les exports historiques sont en PascalCase, le stockage en camelCase.
                // Sans ceci, un fichier exporté n'était pas relisible par l'application.
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Écriture atomique : le contenu part dans un fichier temporaire, qui ne
        /// remplace l'original qu'une fois entièrement écrit et vidé sur le disque.
        /// Une coupure en cours de route laisse l'ancien fichier intact.
        /// </summary>
        public async Task SaveDosesAsync(List<DoseEntry> doses)
        {
            await _gate.WaitAsync();
            try
            {
                string json = JsonSerializer.Serialize(doses, jsonOptions);
                string tempPath = dataFilePath + ".tmp";

                await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(json);
                    await writer.FlushAsync();
                    stream.Flush(true);
                }

                File.Move(tempPath, dataFilePath, overwrite: true);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Relit le fichier. Absent : liste vide. Illisible : le fichier est mis de
        /// côté sous un nom horodaté et l'exception remonte — jamais de liste vide
        /// silencieuse, qui condamnerait l'historique à l'écrasement suivant.
        /// </summary>
        public async Task<List<DoseEntry>> LoadDosesAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (!File.Exists(dataFilePath))
                    return new List<DoseEntry>();

                string json = await File.ReadAllTextAsync(dataFilePath);

                if (string.IsNullOrWhiteSpace(json))
                    return new List<DoseEntry>();

                try
                {
                    var doses = JsonSerializer.Deserialize<List<DoseEntry>>(json, jsonOptions);
                    return doses ?? new List<DoseEntry>();
                }
                catch (JsonException ex)
                {
                    string quarantine = $"{dataFilePath}.corrompu-{DateTime.Now:yyyyMMdd-HHmmss}";
                    try { File.Copy(dataFilePath, quarantine, overwrite: true); }
                    catch { /* la mise de côté est un bonus, pas une condition */ }
                    throw new DoseDataCorruptedException(quarantine, ex);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Copie horodatée du fichier courant, avant toute migration.</summary>
        public async Task<string?> BackupAsync(string suffix)
        {
            await _gate.WaitAsync();
            try
            {
                if (!File.Exists(dataFilePath))
                    return null;

                string backupPath = $"{dataFilePath}.{suffix}-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
                File.Copy(dataFilePath, backupPath, overwrite: true);
                return backupPath;
            }
            catch
            {
                return null;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task DeleteAllDataAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (File.Exists(dataFilePath))
                    File.Delete(dataFilePath);
            }
            finally
            {
                _gate.Release();
            }
        }

        public bool HasData() => File.Exists(dataFilePath) && new FileInfo(dataFilePath).Length > 2;
    }
}
