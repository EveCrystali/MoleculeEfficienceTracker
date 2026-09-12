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

        /// <summary>Au-delà de trois doses rapides, la rangée passe à la ligne.</summary>
        private const int PresetsPerRow = 3;

        public MoleculePanelView()
        {
            InitializeComponent();
            UpdateDetailToggleText();
            UpdateDataToggleText();
        }

        // ----- Contrôles exposés -----
        public Entry DoseInput => DoseEntryControl;
        public DatePicker DatePickerControl => DateControl;
        public TimePicker TimePickerControl => TimeControl;
        public Label ConcentrationOutput => ConcentrationLabel;
        public Label ConcentrationDetail => ConcentrationDetailLabel;
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
        public Button ImportDataButton => ImportButton;
        public Button ClearDataButton => ClearButton;
        public ContentView ExtraInputSlot => ExtraInputHost;

        // ----- Configuration -----

        public string SectionTitle
        {
            get => AddTitleLabel.Text;
            set => AddTitleLabel.Text = value;
        }

        /// <summary>
        /// Le libellé du champ de dose. Material 3 le pose dans le champ puis le
        /// fait flotter au-dessus du contour dès la frappe : il n'occupe plus une
        /// colonne entière à gauche.
        /// </summary>
        public string DoseFieldCaption
        {
            get => DoseEntryControl.Placeholder;
            set => DoseEntryControl.Placeholder = value;
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

        /// <summary>Le nombre de prises, rappelé dans le titre de l'historique.</summary>
        public void SetHistoryCount(int count)
            => HistoryTitleLabel.Text = count > 0 ? $"Prises récentes · {count}" : "Prises récentes";

        /// <summary>
        /// La puce d'état : fond teinté, texte du même rôle, glyphe compris.
        ///
        /// Le mot vient de la page, pas de la palette — l'alcool dit « ivresse
        /// légère » là où la caféine dit « effet léger », et l'écran alcool
        /// affichait jusqu'ici « effet net » pour une alcoolémie au-dessus de la
        /// limite légale.
        /// </summary>
        public void SetStatus(EffectLevel level, string text)
        {
            EffectStatusLabel.Text = $"{EffectPalette.Glyph(level)}  {text}";
            EffectStatusLabel.TextColor = EffectPalette.OnContainer(level);
            StatusChip.Background = new SolidColorBrush(EffectPalette.Container(level));
            StatusChip.IsVisible = true;

            SemanticProperties.SetDescription(StatusChip, text);
        }

        /// <summary>
        /// Masque la courbe et ses axes tant qu'il n'y a rien à tracer. La version
        /// précédente dessinait le cadre, la grille, les quatre seuils et le repère
        /// « Maintenant » au-dessus d'une série vide.
        /// </summary>
        public void ShowChart(bool visible, string? emptyMessage = null)
        {
            ChartHost.IsVisible = visible;
            ChartEmptyLabel.IsVisible = !visible;

            if (!visible && !string.IsNullOrWhiteSpace(emptyMessage))
                ChartEmptyLabel.Text = emptyMessage;
        }

        /// <summary>
        /// Installe les doses en accès direct. Un appui enregistre à l'heure
        /// courante : c'est ce qui ramène le coût d'une saisie à un geste.
        ///
        /// Les boutons se partagent la largeur sur une grille de trois colonnes et
        /// passent à la ligne au-delà. Ils portaient auparavant une largeur de
        /// 104 px en dur dans une pile horizontale sans repli : trois d'entre eux,
        /// leurs espaces et les marges réclamaient 396 px sur un écran de 360.
        /// </summary>
        public void SetPresets(IEnumerable<double> presets, string unit)
        {
            PresetsHost.Clear();
            PresetsHost.ColumnDefinitions.Clear();
            PresetsHost.RowDefinitions.Clear();

            var list = presets.ToList();
            PresetsHost.IsVisible = list.Count > 0;
            if (list.Count == 0) return;

            int columns = Math.Min(list.Count, PresetsPerRow);
            int rows = (list.Count + columns - 1) / columns;

            for (int c = 0; c < columns; c++)
                PresetsHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            for (int r = 0; r < rows; r++)
                PresetsHost.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (int i = 0; i < list.Count; i++)
            {
                double preset = list[i];

                var button = new Button { Text = $"{preset:0.##} {unit}" };

                if (Application.Current?.Resources.TryGetValue("PresetButtonStyle", out object? style) == true)
                    button.Style = (Style)style;

                double captured = preset;
                button.Clicked += (_, _) => PresetSelected?.Invoke(this, captured);
                SemanticProperties.SetDescription(button, $"Enregistrer {preset:0.##} {unit} maintenant");

                Grid.SetColumn(button, i % columns);
                Grid.SetRow(button, i / columns);
                PresetsHost.Add(button);
            }
        }

        /// <summary>
        /// Légende des seuils : trait et libellé, jamais la couleur seule. Elle
        /// porte désormais seule les noms des niveaux, puisque le graphique ne les
        /// écrit plus sur ses lignes.
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
                    X2 = 22,
                    Y2 = 6,
                    Stroke = new SolidColorBrush(EffectPalette.For(level)),
                    StrokeThickness = 2.5,
                    WidthRequest = 22,
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

        // ----- Volets repliables -----
        //
        // Un bouton, un conteneur, une bascule de visibilité. Le contrôle Expander
        // de la bibliothèque communautaire ferait la même chose, au prix d'une
        // mesure hasardeuse dans un ScrollView sur Android.

        private void OnDetailToggleClicked(object? sender, EventArgs e)
        {
            DetailForm.IsVisible = !DetailForm.IsVisible;
            UpdateDetailToggleText();
        }

        private void OnDataToggleClicked(object? sender, EventArgs e)
        {
            DataSection.IsVisible = !DataSection.IsVisible;
            UpdateDataToggleText();
        }

        private void UpdateDetailToggleText()
            => DetailToggleButton.Text = DetailForm.IsVisible
                ? "⌃  Masquer la saisie détaillée"
                : "⌄  Saisir une dose précise";

        private void UpdateDataToggleText()
            => DataToggleButton.Text = DataSection.IsVisible
                ? "⌃  Masquer les données"
                : "⌄  Données";

        /// <summary>Replie le formulaire détaillé, après un enregistrement.</summary>
        public void CollapseDetailForm()
        {
            if (!DetailForm.IsVisible) return;

            DetailForm.IsVisible = false;
            UpdateDetailToggleText();
        }

        private void OnDeleteRowClicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: string id })
                DoseDeleteRequested?.Invoke(this, id);
        }
    }
}
