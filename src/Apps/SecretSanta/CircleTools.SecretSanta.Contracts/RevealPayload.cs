using System.Text.Json.Serialization;

namespace CircleTools.SecretSanta.Contracts;

/// <summary>
/// Self-contained payload carried inside a Secret-Santa reveal link (ADR-0014). It describes a
/// single giver's assignment and is decoded entirely client-side, with no backend. The
/// JSON property keys are intentionally short to keep encoded URLs small.
/// </summary>
/// <param name="Version">The payload format version (see <see cref="CurrentVersion"/>).</param>
/// <param name="RecipientName">The name of the person the giver draws (the spoiler-sensitive value).</param>
/// <param name="Wishlist">The recipient's wishlist items.</param>
/// <param name="BudgetAmount">The optional gift budget amount.</param>
/// <param name="BudgetCurrency">The optional ISO currency code for <paramref name="BudgetAmount"/>.</param>
/// <param name="EventDate">The optional event date, formatted as a round-trip (ISO 8601) string.</param>
/// <param name="EventLocation">The optional event location.</param>
/// <param name="EventNotes">Optional free-text event notes.</param>
public sealed record RevealPayload(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("n")] string RecipientName,
    [property: JsonPropertyName("w")] IReadOnlyList<string> Wishlist,
    [property: JsonPropertyName("ba")] decimal? BudgetAmount,
    [property: JsonPropertyName("bc")] string? BudgetCurrency,
    [property: JsonPropertyName("ed")] string? EventDate,
    [property: JsonPropertyName("el")] string? EventLocation,
    [property: JsonPropertyName("en")] string? EventNotes)
{
    /// <summary>The current reveal-payload format version.</summary>
    public const int CurrentVersion = 1;
}
