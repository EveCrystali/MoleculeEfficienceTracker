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
    /// </summary>
    public sealed class GaugeDrawable : IDrawable
    {
        private const float StartAngle = 225f;
        private const float TotalSweep = 270f;

        /// <summary>Fraction remplie, entre 0 et 1.</summary>
        public double Progress { get; set; }

        public Color TrackColor { get; set; } = Color.FromArgb("#C3C7CF");

        public Color ProgressColor { get; set; } = Color.FromArgb("#1F5FAF");

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
            if (filled <= 0.001) return;

            // Un filet minimal reste visible : une jauge à 0,3 % qui ne dessine
            // rien laisse croire que la mesure a échoué.
            float sweep = Math.Max((float)(TotalSweep * filled), 3f);

            canvas.StrokeColor = ProgressColor;
            canvas.DrawArc(x, y, size, size, StartAngle, StartAngle - sweep, true, false);
        }
    }
}
