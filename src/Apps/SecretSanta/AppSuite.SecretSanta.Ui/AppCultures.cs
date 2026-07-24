using System.Globalization;

namespace AppSuite.SecretSanta.Ui;

/// <summary>
/// The product languages (ADR-0011) and the browser-storage key the chosen one is remembered under.
/// The culture is applied once at startup, before the host runs, because <see cref="CultureInfo"/>
/// defaults are read when components first render.
/// </summary>
public static class AppCultures
{
    /// <summary>The <c>localStorage</c> key holding the user's chosen culture name.</summary>
    public const string StorageKey = "secretsanta.culture";

    /// <summary>The culture used when the user has not chosen one (Swiss-first).</summary>
    public const string DefaultCulture = "de-CH";

    /// <summary>The cultures offered in the UI, in display order.</summary>
    public static IReadOnlyList<string> Supported { get; } = ["de-CH", "en", "fr-CH"];

    /// <summary>
    /// Resolves a stored culture name to a supported <see cref="CultureInfo"/>, falling back to
    /// <see cref="DefaultCulture"/> for anything missing or unrecognised.
    /// </summary>
    /// <param name="name">The stored culture name, which may be <see langword="null"/> or unknown.</param>
    /// <returns>A supported culture.</returns>
    public static CultureInfo Resolve(string? name)
    {
        var match = Supported.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
        return new CultureInfo(match ?? DefaultCulture);
    }

    /// <summary>Gets the display name shown in the language picker for a supported culture.</summary>
    /// <param name="name">The culture name.</param>
    /// <returns>The native display name (e.g. "Deutsch").</returns>
    public static string DisplayName(string name) => name switch
    {
        "de-CH" => "Deutsch",
        "en" => "English",
        "fr-CH" => "Français",
        _ => name,
    };
}
