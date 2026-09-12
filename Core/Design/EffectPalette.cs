using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using MoleculeEfficienceTracker.Core.Services;

namespace MoleculeEfficienceTracker.Core.Design
{
    /// <summary>
    /// Palette des niveaux d'effet, côté C#.
    ///
    /// Elle ne définit plus ses propres teintes : elle branche chaque niveau sur un
    /// rôle Material 3, les mêmes valeurs que Resources/Styles/Colors.xaml. Les deux
    /// fichiers avaient divergé — le XAML disait #1F6FB2 là où le C# disait #1F5FAF,
    /// sur le même écran.
    ///
    ///   Négligeable → onSurfaceVariant · Léger → primary
    ///   Net         → secondary        · Fort  → error
    ///
    /// Deux règles gouvernent le choix de ces rôles, et elles corrigent le même
    /// défaut.
    ///
    /// Aucun vert ne porte de sens : la lecture rouge/vert est inaccessible en
    /// deutéranopie. L'échelle monte du gris au bleu, puis à l'orange, puis au
    /// rouge — un axe que l'œil distingue quelle que soit la vision des couleurs.
    ///
    /// La couleur ne porte jamais seule. Chaque niveau a son motif de trait, du
    /// pointillé serré au trait plein : plus l'effet est fort, plus la ligne est
    /// continue. L'ancienne version traçait quatre seuils au même trait, distingués
    /// par la seule teinte — et sur deux pages, ces teintes étaient orange,
    /// jaune-vert et vert.
    /// </summary>
    public static class EffectPalette
    {
        // Rôles, thème clair — sur surface #FCFCFF
        private static readonly Color NoneLight = Color.FromArgb("#43474E");      // onSurfaceVariant
        private static readonly Color LightLight = Color.FromArgb("#1F5FAF");     // primary
        private static readonly Color ModerateLight = Color.FromArgb("#8A5300");  // secondary
        private static readonly Color StrongLight = Color.FromArgb("#B3261E");    // error

        // Rôles, thème sombre — sur surface #111418
        private static readonly Color NoneDark = Color.FromArgb("#C3C7CF");
        private static readonly Color LightDark = Color.FromArgb("#A8C7FA");
        private static readonly Color ModerateDark = Color.FromArgb("#FFB95C");
        private static readonly Color StrongDark = Color.FromArgb("#F2B8B5");

        // Conteneurs — le fond des puces d'état. Jamais le rôle plein : du texte
        // sombre sur un fond teinté clair, comme le veut Material 3.
        private static readonly Color NoneContainerLight = Color.FromArgb("#E3E6EA");
        private static readonly Color LightContainerLight = Color.FromArgb("#D6E3FF");
        private static readonly Color ModerateContainerLight = Color.FromArgb("#FFDDB7");
        private static readonly Color StrongContainerLight = Color.FromArgb("#F9DEDC");

        private static readonly Color NoneContainerDark = Color.FromArgb("#323539");
        private static readonly Color LightContainerDark = Color.FromArgb("#2C4B6F");
        private static readonly Color ModerateContainerDark = Color.FromArgb("#5F3B00");
        private static readonly Color StrongContainerDark = Color.FromArgb("#8C1D18");

        private static bool IsDarkTheme =>
            Application.Current?.RequestedTheme == AppTheme.Dark;

        /// <summary>Couleur du niveau : le rôle plein, pour un trait ou un texte.</summary>
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

        /// <summary>Fond de la puce d'état : le conteneur du même rôle.</summary>
        public static Color Container(EffectLevel level) => IsDarkTheme
            ? level switch
            {
                EffectLevel.Strong => StrongContainerDark,
                EffectLevel.Moderate => ModerateContainerDark,
                EffectLevel.Light => LightContainerDark,
                _ => NoneContainerDark
            }
            : level switch
            {
                EffectLevel.Strong => StrongContainerLight,
                EffectLevel.Moderate => ModerateContainerLight,
                EffectLevel.Light => LightContainerLight,
                _ => NoneContainerLight
            };

        /// <summary>
        /// Texte posé sur le conteneur. Au thème clair, le rôle « on-container »
        /// est bien plus sombre que le rôle plein : c'est ce qui rend la puce
        /// lisible au lieu de la laisser vibrer.
        /// </summary>
        public static Color OnContainer(EffectLevel level) => IsDarkTheme
            ? level switch
            {
                EffectLevel.Strong => Color.FromArgb("#F9DEDC"),
                EffectLevel.Moderate => Color.FromArgb("#FFDDB7"),
                EffectLevel.Light => Color.FromArgb("#D6E3FF"),
                _ => Color.FromArgb("#E2E2E6")
            }
            : level switch
            {
                EffectLevel.Strong => Color.FromArgb("#410E0B"),
                EffectLevel.Moderate => Color.FromArgb("#2C1600"),
                EffectLevel.Light => Color.FromArgb("#001C38"),
                _ => Color.FromArgb("#1A1C1E")
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
        public static Color Landmark => IsDarkTheme ? NoneDark : NoneLight;

        /// <summary>Couleur de la courbe de concentration.</summary>
        public static Color Series => IsDarkTheme ? LightDark : LightLight;

        /// <summary>Couleur de la courbe secondaire (saturation, effet).</summary>
        public static Color SeriesSecondary => IsDarkTheme ? ModerateDark : ModerateLight;

        /// <summary>Le gris des axes et de la grille : outlineVariant.</summary>
        public static Color GridLine => IsDarkTheme
            ? Color.FromArgb("#43474E")
            : Color.FromArgb("#C3C7CF");

        /// <summary>Teintes distinctes par molécule, pour la page Aujourd'hui.</summary>
        public static Color ForMolecule(string moleculeKey) => moleculeKey switch
        {
            Models.MoleculeKeys.Caffeine => IsDarkTheme ? ModerateDark : ModerateLight,
            Models.MoleculeKeys.Bromazepam => IsDarkTheme ? LightDark : LightLight,
            Models.MoleculeKeys.Paracetamol => Color.FromArgb(IsDarkTheme ? "#9BB4C9" : "#4A6274"),
            Models.MoleculeKeys.Ibuprofen => Color.FromArgb(IsDarkTheme ? "#D0BCFF" : "#65558F"),
            Models.MoleculeKeys.Alcohol => IsDarkTheme ? StrongDark : StrongLight,
            _ => Landmark
        };
    }
}
