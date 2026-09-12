using System.Globalization;
using Microsoft.Maui.Graphics;
using MoleculeEfficienceTracker.Core.Design;
using MoleculeEfficienceTracker.Core.Models;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker
{
    /// <summary>
    /// Synthèse d'usage, toutes molécules.
    ///
    /// La page lisait et désérialisait près de cent fois les mêmes fichiers à
    /// chaque affichage, dont une vingtaine pour alimenter des graphiques dont tout
    /// le code était commenté. Elle interrogeait en outre « alcool » là où la page
    /// Alcool écrivait « alcohol », si bien que cette ligne affichait zéro depuis
    /// l'origine.
    /// </summary>
    public partial class ChargePage : ContentPage
    {
        private readonly UsageStatsService _stats = new(MoleculeKeys.All);

        public ChargePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            try
            {
                _stats.Invalidate();

                Stats1dCollection.ItemsSource = await BuildRowsAsync(1);
                Stats7dCollection.ItemsSource = await BuildRowsAsync(7);
                Stats30dCollection.ItemsSource = await BuildRowsAsync(30);

                SummaryLabel.Text = $"Mis à jour à {DateTime.Now:HH\\hmm}. " +
                                    "La variation compare la période à la précédente de même durée.";
            }
            catch (Exception ex)
            {
                SummaryLabel.Text = $"Statistiques indisponibles : {ex.Message}";
            }
        }

        private async Task<List<StatRow>> BuildRowsAsync(int days)
        {
            var rows = new List<StatRow>();

            foreach (string key in MoleculeKeys.All)
            {
                List<DailyStats> history = await _stats.GetDailyStatsAsync(key, days * 2);
                if (history.Count == 0) continue;

                List<DailyStats> current = history.TakeLast(days).ToList();
                List<DailyStats> previous = history.Take(history.Count - days).ToList();

                double average = current.Count > 0 ? current.Average(d => d.TotalDose) : 0;
                double before = previous.Count > 0 ? previous.Average(d => d.TotalDose) : 0;
                int takes = current.Sum(d => d.Count);

                if (average <= 0 && takes == 0) continue;

                double? variation = before > 0 ? 100.0 * (average - before) / before : null;

                rows.Add(new StatRow(key, average, takes, days, variation));
            }

            return rows;
        }

        /// <summary>Une ligne de statistiques, prête à afficher.</summary>
        public sealed class StatRow
        {
            private readonly string _key;
            private readonly double _average;
            private readonly int _takes;
            private readonly int _days;
            private readonly double? _variation;

            public StatRow(string key, double average, int takes, int days, double? variation)
            {
                _key = key;
                _average = average;
                _takes = takes;
                _days = days;
                _variation = variation;
            }

            public string MoleculeName => MoleculeKeys.DisplayName(_key);

            public Color Accent => EffectPalette.ForMolecule(_key);

            public string AverageDisplay =>
                string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}/j", _average, MoleculeKeys.DoseUnit(_key));

            public string Detail => _takes switch
            {
                0 => "aucune prise",
                1 => "1 prise sur la période",
                _ => $"{_takes} prises · {_takes / (double)_days:0.#} par jour"
            };

            /// <summary>
            /// La variation porte toujours un glyphe en plus de sa couleur — c'était
            /// déjà le cas ici, et c'est désormais la règle partout. La palette
            /// renonce en revanche au vert : hausse en orange, baisse en bleu.
            /// </summary>
            public string VariationDisplay => _variation switch
            {
                null => "—",
                > 1 => $"▲ {_variation:0} %",
                < -1 => $"▼ {Math.Abs(_variation.Value):0} %",
                _ => "= stable"
            };

            public Color VariationColor => _variation switch
            {
                null => EffectPalette.Landmark,
                > 1 => EffectPalette.For(EffectLevel.Moderate),
                < -1 => EffectPalette.For(EffectLevel.Light),
                _ => EffectPalette.Landmark
            };
        }
    }
}
