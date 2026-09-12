using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MoleculeEfficienceTracker.Core.Models;

namespace MoleculeEfficienceTracker.Core.Services
{
    /// <summary>
    /// Sauvegarde sortante, quotidienne et unidirectionnelle.
    ///
    /// C'est la seule part du backend qui valait son prix. Un vrai service
    /// synchronisé n'aurait rien retiré au travail : l'application devant
    /// fonctionner hors du réseau, la couche locale resterait à écrire, et la
    /// synchronisation puis la résolution de conflits s'y seraient ajoutées. Ici,
    /// rien ne revient du serveur : il n'y a donc aucun conflit à arbitrer, aucune
    /// authentification à inventer, et aucun mode dégradé — un échec est silencieux
    /// et sans conséquence.
    ///
    /// Le point de dépôt est typiquement une adresse du tailnet, joignable sans
    /// exposition publique.
    /// </summary>
    public class OutboundBackupService
    {
        private const string EndpointKey = "backup_endpoint";
        private const string LastRunKey = "backup_last_run";
        private static readonly TimeSpan MinimumInterval = TimeSpan.FromHours(20);

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

        public static string GetEndpoint() => Preferences.Get(EndpointKey, string.Empty);

        public static void SetEndpoint(string url) => Preferences.Set(EndpointKey, url?.Trim() ?? string.Empty);

        public static DateTime? GetLastRun()
        {
            long ticks = Preferences.Get(LastRunKey, 0L);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Local);
        }

        public static bool IsConfigured => Uri.TryCreate(GetEndpoint(), UriKind.Absolute, out _);

        /// <summary>
        /// Envoie si l'adresse est configurée et que la dernière tentative réussie
        /// remonte à plus de vingt heures.
        /// </summary>
        public async Task<bool> RunIfDueAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConfigured) return false;

            DateTime? last = GetLastRun();
            if (last is not null && DateTime.Now - last.Value < MinimumInterval)
                return false;

            return await SendAsync(cancellationToken);
        }

        /// <summary>Envoie immédiatement, sans tenir compte de l'intervalle.</summary>
        public async Task<bool> SendAsync(CancellationToken cancellationToken = default)
        {
            string endpoint = GetEndpoint();
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
                return false;

            try
            {
                string payload = await BuildPayloadAsync();

                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                content.Headers.Add("X-Backup-Filename",
                    $"molecule-tracker-{DateTime.Now:yyyyMMdd-HHmm}.json");

                using HttpResponseMessage response = await Http.PostAsync(uri, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[Sauvegarde] refus du serveur : {(int)response.StatusCode}");
                    return false;
                }

                Preferences.Set(LastRunKey, DateTime.Now.Ticks);
                return true;
            }
            catch (Exception ex)
            {
                // Best-effort : hors du tailnet, hors ligne, serveur éteint — rien
                // de tout cela ne doit peser sur l'usage de l'application.
                Debug.WriteLine($"[Sauvegarde] envoi impossible : {ex.Message}");
                return false;
            }
        }

        /// <summary>Toutes les molécules dans un seul document, daté.</summary>
        private static async Task<string> BuildPayloadAsync()
        {
            var payload = new Dictionary<string, object>
            {
                ["exportedAt"] = DateTimeOffset.Now.ToString("O"),
                ["schemaVersion"] = DataMigrationService.CurrentSchemaVersion
            };

            var molecules = new Dictionary<string, List<DoseEntry>>();

            foreach (string key in MoleculeKeys.All)
            {
                try
                {
                    molecules[key] = await new DataPersistenceService(key).LoadDosesAsync();
                }
                catch (DoseDataCorruptedException)
                {
                    // Un fichier illisible ne doit pas empêcher de sauvegarder les autres.
                    molecules[key] = new List<DoseEntry>();
                }
            }

            payload["molecules"] = molecules;

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }
}
