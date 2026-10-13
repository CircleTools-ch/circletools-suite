using System.Diagnostics.CodeAnalysis;
using Foundation;

namespace CircleTools.SecretSanta.App;

[Register("AppDelegate")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "AppDelegate is the Apple/MAUI-mandated type name for the iOS application delegate; it cannot be renamed.")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	// Universal Link / URL handling is NOT here. Once the app adopts the UIScene lifecycle (ADR-0065, see
	// Info.plist UIApplicationSceneManifest + SceneDelegate), iOS delivers incoming links to the scene
	// delegate and never calls AppDelegate.ContinueUserActivity / OpenUrl. The reveal/event/email deep-link
	// forwarding therefore lives in SceneDelegate (ADR-0053).
}
