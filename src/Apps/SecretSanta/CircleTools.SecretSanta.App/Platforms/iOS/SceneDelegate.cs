using System.Diagnostics.CodeAnalysis;
using Foundation;

namespace CircleTools.SecretSanta.App;

// UIScene lifecycle adoption. Building against the iOS/iPadOS 26+ SDK (Xcode 27, ADR-0065), UIKit traps
// at launch — EXC_BREAKPOINT in _UIApplicationEvaluateRuntimeIssueForNoSceneLifecycleAdoption — for any
// multitasking-capable app that does not adopt the scene lifecycle; MAUI's legacy app-delegate window
// creation is no longer accepted. MAUI ships the runtime: MauiUIApplicationDelegate selects scene startup
// when a UIApplicationSceneManifest naming __MAUI_DEFAULT_SCENE_CONFIGURATION__ + this delegate is present,
// and MauiUISceneDelegate creates the scene-backed MAUI window (dotnet/maui#38601).
//
// NOTE: under the scene lifecycle, Universal Links (reveal/event deep links) arrive on the SCENE delegate,
// not on AppDelegate.ContinueUserActivity — so the iOS deep-link forwarding needs to move here. Left as a
// follow-up so this launch fix ships first and the scene-delegate override signatures are added against a
// build that is confirmed to run.
[Register("SceneDelegate")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "SceneDelegate is the Apple/MAUI-mandated type name referenced by UIApplicationSceneManifest; it cannot be renamed.")]
public class SceneDelegate : MauiUISceneDelegate
{
}
