using System.Diagnostics.CodeAnalysis;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using UIKit;

namespace CircleTools.SecretSanta.App;

[Register("AppDelegate")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "AppDelegate is the Apple/MAUI-mandated type name for the iOS application delegate; it cannot be renamed.")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// Universal Link handling — the iOS counterpart to Android's MainActivity intent filters + ForwardDeepLink
	// (ADR-0053 reveal links, ADR-0065 follow-up #3). A tap on https://secretsanta.circletools.ch/r… or
	// /groups/…/events/… arrives as a "browsing web" user activity (both on cold launch and while running,
	// since the MAUI head uses the classic app-delegate lifecycle, not scenes). Hand the full URL to
	// DeepLinkService, which strips it to the in-app route and renders it offline — no server involved.
	public override bool ContinueUserActivity(UIApplication application, NSUserActivity userActivity, UIApplicationRestorationHandler completionHandler)
	{
		if (userActivity.ActivityType == NSUserActivityType.BrowsingWeb
			&& userActivity.WebPageUrl?.AbsoluteString is { Length: > 0 } url)
		{
			ForwardDeepLink(url);
			return true;
		}

		return false;
	}

	// Custom-scheme fallback. No URL scheme is registered today (reveal/event links are HTTPS Universal
	// Links, which is the preferred mechanism), but this keeps parity with Android if one is ever added.
	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	{
		if (url.AbsoluteString is { Length: > 0 } absoluteUrl)
		{
			ForwardDeepLink(absoluteUrl);
			return true;
		}

		return false;
	}

	private static void ForwardDeepLink(string url) =>
		Microsoft.Maui.IPlatformApplication.Current?.Services
			.GetService<DeepLinkService>()?.ReceiveAbsolute(url);
}
