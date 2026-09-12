using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker.Core.Design
{
    /// <summary>
    /// Palette des niveaux d'effet.
    ///
    /// Deux règles la gouvernent, et elles corrigent le même défaut.
    ///
    /// D'abord, aucun vert porteur de sens : la lecture rouge/vert est inaccessible
    /// en deutéranopie. L'échelle monte du gris au bleu, puis à l'orange, puis au
    /// rouge — un axe que l'œil distingue quelle que soit la vision des couleurs.
    ///
    /// Ensuite, la couleur ne porte jamais seule. Chaque niveau a son motif de
    /// trait, du pointillé serré au trait plein : plus l'effet est fort, plus la
    /// ligne est continue. L'ancienne version traçait quatre seuils au même trait,
    /// distingués par la seule teinte — et sur deux pages, ces teintes étaient
    /// orange, jaune-vert et vert.
    ///
    /// Le sens est enfin le même partout : l'écran caféine peignait « effet fort »
    /// en vert et « effet négligeable » en rouge, exactement à l'envers des écrans
    /// bromazépam et anti-douleur.
    /// </summary>
    public static class EffectPalette
    {
        // Clair, sur fond #F7F9FC
        private static readonly Color NoneLight = Color.FromArgb("#5B6B7C");
        private static readonly Color LightLight = Color.FromArgb("#1F6FB2");
        private static readonly Color ModerateLight = Color.FromArgb("#B86A00");
        private static readonly Color StrongLight = Color.FromArgb("#C0392B");

        // Sombre, sur fond #12171D
        private static readonly Color NoneDark = Color.FromArgb("#93A1B0");
        private static readonly Color LightDark = Color.FromArgb("#5AA9E6");
        private static readonly Color ModerateDark = Color.FromArgb("#E8A33D");
        private static readonly Color StrongDark = Color.FromArgb("#F07167");

        private static bool IsDarkTheme =>
            Application.Current?.RequestedTheme == AppTheme.Dark;

        public static Color For(EffectLevel level) => IsDarkTheme
            ? level switch
            {
                EffectLevel.Strong => StrongDark,
                EffectLevel.Moderate => ModerateDark,
                EffectLevel.Light => LightDark,
                _ => NoneDark
            }
            : level switch
            {
                EffectLevel.Strong => StrongLight,
                EffectLevel.Moderate => ModerateLight,
                EffectLevel.Light => LightLight,
                _ => NoneLight
            };

        /// <summary>
        /// Motif de trait du niveau. Un tableau vide signifie trait plein : la
        /// continuité de la ligne croît avec l'intensité de l'effet.
        /// </summary>
        public static double[] DashPattern(EffectLevel level) => level switch
        {
            EffectLevel.Strong => Array.Empty<double>(),
            EffectLevel.Moderate => new double[] { 12, 4 },
            EffectLevel.Light => new double[] { 6, 4 },
            _ => new double[] { 2, 4 }
        };

        /// <summary>Glyphe redondant avec la couleur, pour les libellés d'état.</summary>
        public static string Glyph(EffectLevel level) => level switch
        {
            EffectLevel.Strong => "▲▲",
            EffectLevel.Moderate => "▲",
            EffectLevel.Light => "▪",
            _ => "▫"
        };

        /// <summary>Libellé français du niveau.</summary>
        public static string Label(EffectLevel level) => level switch
        {
            EffectLevel.Strong => "Effet fort",
            EffectLevel.Moderate => "Effet net",
            EffectLevel.Light => "Effet léger",
            _ => "Effet négligeable"
        };

        /// <summary>Libellé complet, glyphe compris — jamais la couleur seule.</summary>
        public static string Describe(EffectLevel level) => $"{Glyph(level)}  {Label(level)}";

        /// <summary>Couleur neutre d'un repère temporel. Jamais rouge : le rouge
        /// est réservé au dépassement, et la ligne « Maintenant » le partageait
        /// avec le seuil de toxicité sur le même graphique.</summary>
        public static Color Landmark => IsDarkTheme
            ? Color.FromArgb("#93A1B0")
            : Color.FromArgb("#5B6B7C");

        /// <summary>Couleur de la courbe de concentration.</summary>
        public static Color Series => IsDarkTheme
            ? Color.FromArgb("#5AA9E6")
            : Color.FromArgb("#1F5FAF");

        /// <summary>Couleur de la courbe secondaire (saturation, effet).</summary>
        public static Color SeriesSecondary => IsDarkTheme
            ? Color.FromArgb("#E8A33D")
            : Color.FromArgb("#B86A00");

        /// <summary>Teintes distinctes par molécule, pour la page de synthèse.</summary>
        public static Color ForMolecule(string moleculeKey) => moleculeKey switch
        {
            Models.MoleculeKeys.Caffeine => Color.FromArgb(IsDarkTheme ? "#E8A33D" : "#B86A00"),
            Models.MoleculeKeys.Bromazepam => Color.FromArgb(IsDarkTheme ? "#5AA9E6" : "#1F5FAF"),
            Models.MoleculeKeys.Paracetamol => Color.FromArgb(IsDarkTheme ? "#9BB4C9" : "#4A6274"),
            Models.MoleculeKeys.Ibuprofen => Color.FromArgb(IsDarkTheme ? "#B79CE8" : "#6A4FA3"),
            Models.MoleculeKeys.Alcohol => Color.FromArgb(IsDarkTheme ? "#F07167" : "#C0392B"),
            _ => Landmark
        };
    }
}
