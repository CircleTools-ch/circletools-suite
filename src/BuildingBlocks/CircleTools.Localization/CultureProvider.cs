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

    /// <inheritdoc />
    public CultureInfo CurrentCulture => _current;

    /// <inheritdoc />
    public event EventHandler? CultureChanged;

    /// <inheritdoc />
    public async ValueTask<CultureInfo> InitializeAsync(CancellationToken cancellationToken = default)
    {
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
    /// Applies a culture to both the process defaults and the calling thread.
    /// </summary>
    /// <remarks>
    /// Setting only <see cref="CultureInfo.DefaultThreadCurrentCulture"/> is enough at startup, but not
    /// for a change made while the app is running: the default is consulted only by threads that have
    /// not already resolved their own <see cref="CultureInfo.CurrentCulture"/>, and by then this one
    /// has. Assigning both is what lets the language switch take effect on a re-render instead of
    /// needing a page reload.
    /// </remarks>
    /// <param name="culture">The culture to apply.</param>
    private void Apply(CultureInfo culture)
    {
        _current = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
