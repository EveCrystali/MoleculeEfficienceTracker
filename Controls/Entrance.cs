namespace MoleculeEfficienceTracker.Controls
{
    /// <summary>
    /// L'entrée en cascade des cartes d'un écran.
    ///
    /// Chaque carte monte de vingt pixels en s'opacifiant, décalée de soixante
    /// millisecondes sur la précédente. Le décalage est tout : jouées ensemble,
    /// les cartes donnent l'impression d'un écran qui clignote ; jouées en
    /// cascade, d'un écran qui se compose.
    ///
    /// L'animation vise exactement les valeurs de repos — opacité pleine,
    /// translation nulle. Les remettre à la main pendant qu'elle court ne la
    /// contrarie donc pas : elle y arrivait de toute façon.
    /// </summary>
    public static class Entrance
    {
        private const uint Duration = 280;
        private const int Stagger = 60;
        private const double Rise = 20;

        public static void Play(Layout root)
        {
            List<VisualElement> cards = root.Children
                                            .OfType<VisualElement>()
                                            .Where(c => c.IsVisible)
                                            .ToList();

            if (cards.Count == 0) return;

            foreach (VisualElement card in cards)
            {
                card.Opacity = 0;
                card.TranslationY = Rise;
            }

            for (int i = 0; i < cards.Count; i++)
                _ = RiseAsync(cards[i], i * Stagger);
        }

        /// <summary>Remet les cartes au repos, quand l'écran disparaît.</summary>
        public static void Reset(Layout root)
        {
            foreach (VisualElement card in root.Children.OfType<VisualElement>())
            {
                card.Opacity = 1;
                card.TranslationY = 0;
            }
        }

        private static async Task RiseAsync(VisualElement card, int delay)
        {
            try
            {
                if (delay > 0) await Task.Delay(delay);

                await Task.WhenAll(
                    card.FadeToAsync(1, Duration, Easing.CubicOut),
                    card.TranslateToAsync(0, 0, Duration, Easing.CubicOut));
            }
            catch
            {
                // Une animation interrompue par un changement d'onglet ne doit rien
                // casser : la carte est simplement remise au repos.
                card.Opacity = 1;
                card.TranslationY = 0;
            }
        }
    }
}
