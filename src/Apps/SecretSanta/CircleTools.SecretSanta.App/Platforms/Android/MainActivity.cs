using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.SecretSanta.App;

// SingleTask so a reveal link always reuses the one running instance (delivered via OnNewIntent)
// instead of stacking a second copy of the app (ADR-0053).
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// Android App Link (ADR-0053) for reveal links: https://secretsanta.circletools.ch/r… opens the app.
// AutoVerify makes it open silently once the domain serves a matching /.well-known/assetlinks.json
// (which needs the Play App Signing fingerprint, ADR-0030); until then the link opens via the chooser.
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "secretsanta.circletools.ch",
    DataPathPrefix = "/r",
    AutoVerify = true)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ForwardDeepLink(Intent); // a link that cold-started the app
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        ForwardDeepLink(intent); // a link tapped while the app was already running
    }

    // A reveal App Link arrives as a VIEW intent whose data is the full URL, fragment and all. Hand it
    // to the Blazor app (DeepLinkService), which navigates to its own /r page and renders it offline.
    private static void ForwardDeepLink(Intent? intent)
    {
        if (intent?.Action != Intent.ActionView || intent.DataString is not { Length: > 0 } url)
        {
            return;
        }

        Microsoft.Maui.IPlatformApplication.Current?.Services
            .GetService<DeepLinkService>()?.ReceiveAbsolute(url);
    }
}
