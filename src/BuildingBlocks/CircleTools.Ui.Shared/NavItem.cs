namespace CircleTools.Ui.Shared;

/// <summary>
/// One primary navigation destination, rendered both in the wide-screen drawer and in the mobile
/// bottom navigation bar (ADR-0040). <paramref name="Icon"/> is a MudBlazor icon string.
/// </summary>
/// <param name="Href">Relative route.</param>
/// <param name="Label">Localised label.</param>
/// <param name="Icon">MudBlazor icon (e.g. <c>Icons.Material.Filled.Home</c>).</param>
/// <param name="Exact">Match the route exactly (true for the home route).</param>
public sealed record NavItem(string Href, string Label, string Icon, bool Exact = false);
