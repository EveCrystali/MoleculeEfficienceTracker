using Microsoft.Maui.Graphics;

namespace MoleculeEfficienceTracker.Controls
{
    /// <summary>
    /// L'anneau de la valeur du moment.
    ///
    /// Un nombre seul ne dit pas s'il est grand. « 1,6 mg/L » ne signifie rien sans
    /// l'échelle où il se place ; l'anneau la donne d'un coup d'œil, et la couleur
    /// du niveau s'y lit sans être lue.
    ///
    /// La course va de 225° à −45°, soit 270° ouverts vers le bas : une jauge
    /// fermée se confondrait avec un cercle de progression, et l'ouverture dit le
    /// sens de lecture. Les angles de Microsoft.Maui.Graphics se comptent depuis
    /// trois heures, positifs dans le sens trigonométrique — d'où l'angle de fin
    /// négatif et le tracé horaire.
    ///
    /// L'arc se dégrade du clair au plein sur sa course. Un pinceau dégradé ne
    /// s'applique pas à un trait : il est donc tracé par segments de cinq degrés,
    /// dont les bouts arrondis se recouvrent et effacent les coutures.
    /// </summary>
    public sealed class GaugeDrawable : IDrawable
    {
        private const float StartAngle = 225f;
        private const float TotalSweep = 270f;

        /// <summary>Fraction remplie, entre 0 et 1.</summary>
        public double Progress { get; set; }

        public Color TrackColor { get; set; } = Color.FromArgb("#C3C7CF");

        /// <summary>Couleur d'arrivée de l'arc, au bout de sa course.</summary>
        public Color ProgressColor { get; set; } = Color.FromArgb("#1F5FAF");

        /// <summary>Couleur de départ de l'arc. Égale à la précédente, aucun dégradé.</summary>
        public Color StartColor { get; set; } = Color.FromArgb("#1F5FAF");

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float thickness = Math.Max(6f, Math.Min(dirtyRect.Width, dirtyRect.Height) * 0.085f);
            float inset = thickness / 2f + 1f;

            float size = Math.Min(dirtyRect.Width, dirtyRect.Height) - inset * 2f;
            if (size <= 0) return;

            float x = dirtyRect.X + (dirtyRect.Width - size) / 2f;
            float y = dirtyRect.Y + (dirtyRect.Height - size) / 2f;

            canvas.StrokeSize = thickness;
            canvas.StrokeLineCap = LineCap.Round;

            canvas.StrokeColor = TrackColor;
            canvas.DrawArc(x, y, size, size, StartAngle, StartAngle - TotalSweep, true, false);

            double filled = Math.Clamp(Progress, 0, 1);
            if (filled <= 0.0005) return;

            // Un filet minimal reste visible : une jauge à 0,3 % qui ne dessine
            // rien laisse croire que la mesure a échoué.
            float sweep = Math.Max((float)(TotalSweep * filled), 3f);

            int segments = Math.Clamp((int)(sweep / 5f), 1, 64);
            float step = sweep / segments;

            for (int i = 0; i < segments; i++)
            {
                canvas.StrokeColor = Blend(StartColor, ProgressColor, (i + 1f) / segments);
                canvas.DrawArc(x, y, size, size,
                               StartAngle - step * i,
                               StartAngle - step * (i + 1),
                               true, false);
            }
        }

        /// <summary>Interpolation linéaire entre deux couleurs, composante à composante.</summary>
        public static Color Blend(Color from, Color to, float t)
            => Color.FromRgba(
                from.Red + (to.Red - from.Red) * t,
                from.Green + (to.Green - from.Green) * t,
                from.Blue + (to.Blue - from.Blue) * t,
                from.Alpha + (to.Alpha - from.Alpha) * t);
    }
}
