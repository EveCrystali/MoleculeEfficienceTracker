namespace MoleculeEfficienceTracker.Core.Services
{
    public interface IAlertService
    {
        Task ShowAlertAsync(string title, string message, string cancel = "OK");
        Task<bool> ShowConfirmAsync(string title, string message, string accept = "Oui", string cancel = "Non");
    }

    /// <summary>
    /// Boîtes de dialogue.
    ///
    /// Passait par Application.Current.MainPage, obsolète depuis MAUI 9 et promise
    /// au retrait. La page racine se lit maintenant sur la fenêtre. Les appels sont
    /// en outre replacés sur le fil d'interface : rien ne garantissait qu'ils en
    /// venaient, et un appel depuis un fil de fond faisait tomber l'application.
    /// </summary>
    public class AlertService : IAlertService
    {
        private static Page? RootPage =>
            Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;

        public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
        {
            try
            {
                Page? page = RootPage;
                if (page is null)
                {
                    System.Diagnostics.Debug.WriteLine($"{title}: {message}");
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlert(title, message, cancel));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Alerte impossible : {title} - {message} ({ex.Message})");
            }
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string accept = "Oui", string cancel = "Non")
        {
            try
            {
                Page? page = RootPage;
                if (page is null)
                {
                    System.Diagnostics.Debug.WriteLine($"Confirmation impossible : {title} - {message}");
                    return false;
                }

                return await MainThread.InvokeOnMainThreadAsync(() => page.DisplayAlert(title, message, accept, cancel));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Confirmation impossible : {title} - {message} ({ex.Message})");
                return false;
            }
        }
    }
}
