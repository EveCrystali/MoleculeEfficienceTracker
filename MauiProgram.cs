using Microsoft.Extensions.Logging;
using MoleculeEfficienceTracker.Core.Services;
using Syncfusion.Maui.Core.Hosting;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui;

namespace MoleculeEfficienceTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>().ConfigureSyncfusionCore() // Ajout pour Syncfusion
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            }).UseMauiCommunityToolkit();
            builder.Services.AddSingleton<BromazepamCalculator>();
            builder.Services.AddTransient<MainPage>();
#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}