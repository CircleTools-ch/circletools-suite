using System.Diagnostics.CodeAnalysis;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using UIKit;

namespace CircleTools.SecretSanta.App;

// UIScene lifecycle adoption. Building against the iOS/iPadOS 26+ SDK (Xcode 27, ADR-0065), UIKit traps
// at launch — EXC_BREAKPOINT in _UIApplicationEvaluateRuntimeIssueForNoSceneLifecycleAdoption — for any
// multitasking-capable app that does not adopt the scene lifecycle; MAUI's legacy app-delegate window
// creation is no longer accepted. MAUI ships the runtime: MauiUIApplicationDelegate selects scene startup
// when a UIApplicationSceneManifest naming __MAUI_DEFAULT_SCENE_CONFIGURATION__ + this delegate is present,
// and MauiUISceneDelegate creates the scene-backed MAUI window (dotnet/maui#38601).
//
// Universal Link handling (ADR-0053 reveal links, ADR-0065 follow-up #3) lives here, not on AppDelegate:
// under the scene lifecycle the system delivers "browsing web" user activities to the SCENE delegate
// (AppDelegate.ContinueUserActivity / OpenUrl are never called once scenes are adopted). A tap on
// https://secretsanta.circletools.ch/r…, /groups/…/events/… or an account email link arrives either in
// the scene connection options (cold launch) or via scene:continueUserActivity: (already running). Both
// paths hand the full URL to DeepLinkService, which strips it to the in-app route and renders it offline —
// no server involved (ADR-0014). This is the iOS counterpart to Android's MainActivity intent filters.
[Register("SceneDelegate")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "SceneDelegate is the Apple/MAUI-mandated type name referenced by UIApplicationSceneManifest; it cannot be renamed.")]
public class SceneDelegate : MauiUISceneDelegate
{
    // Cold launch. base.WillConnect creates the MAUI scene-backed window; a link that launched the app is
    // delivered in connectionOptions (never via scene:continueUserActivity: on a cold start). The Blazor
    // tree is not mounted yet, so DeepLinkService holds the link until DeepLinkListener consumes it on
    // first render (same pending-link path Android uses for a cold-started App Link).
    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        base.WillConnect(scene, session, connectionOptions);

        foreach (var activity in connectionOptions.UserActivities)
        {
            ForwardUserActivity(activity);
        }

        ForwardUrlContexts(connectionOptions.UrlContexts);
    }

    // A Universal Link tapped while the app is already running.
    [Export("scene:continueUserActivity:")]
    public void ContinueUserActivity(UIScene scene, NSUserActivity userActivity) =>
        ForwardUserActivity(userActivity);

    // Custom-scheme fallback, while running (parity with the old AppDelegate.OpenUrl). No URL scheme is
    // registered today — reveal/event/email links are HTTPS Universal Links — but this keeps parity with
    // Android if one is ever added.
    [Export("scene:openURLContexts:")]
    public void OpenUrlContexts(UIScene scene, NSSet<UIOpenUrlContext> urlContexts) =>
        ForwardUrlContexts(urlContexts);

    private static void ForwardUserActivity(NSUserActivity userActivity)
    {
        if (userActivity.ActivityType == NSUserActivityType.BrowsingWeb
            && userActivity.WebPageUrl?.AbsoluteString is { Length: > 0 } url)
        {
            Forward(url);
        }
    }

    private static void ForwardUrlContexts(NSSet<UIOpenUrlContext> urlContexts)
    {
        foreach (var context in urlContexts)
        {
            if (context.Url?.AbsoluteString is { Length: > 0 } url)
            {
                Forward(url);
            }
        }
    }

    private static void Forward(string url) =>
        Microsoft.Maui.IPlatformApplication.Current?.Services
            .GetService<DeepLinkService>()?.ReceiveAbsolute(url);
}
