// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

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
    //
    // A managed exception escaping this override is converted to an NSException and aborts the process
    // inside +[UIScene _sceneForFBSScene:…] — the crash report then shows only SIGABRT, never the .NET
    // message (0.1.113 crashed on iPad exactly this way). So neither half may throw out of here: a failed
    // window creation is shown on screen with its full exception text, and a failed link hand-off only
    // drops that one link — it must never cost the user the app launch.
    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
#pragma warning disable CA1031 // Any exception here aborts the app with its message lost; surface it instead.
        try
        {
            base.WillConnect(scene, session, connectionOptions);
        }
        catch (Exception ex)
        {
            ShowStartupFailure(scene, ex);
            return;
        }

        try
        {
            foreach (var activity in connectionOptions.UserActivities)
            {
                ForwardUserActivity(activity);
            }

            ForwardUrlContexts(connectionOptions.UrlContexts);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"SceneDelegate: dropped launch deep link: {ex}");
        }
#pragma warning restore CA1031
    }

    // Last-resort native screen when MAUI could not build its window: the exception text, selectable so it
    // can be copied from the device. Deliberately plain UIKit — the MAUI/Blazor stack is what just failed.
    private void ShowStartupFailure(UIScene scene, Exception exception)
    {
        Console.Error.WriteLine($"SceneDelegate: window creation failed: {exception}");

        if (scene is not UIWindowScene windowScene)
        {
            return;
        }

        var details = new UITextView
        {
            Text = exception.ToString(),
            Editable = false,
            Selectable = true,
            Font = UIFont.GetMonospacedSystemFont(12, UIFontWeight.Regular),
            BackgroundColor = UIColor.SystemBackground,
            TextColor = UIColor.Label,
        };

        var window = new UIWindow(windowScene)
        {
            RootViewController = new UIViewController { View = details },
        };
        Window = window;
        window.MakeKeyAndVisible();
    }

    // A Universal Link tapped while the app is already running. MauiUISceneDelegate already declares a
    // virtual ContinueUserActivity carrying [Export("scene:continueUserActivity:")], so we override it
    // (not re-export) — chaining base keeps MAUI's own scene-lifecycle dispatch intact. MAUI declares it
    // bool-returning (whether a lifecycle handler took it); UIKit ignores the value, so we pass base's through.
    public override bool ContinueUserActivity(UIScene scene, NSUserActivity userActivity)
    {
        var handled = base.ContinueUserActivity(scene, userActivity);
        ForwardUserActivity(userActivity);
        return handled;
    }

    // Custom-scheme fallback, while running (parity with the old AppDelegate.OpenUrl). No URL scheme is
    // registered today — reveal/event/email links are HTTPS Universal Links — but this keeps parity with
    // Android if one is ever added. The base's virtual method for scene:openURLContexts: is named OpenUrl,
    // so we override that; re-exporting the selector under a second name would clash at type registration.
    public override bool OpenUrl(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
    {
        var handled = base.OpenUrl(scene, urlContexts);
        ForwardUrlContexts(urlContexts);
        return handled;
    }

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
