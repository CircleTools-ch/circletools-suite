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
// Join links (ADR-0058, ADR-0080): https://secretsanta.circletools.ch/join#t=… opens the in-app join page, so a
// participant with the app installed joins in the app and not in a browser tab (R10, 2026-10-20 device test).
// Forwarded generically by DeepLinkService; the token stays in the fragment.
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "https",
    DataHost = "secretsanta.circletools.ch",
    DataPathPrefix = "/join",
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

        // The soft keyboard (D1, 2026-10-15 sync test; B6). Targeting Android 15+ the app is drawn edge to edge
        // and "adjustResize" no longer shrinks the window, so the system panned the whole view instead: the
        // app bar slid under the status bar, or a bottom sheet's field and button stayed under the keyboard.
        // Shrink the content by the keyboard's height ourselves; the WebView's viewport follows, and the page
        // lays its dialogs and sheets out above the keyboard.
        if (FindViewById(Android.Resource.Id.Content) is { } content)
        {
            AndroidX.Core.View.ViewCompat.SetOnApplyWindowInsetsListener(content, new KeyboardInsetsListener());
        }

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

    // Closes the top-most open overlay (a MudBlazor dialog, menu/select popover or the temporary drawer) by
    // clicking its scrim — exactly what a tap outside it does — and reports whether there was one. Android
    // users expect Back to dismiss an open sheet or drawer before it navigates (B2, 2026-10-14 review).
    private const string CloseTopOverlayScript = """
        (function () {
          var overlays = Array.prototype.filter.call(document.querySelectorAll('.mud-overlay'), function (o) {
            var r = o.getBoundingClientRect();
            return r.width > 0 && r.height > 0 && getComputedStyle(o).visibility !== 'hidden';
          });
          if (overlays.length === 0) { return false; }
          overlays[overlays.length - 1].click();
          return true;
        })();
        """;

    private sealed class WebViewBackCallback(MainActivity activity) : AndroidX.Activity.OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed()
        {
            var webView = CurrentWebView;
            if (webView is null)
            {
                ExitToSystem();
                return;
            }

            // First let an open drawer / dialog / menu close; only when nothing was open does Back navigate.
            webView.EvaluateJavascript(CloseTopOverlayScript, new ValueCallback(closed =>
            {
                if (closed == "true")
                {
                    return;
                }

                if (webView.CanGoBack())
                {
                    webView.GoBack();
                    return;
                }

                ExitToSystem();
            }));
        }

        // No in-app history left: step aside for one dispatch so the system default (home / task switcher)
        // runs — the normal "exit the app" Back at the root. Re-enabled right after (and again on resume):
        // leaving it disabled made every later Back skip the app's own history and overlays.
        private void ExitToSystem()
        {
            Enabled = false;
            activity.OnBackPressedDispatcher.OnBackPressed();
            Enabled = true;
        }
    }

    // Bridges WebView.evaluateJavascript's result callback to a delegate.
    // Pads the content view's bottom by the part of the keyboard that covers it. The navigation-bar strip is
    // left out: the page already reserves it (safe-area insets), so only the keyboard above it is added.
    private sealed class KeyboardInsetsListener : Java.Lang.Object, AndroidX.Core.View.IOnApplyWindowInsetsListener
    {
        public AndroidX.Core.View.WindowInsetsCompat OnApplyWindowInsets(
            Android.Views.View? view,
            AndroidX.Core.View.WindowInsetsCompat? insets)
        {
            if (view is not null && insets is not null)
            {
                // Pad by how far the content actually reaches under the keyboard, not by "keyboard minus navigation
                // bar": whether the content already stops above the navigation bar depends on the Android version
                // (edge to edge on 15+, not on a Galaxy S24 with Android 14), and the guess left the WebView a
                // navigation bar too tall there, so the system panned it and a dialog's title went under the status
                // bar (S24 device test, 2026-10-25).
                var ime = insets.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.Ime())?.Bottom ?? 0;
                var bottom = 0;
                if (ime > 0)
                {
                    var location = new int[2];
                    view.GetLocationInWindow(location);
                    var keyboardTop = (view.RootView ?? view).Height - ime;
                    bottom = Math.Max(0, location[1] + view.Height - keyboardTop);
                }

                if (view.PaddingBottom != bottom)
                {
                    view.SetPadding(view.PaddingLeft, view.PaddingTop, view.PaddingRight, bottom);
                }
            }

            return insets!;
        }
    }

    private sealed class ValueCallback(Action<string?> onResult) : Java.Lang.Object, Android.Webkit.IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => onResult(value?.ToString());
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
