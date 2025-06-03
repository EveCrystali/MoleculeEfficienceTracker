using MoleculeEfficienceTracker.Core.Models;
using System.Text.Json;

namespace MoleculeEfficienceTracker.Core.Services
{
    public class DataPersistenceService
    {
        private readonly string _moleculeKey;
        private readonly string dataFilePath;
        private readonly JsonSerializerOptions jsonOptions;

        public DataPersistenceService(string moleculeKey)
        {
            _moleculeKey = moleculeKey.ToLowerInvariant(); // Assurer la cohérence de la casse
            dataFilePath = Path.Combine(FileSystem.AppDataDirectory, $"{_moleculeKey}_dose_data.json");
            jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task SaveDosesAsync(List<DoseEntry> doses)
        {
            try
            {
                var json = JsonSerializer.Serialize(doses, jsonOptions);
                await File.WriteAllTextAsync(dataFilePath, json);
            }
            catch (Exception ex)
            {
                // Log l'erreur ou la gérer selon tes besoins
                System.Diagnostics.Debug.WriteLine($"Erreur sauvegarde: {ex.Message}");
            }
        }

        public async Task<List<DoseEntry>> LoadDosesAsync()
        {
            try
            {
                if (!File.Exists(dataFilePath))
                {
                    return new List<DoseEntry>();
                }

                var json = await File.ReadAllTextAsync(dataFilePath);
                var doses = JsonSerializer.Deserialize<List<DoseEntry>>(json, jsonOptions);
                return doses ?? new List<DoseEntry>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur chargement: {ex.Message}");
                return new List<DoseEntry>();
            }
        }

        public async Task DeleteAllDataAsync()
        {
            try
            {
                if (File.Exists(dataFilePath))
                {
                    File.Delete(dataFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erreur suppression: {ex.Message}");
            }
        }

        public async Task<bool> HasDataAsync()
        {
            return File.Exists(dataFilePath) && (await LoadDosesAsync()).Any();
        }
    }
}
