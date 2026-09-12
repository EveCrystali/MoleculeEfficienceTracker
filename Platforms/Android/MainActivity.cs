using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MoleculeEfficienceTracker
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                               ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                               ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [MetaData("android.app.shortcuts", Resource = "@xml/shortcuts")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Intent is not null)
                Plugin.LocalNotification.LocalNotificationCenter.SetLaunchNotificationFromIntent(Intent);
        }

        protected override void OnNewIntent(Android.Content.Intent? intent)
        {
            base.OnNewIntent(intent);

            if (intent is not null)
                Plugin.LocalNotification.LocalNotificationCenter.NotifyNotificationTapped(intent);
        }
    }
}
