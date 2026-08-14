namespace CircleTools.Ui.Shared;

/// <summary>
/// The per-app visual identity shown in the shared shell (ADR-0040 amendment). The in-app surfaces stay
/// neutral across the suite; only the <b>app bar</b> carries the app's festive brand colour, plus its
/// brand mark, so the two apps are told apart at a glance. Registered once per app in that app's UI
/// composition root and injected by both the web and MAUI shells.
/// </summary>
/// <param name="LogoUrl">
/// URL of the app's brand mark, resolved against the host base href (e.g. <c>favicon.svg</c>). Shown
/// before the title in the app bar; an empty value simply renders no logo.
/// </param>
/// <param name="AppBarColor">The app-bar background colour (a brand hex), the same in light and dark so
/// the identity cue is stable.</param>
/// <param name="AppBarText">The app-bar foreground colour (title + icons), chosen for contrast on
/// <paramref name="AppBarColor"/>.</param>
public sealed record AppBranding(string LogoUrl, string AppBarColor, string AppBarText);
