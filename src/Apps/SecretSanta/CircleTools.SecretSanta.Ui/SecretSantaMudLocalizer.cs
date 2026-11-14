// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Localization;
using MudBlazor;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Translates MudBlazor's own built-in strings — the accessible names of its close buttons and nav-group
/// toggles — so screen readers do not announce English ("Close", "Toggle Familie") inside the German or
/// French UI (UX review P6, 2026-10-14).
/// </summary>
/// <remarks>
/// Each MudBlazor key <c>K</c> is looked up as <c>Mud_K</c> in <see cref="AppStrings"/>. A key the app
/// does not translate reports <see cref="LocalizedString.ResourceNotFound"/>, so MudBlazor falls back to
/// its built-in English for it.
/// </remarks>
/// <param name="localizer">The app's string localizer.</param>
internal sealed class SecretSantaMudLocalizer(IStringLocalizer<AppStrings> localizer) : MudLocalizer
{
    /// <inheritdoc />
    public override LocalizedString this[string key] => Translate(key, localizer[$"Mud_{key}"]);

    /// <inheritdoc />
    public override LocalizedString this[string key, params object[] arguments] =>
        Translate(key, localizer[$"Mud_{key}", arguments]);

    private static LocalizedString Translate(string key, LocalizedString found) =>
        found.ResourceNotFound ? new LocalizedString(key, key, resourceNotFound: true) : new LocalizedString(key, found.Value);
}
