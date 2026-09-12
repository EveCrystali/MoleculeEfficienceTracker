using Microsoft.Maui.Controls.Shapes;
using MoleculeEfficienceTracker.Core.Design;
using MoleculeEfficienceTracker.Core.Services;
using Syncfusion.Maui.Charts;

namespace MoleculeEfficienceTracker.Controls
{
    /// <summary>
    /// Corps partagé des pages molécules. Il expose ses contrôles plutôt que de
    /// multiplier les propriétés liables : la page les câble en C#, ce qui garde
    /// une seule définition du XAML sans rendre la configuration indirecte.
    /// </summary>
    public partial class MoleculePanelView : ContentView
    {
        /// <summary>Levé quand l'utilisateur demande la suppression d'une prise.</summary>
        public event EventHandler<string>? DoseDeleteRequested;

        /// <summary>Levé quand l'utilisateur appuie sur une dose en accès direct.</summary>
        public event EventHandler<double>? PresetSelected;

        public MoleculePanelView()
        {
            InitializeComponent();
        }

        // ----- Contrôles exposés -----
        public Entry DoseInput => DoseEntryControl;
        public DatePicker DatePickerControl => DateControl;
        public TimePicker TimePickerControl => TimeControl;
        public Label ConcentrationOutput => ConcentrationLabel;
        public Label LastUpdateOutput => LastUpdateLabel;
        public Label EffectStatus => EffectStatusLabel;
        public Label EffectPrediction => EffectPredictionLabel;
        public Label Headline => HeadlineLabel;
        public Label HeadlineDetail => HeadlineDetailLabel;
        public SfCartesianChart Chart => ConcentrationChart;
        public SplineSeries Series => ConcentrationSeries;
        public NumericalAxis YAxis => ChartYAxis;
        public DateTimeAxis XAxis => ChartXAxis;
        public Label EmptyIndicator => EmptyDosesLabel;
        public CollectionView DosesView => DosesCollection;
        public Button AddDoseButton => AddButton;
        public Button ExportDataButton => ExportButton;
        public Button ClearDataButton => ClearButton;
        public ContentView ExtraInputSlot => ExtraInputHost;

        // ----- Configuration -----

        public string SectionTitle
        {
            get => AddTitleLabel.Text;
            set => AddTitleLabel.Text = value;
        }

        public string DoseFieldCaption
        {
            get => DoseFieldLabel.Text;
            set => DoseFieldLabel.Text = value;
        }

        public string HelperText
        {
            get => HelperLabel.Text;
            set
            {
                HelperLabel.Text = value;
                HelperLabel.IsVisible = !string.IsNullOrWhiteSpace(value);
            }
        }

        /// <summary>La phrase en tête d'écran. Vide, la carte se replie sur la valeur.</summary>
        public string HeadlineText
        {
            get => HeadlineLabel.Text;
            set
            {
                HeadlineLabel.Text = value;
                HeadlineLabel.IsVisible = !string.IsNullOrWhiteSpace(value);
            }
        }

        public string HeadlineDetailText
        {
            get => HeadlineDetailLabel.Text;
            set
            {
                HeadlineDetailLabel.Text = value;
                HeadlineDetailLabel.IsVisible = !string.IsNullOrWhiteSpace(value);
            }
        }

        /// <summary>
        /// Installe les doses en accès direct. Un appui enregistre à l'heure
        /// courante : c'est ce qui ramène le coût d'une saisie à un geste.
        /// </summary>
        public void SetPresets(IEnumerable<double> presets, string unit)
        {
            PresetsHost.Clear();

            var list = presets.ToList();
            PresetsSection.IsVisible = list.Count > 0;
            ManualEntryHint.IsVisible = list.Count > 0;

            foreach (double preset in list)
            {
                var button = new Button
                {
                    Text = $"+{preset:0.##} {unit}",
                    WidthRequest = 104
                };

                if (Application.Current?.Resources.TryGetValue("PresetButtonStyle", out object? style) == true)
                    button.Style = (Style)style;

                double captured = preset;
                button.Clicked += (_, _) => PresetSelected?.Invoke(this, captured);
                SemanticProperties.SetDescription(button, $"Enregistrer {preset:0.##} {unit} maintenant");

                PresetsHost.Add(button);
            }
        }

        /// <summary>
        /// Légende des seuils : trait et libellé, jamais la couleur seule. C'était
        /// la lacune des graphiques précédents, qui n'avaient aucune légende.
        /// </summary>
        public void SetThresholdLegend(IEnumerable<(string Label, EffectLevel Level)> entries)
        {
            ThresholdLegend.Clear();

            foreach ((string label, EffectLevel level) in entries)
            {
                var swatch = new Line
                {
                    X1 = 0,
                    Y1 = 6,
                    X2 = 26,
                    Y2 = 6,
                    Stroke = new SolidColorBrush(EffectPalette.For(level)),
                    StrokeThickness = 2.5,
                    WidthRequest = 26,
                    HeightRequest = 12,
                    VerticalOptions = LayoutOptions.Center
                };

                double[] dash = EffectPalette.DashPattern(level);
                if (dash.Length > 0)
                {
                    var pattern = new DoubleCollection();
                    foreach (double value in dash) pattern.Add(value);
                    swatch.StrokeDashArray = pattern;
                }

                var row = new HorizontalStackLayout
                {
                    Spacing = 6,
                    Margin = new Thickness(0, 2, 14, 2),
                    Children =
                    {
                        swatch,
                        new Label
                        {
                            Text = label,
                            FontSize = 12,
                            VerticalTextAlignment = TextAlignment.Center,
                            TextColor = EffectPalette.For(level)
                        }
                    }
                };

                ThresholdLegend.Add(row);
            }
        }

        private void OnDeleteRowClicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: string id })
                DoseDeleteRequested?.Invoke(this, id);
        }
    }
}
