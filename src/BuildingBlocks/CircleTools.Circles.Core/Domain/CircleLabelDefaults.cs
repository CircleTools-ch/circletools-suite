namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// The label a new circle starts with, per language (FR-016).
/// </summary>
/// <remarks>
/// This is a default <em>at creation time only</em>. Once stored, a label is user data: it is never
/// re-translated and never re-defaulted, so a circle labelled <c>Familie</c> still reads
/// <c>Familie</c> to a member whose language is French. That is why the label lives in the database
/// rather than in a resource file — a resource lookup would silently translate it on every render.
/// </remarks>
public static class CircleLabelDefaults
{
    /// <summary>The label a German-speaking creator starts with.</summary>
    public const string German = "Familie";

    /// <summary>The label an English-speaking creator starts with.</summary>
    public const string English = "Family";

    /// <summary>The label a French-speaking creator starts with.</summary>
    public const string French = "Famille";

    /// <summary>Gets the default label for a language.</summary>
    /// <param name="language">
    /// A language or culture name (<c>de-CH</c>, <c>en</c>, <c>fr-CH</c>). Unknown, empty and
    /// <c>null</c> values fall back to German, the suite's default language.
    /// </param>
    /// <returns>The default label.</returns>
    public static string ForLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return German;
        }

        var trimmed = language.Trim();

        if (trimmed.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        return trimmed.StartsWith("fr", StringComparison.OrdinalIgnoreCase) ? French : German;
    }
}
