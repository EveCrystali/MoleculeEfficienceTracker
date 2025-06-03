using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Syncfusion.Maui.Charts;
using Microsoft.Maui.Graphics;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using System.Text;
using System.IO;

namespace MoleculeEfficienceTracker
{
    public partial class BromazepamPage : BaseMoleculePage<BromazepamCalculator>
    {
        protected override Entry DoseInputControl => DoseEntry;
        protected override DatePicker DatePickerControl => DatePicker;
        protected override TimePicker TimePickerControl => TimePicker;
        protected override Label ConcentrationOutputLabel => ConcentrationLabel;
        protected override Label LastUpdateOutputLabel => LastUpdateLabel;
        protected override SfCartesianChart ChartControl => ConcentrationChart;
        protected override CollectionView DosesDisplayCollection => DosesCollection;
        protected override Label EmptyStateIndicatorLabel => EmptyDosesLabel;
        

        protected override string DoseAnnotationIcon => "💊";
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-7);
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(3);
        protected override int GraphDataNumberOfPoints => 10 * 24 * 2;
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-12); // Ajustez si nécessaire
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(24);  // Ajustez si nécessaire


        public BromazepamPage() : base("bromazepam")
        {
            InitializeComponent();
            base.InitializePageUI();
        }

        protected override async Task OnBeforeLoadDataAsync()
        {
            await base.OnBeforeLoadDataAsync(); // Appel à l'implémentation de base (facultatif ici car vide)

            // --- Début de la logique de migration ---
            string oldDataFilePath = Path.Combine(FileSystem.AppDataDirectory, "dose_data.json");
            if (!await PersistenceService.HasDataAsync() && File.Exists(oldDataFilePath))
            {
                try
                {
                    var oldJson = await File.ReadAllTextAsync(oldDataFilePath);
                    var oldDoses = JsonSerializer.Deserialize<List<DoseEntry>>(oldJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); // Soyez flexible avec la casse pour l'ancien format

                    if (oldDoses != null && oldDoses.Any())
                    {
                        await PersistenceService.SaveDosesAsync(oldDoses);
                        string migratedOldFilePath = Path.Combine(FileSystem.AppDataDirectory, $"dose_data_migrated_to_{MoleculeKey}.json");
                        File.Move(oldDataFilePath, migratedOldFilePath);
                        await AlertService.ShowAlertAsync("Migration Réussie", $"Vos anciennes données de {Calculator.DisplayName} ont été importées.", "OK");
                    }
                    else if (oldDoses == null || !oldDoses.Any())
                    {
                        File.Delete(oldDataFilePath);
                    }
                }
                catch (Exception ex)
                {
                    await AlertService.ShowAlertAsync("Erreur de Migration", $"Impossible de migrer: {ex.Message}", "OK");
                }
            }
            // --- Fin de la logique de migration ---
        }
    }
}
