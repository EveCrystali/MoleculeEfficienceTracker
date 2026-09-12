using System.Reflection;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MoleculeEfficienceTracker.Core.Services;
using Plugin.LocalNotification;
using Syncfusion.Licensing;
using Syncfusion.Maui.Core.Hosting;

namespace MoleculeEfficienceTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            RegisterSyncfusionLicense();

            MauiAppBuilder builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureSyncfusionCore()
                .UseMauiCommunityToolkit()
                .UseLocalNotification()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Les calculateurs étaient déjà enregistrés ici, mais jamais résolus :
            // les pages faisaient `new TCalculator()`. Les services partagés le
            // sont désormais pour de bon, et les pages les récupèrent.
            builder.Services.AddSingleton<IAlertService, AlertService>();
            builder.Services.AddSingleton<IResidualLoadService, ResidualLoadService>();
            builder.Services.AddSingleton<CaffeineNotificationService>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            MauiApp app = builder.Build();
            ServiceLocator.Initialize(app.Services);
            return app;
        }

        /// <summary>
        /// Enregistre la licence Syncfusion si la compilation en a reçu une.
        ///
        /// Sans elle, un bandeau d'essai se superpose à chaque graphique. La clé
        /// arrive en métadonnée d'assemblage, injectée par MSBuild depuis la
        /// variable d'environnement SYNCFUSION_LICENSE_KEY, et n'apparaît donc
        /// jamais dans le dépôt.
        /// </summary>
        private static void RegisterSyncfusionLicense()
        {
            string? key = typeof(MauiProgram).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "SyncfusionLicense")?.Value;

            if (!string.IsNullOrWhiteSpace(key))
                SyncfusionLicenseProvider.RegisterLicense(key);
        }
    }

    /// <summary>
    /// Accès aux services partagés depuis les pages construites par XAML.
    ///
    /// Compromis assumé : les pages sont instanciées par le XAML de l'onglet et
    /// n'ont pas de constructeur injecté. Plutôt que de laisser chaque page faire
    /// `new` sur ses dépendances — ce qui multipliait les caches et les lectures
    /// de fichiers — elles passent par ici.
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider? _services;

        public static void Initialize(IServiceProvider services) => _services = services;

        public static T Get<T>() where T : notnull
        {
            if (_services is null)
                throw new InvalidOperationException("Les services ne sont pas encore initialisés.");

            return _services.GetRequiredService<T>();
        }

        public static T? GetOptional<T>() where T : class
            => _services?.GetService<T>();
    }
}
