using CircleTools.Ui.Abstractions;

namespace CircleTools.Ui.Shared;

/// <summary>The three choices the in-app theme switch offers (ADR-0040).</summary>
public enum ThemeMode
{
    /// <summary>Follow the operating-system light/dark preference.</summary>
    System,
    Light,
    Dark,
}

/// <summary>
/// Holds the resolved light/dark state for the app shell and persists the user's choice through the
/// shared <see cref="IKeyValueStore"/> seam (ADR-0028) — localStorage on the web, a file on device —
/// so the same switch works on every head. The shell feeds it the OS preference (which only the
/// MudThemeProvider can read, via JS); this type owns the System→dark resolution and notifies
/// listeners (the shell re-renders; the MAUI head mirrors it to <c>UserAppTheme</c>).
/// </summary>
public sealed class ThemeState(IKeyValueStore store)
{
    private const string StorageKey = "circletools.themeMode";

    /// <summary>The user's chosen mode. Defaults to <see cref="ThemeMode.System"/>.</summary>
    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    /// <summary>The resolved value the theme provider should use right now.</summary>
    public bool IsDark { get; private set; }

    /// <summary>Raised whenever <see cref="Mode"/> or <see cref="IsDark"/> changes.</summary>
    public event Action? Changed;

    private bool _systemIsDark;

    /// <summary>Loads the stored choice and resolves against the current OS preference.</summary>
    public async Task InitializeAsync(bool systemIsDark)
    {
        _systemIsDark = systemIsDark;
        var stored = await store.GetAsync(StorageScope.Durable, StorageKey);
        Mode = stored switch
        {
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
        Recompute();
    }

    /// <summary>Records a new choice, persists it, and re-resolves.</summary>
    public async Task SetModeAsync(ThemeMode mode)
    {
        Mode = mode;
        var value = mode switch { ThemeMode.Light => "light", ThemeMode.Dark => "dark", _ => "system" };
        await store.SetAsync(StorageScope.Durable, StorageKey, value);
        Recompute();
    }

    /// <summary>Called when the OS preference changes; only matters in <see cref="ThemeMode.System"/>.</summary>
    public void SystemPreferenceChanged(bool systemIsDark)
    {
        _systemIsDark = systemIsDark;
        if (Mode == ThemeMode.System)
        {
            Recompute();
        }
    }

    private void Recompute()
    {
        IsDark = Mode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            _ => _systemIsDark,
        };
        Changed?.Invoke();
    }
}
