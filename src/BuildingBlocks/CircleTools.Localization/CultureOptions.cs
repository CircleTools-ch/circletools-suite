namespace CircleTools.Localization;

/// <summary>
/// Where a host remembers the chosen language.
/// </summary>
/// <remarks>
/// The key is per-app rather than suite-wide because the apps ship as separate origins today:
/// Secret-Santa already stores <c>secretsanta.culture</c>, and changing that key would silently
/// reset the language for everyone who has already chosen one.
/// </remarks>
/// <param name="StorageKey">The key the chosen culture name is stored under.</param>
public sealed record CultureOptions(string StorageKey);
