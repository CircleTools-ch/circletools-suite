namespace CircleTools.Ui.Shared;

/// <summary>
/// One entry in the suite's cross-app switcher (web only). Each app in the suite is a separate WASM host
/// on its own origin (ADR-0013), so switching is a full navigation to <see cref="Url"/> rather than a
/// route change. The list is built from host configuration (the <c>SuiteApps</c> section) and injected
/// into the shared shell; MAUI heads register no links, so the switcher self-hides there.
/// </summary>
/// <param name="Name">The app's display (brand) name, e.g. "CircleTools" or "Wichteln".</param>
/// <param name="Url">The app's absolute origin, e.g. <c>https://localhost:7081</c>.</param>
/// <param name="LogoUrl">URL of the app's brand mark, resolved against <see cref="Url"/> when relative.</param>
/// <param name="IsCurrent">Whether this entry is the app currently being viewed (shown, but not a link).</param>
public sealed record SuiteAppLink(string Name, string Url, string LogoUrl, bool IsCurrent);
