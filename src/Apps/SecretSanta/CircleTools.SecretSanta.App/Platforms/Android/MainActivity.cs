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
// Event deep link (MNF-6, FR-044): a calendar entry's "open in app" link is the event route
// https://secretsanta.circletools.ch/groups/{gid}/events/{eid}. Forwarded generically (ForwardDeepLink)
// and routed by DeepLinkService to the in-app EventHub — offline, exactly like the reveal link.
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "secretsanta.circletools.ch",
    DataPathPrefix = "/groups",
    AutoVerify = true)]
// Account email links (FR-007/FR-009, ADR-0053): the transactional links the Main service mails live on
// this app's own origin (secretsanta.circletools.ch, per the "secretsanta" source app). Claim them so a
// user who registered or changed their email IN the app confirms it IN the app (the matching in-app
// Blazor route: /verify-email, /reset-password, /confirm-email-change) instead of a detached browser tab.
// Forwarded generically by DeepLinkService like the reveal/event links. Like those, AutoVerify only opens
// the app silently once /.well-known/assetlinks.json is served with the installed build's Play App Signing
// fingerprint (ADR-0030) — otherwise the link falls back to the browser/chooser.
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "secretsanta.circletools.ch",
    DataPathPrefix = "/verify-email",
    AutoVerify = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "secretsanta.circletools.ch",
    DataPathPrefix = "/reset-password",
    AutoVerify = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "secretsanta.circletools.ch",
    DataPathPrefix = "/confirm-email-change",
    AutoVerify = true)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Route the Android Back gesture / button / hardware key into the app's own in-app history
        // (the hybrid WebView), so Back navigates a view back instead of finishing the activity. Only
        // when there is nothing left to go back to does the system default (leave the app) take over.
        OnBackPressedDispatcher.AddCallback(this, new WebViewBackCallback(this));

        ForwardDeepLink(Intent); // a link that cold-started the app
    }

    // The platform WebView behind the BlazorWebView. Its back/forward list tracks the Blazor client-side
    // navigations (history.pushState), so CanGoBack/GoBack maps straight onto the app's own history.
    private static Android.Webkit.WebView? CurrentWebView
    {
        get
        {
            var windows = Microsoft.Maui.Controls.Application.Current?.Windows;
            var page = windows is { Count: > 0 } ? windows[0].Page as MainPage : null;
            return page?.HybridWebView?.Handler?.PlatformView as Android.Webkit.WebView;
        }
    }

    private sealed class WebViewBackCallback(MainActivity activity) : AndroidX.Activity.OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            var webView = CurrentWebView;
            if (webView is not null && webView.CanGoBack())
            {
                webView.GoBack();
                return;
            }

            // No in-app history left: disable this callback and re-dispatch so the system's default
            // behaviour (home / task switcher) runs — the normal "exit the app" Back at the root.
            Enabled = false;
            activity.OnBackPressedDispatcher.OnBackPressed();
        }
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
