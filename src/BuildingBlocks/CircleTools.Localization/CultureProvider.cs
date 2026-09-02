using System.Globalization;
using CircleTools.Ui.Abstractions;

namespace CircleTools.Localization;

/// <summary>
/// The default <see cref="ICultureProvider"/>: reads and writes the chosen language through an
/// <see cref="IKeyValueStore"/>, so it works unchanged in a browser and on a device.
/// </summary>
/// <param name="store">Where the choice is remembered.</param>
/// <param name="options">Which key it is remembered under.</param>
public sealed class CultureProvider(IKeyValueStore store, CultureOptions options) : ICultureProvider
{
    private CultureInfo _current = AppCultures.Resolve(null);

    // The formatting culture (dates, numbers, currency): the device/OS region, kept independent of the
    // chosen UI language (ADR-0056). A Swiss user reading the app in English still sees Swiss dates — the
    // language only changes the words. Seeded from the process default and replaced with the real platform
    // region in InitializeAsync, before any language is applied.
    private CultureInfo _formatting = FormattingOrDefault(CultureInfo.CurrentCulture);

    /// <inheritdoc />
    public CultureInfo CurrentCulture => _current;

    /// <inheritdoc />
    public event EventHandler? CultureChanged;

    /// <inheritdoc />
    public async ValueTask<CultureInfo> InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Capture the platform's region (browser/OS locale) for formatting before any UI language is
        // applied — both heads seed CurrentCulture from the device at startup (ADR-0056). This is the
        // formatting source of truth from here on; the language choice never overwrites it.
        _formatting = FormattingOrDefault(CultureInfo.CurrentCulture);

        var stored = await store.GetAsync(StorageScope.Durable, options.StorageKey, cancellationToken);

        // First run (nothing stored): follow the device/browser language, mapped to the nearest supported
        // one (any de-* -> de-CH, fr-* -> fr-CH, en-* -> en, else de-CH). The device value is read from
        // CurrentUICulture before Apply overwrites it — on both heads the platform seeds it from the
        // device/browser language at startup. A stored choice always wins, and a signed-in account's
        // language still overrides on the next reconcile (ADR-0035). It is deliberately not persisted
        // here: with no explicit choice the default keeps following the device on each start.
        var name = stored ?? CultureInfo.CurrentUICulture.Name;
        Apply(AppCultures.Resolve(name));
        return _current;
    }

    /// <inheritdoc />
    public async ValueTask SetCultureAsync(string name, CancellationToken cancellationToken = default)
    {
        var culture = AppCultures.Resolve(name);
        if (string.Equals(culture.Name, _current.Name, StringComparison.Ordinal))
        {
            return;
        }

        Apply(culture);

        // The result is deliberately ignored. A preference that could not be remembered is a
        // preference the user re-picks next visit; the language they asked for still applies to
        // this session, and refusing to switch would be a worse answer than forgetting.
        _ = await store.SetAsync(StorageScope.Durable, options.StorageKey, culture.Name, cancellationToken);

        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Applies a language: the UI culture follows it, the formatting culture stays on the device region.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Language and region are decoupled (ADR-0056). The chosen <paramref name="language"/> drives
    /// <see cref="CultureInfo.CurrentUICulture"/> — what <c>IStringLocalizer</c> resolves — while
    /// date/number/currency formatting stays on <see cref="_formatting"/>, the device region captured at
    /// startup. So switching language changes the words, not the date format.
    /// </para>
    /// <para>
    /// Both the process defaults and the calling thread are assigned: setting only
    /// <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> is enough at startup, but not for a change
    /// made while the app is running — the default is consulted only by threads that have not already
    /// resolved their own culture, and by then this one has. Assigning both is what lets a language switch
    /// take effect on a re-render instead of needing a page reload.
    /// </para>
    /// </remarks>
    /// <param name="language">The UI language to apply.</param>
    private void Apply(CultureInfo language)
    {
        _current = language;

        // UI/resource lookup follows the chosen language...
        CultureInfo.DefaultThreadCurrentUICulture = language;
        CultureInfo.CurrentUICulture = language;

        // ...while formatting follows the device region, independent of the language (ADR-0056). The
        // region is fixed for the session, so a language switch never changes the date/number format.
        CultureInfo.DefaultThreadCurrentCulture = _formatting;
        CultureInfo.CurrentCulture = _formatting;
    }

    // The device region to format with, or the Swiss-first default when the platform reports none (e.g.
    // invariant globalization) — so formatting is never the culture-less invariant format.
    private static CultureInfo FormattingOrDefault(CultureInfo platform) =>
        string.IsNullOrEmpty(platform.Name) ? new CultureInfo(AppCultures.DefaultCulture) : platform;
}
