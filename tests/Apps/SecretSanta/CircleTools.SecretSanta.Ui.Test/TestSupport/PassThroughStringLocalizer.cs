using Microsoft.Extensions.Localization;

namespace CircleTools.SecretSanta.Ui.Test.TestSupport;

/// <summary>
/// Test double for <see cref="IStringLocalizer{T}"/> that echoes the resource key back as its value.
/// Component tests assert on structure and on non-localized data (e.g. the recipient name), so the
/// actual translations are irrelevant here — key parity is verified separately by the resource tests.
/// </summary>
/// <typeparam name="T">The localizer marker type.</typeparam>
public sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
{
    /// <inheritdoc />
    public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments] => new(name, name, resourceNotFound: false);

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
