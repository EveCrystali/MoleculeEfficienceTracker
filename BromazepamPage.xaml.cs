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
        }
    }
}
