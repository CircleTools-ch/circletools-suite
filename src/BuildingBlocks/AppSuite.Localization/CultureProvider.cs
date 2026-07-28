using System.Globalization;
using AppSuite.Ui.Abstractions;

namespace AppSuite.Localization;

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
        Apply(AppCultures.Resolve(stored));
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
        await store.SetAsync(StorageScope.Durable, options.StorageKey, culture.Name, cancellationToken);
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
