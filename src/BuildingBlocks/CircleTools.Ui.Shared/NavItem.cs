// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Ui.Shared;

/// <summary>
/// One primary navigation destination, rendered both in the wide-screen drawer and in the mobile
/// bottom navigation bar (ADR-0040). <paramref name="Icon"/> is a MudBlazor icon string.
/// </summary>
/// <param name="Href">Relative route.</param>
/// <param name="Label">Localised label.</param>
/// <param name="Icon">MudBlazor icon (e.g. <c>Icons.Material.Filled.Home</c>).</param>
/// <param name="Exact">Match the route exactly (true for the home route).</param>
/// <param name="AlsoActiveFor">Further route prefixes under which the tab is active too (the groups tab on a group's pages).</param>
public sealed record NavItem(string Href, string Label, string Icon, bool Exact = false, IReadOnlyList<string>? AlsoActiveFor = null);
