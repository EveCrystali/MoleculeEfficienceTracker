namespace MoleculeEfficienceTracker.Core.Services
{
    public interface IAlertService
    {
        Task ShowAlertAsync(string title, string message, string cancel = "OK");
        Task<bool> ShowConfirmAsync(string title, string message, string accept = "Oui", string cancel = "Non");
    }

    public class AlertService : IAlertService
    {
        public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
        {
            try
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert(title, message, cancel);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"{title}: {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Alert Error: {title} - {message} ({ex.Message})");
            }
        }

        public async Task<bool> ShowConfirmAsync(string title, string message, string accept = "Oui", string cancel = "Non")
        {
            try
            {
                if (Application.Current?.MainPage != null)
                {
                    return await Application.Current.MainPage.DisplayAlert(title, message, accept, cancel);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Confirm: {title} - {message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Confirm Error: {title} - {message} ({ex.Message})");
                return false;
            }
        }
    }
}
