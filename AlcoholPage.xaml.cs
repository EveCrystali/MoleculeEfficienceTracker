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
    public partial class AlcoholPage : BaseMoleculePage<AlcoholCalculator>
    {
        // Implémentation des propriétés abstraites pour les contrôles UI
        protected override Entry DoseInputControl => DoseEntry;
        protected override DatePicker DatePickerControl => DatePicker;
        protected override TimePicker TimePickerControl => TimePicker;
        protected override Label ConcentrationOutputLabel => ConcentrationLabel;
        protected override Label LastUpdateOutputLabel => LastUpdateLabel;
        protected override SfCartesianChart ChartControl => ConcentrationChart;
        protected override Label EmptyStateIndicatorLabel => EmptyDosesLabel;
        protected override CollectionView DosesDisplayCollection => DosesCollection;

        // Implémentation des propriétés abstraites spécifiques à la molécule
        protected override string DoseAnnotationIcon => "🍾"; // Icône pour l'alcool
        protected override TimeSpan GraphDataStartOffset => TimeSpan.FromDays(-3); // Par exemple, afficher 3 jours avant
        protected override TimeSpan GraphDataEndOffset => TimeSpan.FromDays(1);     // Et 1 jour après
        protected override int GraphDataNumberOfPoints => 4 * 24 * 4; // 4 jours au total, 4 points par heure
        protected override TimeSpan InitialVisibleStartOffset => TimeSpan.FromHours(-12); // Vue initiale de -12h
        protected override TimeSpan InitialVisibleEndOffset => TimeSpan.FromHours(12);   // Vue initiale de +12h

        public AlcoholPage() : base("alcohol")
        {
            InitializeComponent();
            base.InitializePageUI();
        }
    }
}
