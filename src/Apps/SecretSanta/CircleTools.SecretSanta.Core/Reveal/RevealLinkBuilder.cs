using CircleTools.SecretSanta.Contracts;

namespace CircleTools.SecretSanta.Core.Reveal;

/// <summary>
/// Composes and parses reveal links. Everything travels in the URL <b>fragment</b> so it is never
/// sent to a server (ADR-0014). The only human-readable token is <c>for=</c>, which lets the
/// organizer route each link to the right person; the assignment itself is in the opaque
/// <c>d=</c> blob.
/// </summary>
/// <param name="options">The reveal-link options carrying the base URL.</param>
/// <param name="codec">The codec used to encode the payload.</param>
public sealed class RevealLinkBuilder(RevealLinkOptions options, RevealLinkCodec codec)
{
    /// <summary>The reveal route path appended to the base URL.</summary>
    public const string RevealRoute = "/r";

    /// <summary>
    /// The recommended maximum reveal-link length, in characters. Links stay well within common
    /// browser/messenger URL limits below this budget (FEAT-001 aims for ≤ ~1.5–2 KB). Links longer
    /// than this still work but the organizer is warned, as some channels may truncate them.
    /// </summary>
    public const int MaxRecommendedUrlLength = 2000;

    /// <summary>
    /// Indicates whether a reveal link exceeds the <see cref="MaxRecommendedUrlLength"/> budget and
    /// should be flagged to the organizer.
    /// </summary>
    /// <param name="url">The reveal link URL to check.</param>
    /// <returns><see langword="true"/> when the link is longer than recommended.</returns>
    public static bool ExceedsRecommendedLength(string url) =>
        (url ?? string.Empty).Length > MaxRecommendedUrlLength;

    /// <summary>Builds a reveal link for one giver.</summary>
    /// <param name="forLabel">The readable owner label (the giver's name).</param>
    /// <param name="giverMemberId">The giver's member id, carried on the DTO so a caller can attach the reveal token (ADR-0068).</param>
    /// <param name="payload">The payload to encode into the fragment.</param>
    /// <returns>A <see cref="RevealLinkDto"/> with the label, giver id and full URL.</returns>
    public RevealLinkDto Build(string forLabel, Guid giverMemberId, RevealPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var blob = codec.Encode(payload);
        var baseUrl = (options.BaseUrl ?? string.Empty).TrimEnd('/');
        var url = $"{baseUrl}{RevealRoute}#for={Uri.EscapeDataString(forLabel)}&v={payload.Version}&d={blob}";
        return new RevealLinkDto(forLabel, giverMemberId, url);
    }

    /// <summary>Parses a reveal-link fragment into its readable label and opaque blob.</summary>
    /// <param name="fragment">The fragment string (with or without a leading <c>#</c>).</param>
    /// <returns>The decoded <c>for</c> label and the <c>d</c> blob.</returns>
    /// <exception cref="FormatException">Thrown when the fragment lacks the required tokens.</exception>
    public static (string Label, string Blob) ParseFragment(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
        {
            throw new FormatException("The reveal link has no fragment.");
        }

        var trimmed = fragment.StartsWith('#') ? fragment[1..] : fragment;

        string? label = null;
        string? blob = null;
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var key = part[..separator];
            var value = part[(separator + 1)..];
            switch (key)
            {
                case "for":
                    label = Uri.UnescapeDataString(value);
                    break;
                case "d":
                    blob = value;
                    break;
                default:
                    break;
            }
        }

        if (blob is null)
        {
            throw new FormatException("The reveal link is missing its payload.");
        }

        return (label ?? string.Empty, blob);
    }

    /// <summary>
    /// Extracts the optional reveal token (<c>rt=</c>) from a reveal-link fragment (ADR-0068), or
    /// <see langword="null"/> when it is absent — a local-only link carries only the <c>d=</c> snapshot.
    /// </summary>
    /// <param name="fragment">The fragment string (with or without a leading <c>#</c>).</param>
    /// <returns>The reveal token, or <see langword="null"/>.</returns>
    public static string? ParseRevealToken(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
        {
            return null;
        }

        var trimmed = fragment.StartsWith('#') ? fragment[1..] : fragment;
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0 && part[..separator] == "rt")
            {
                return Uri.UnescapeDataString(part[(separator + 1)..]);
            }
        }

        return null;
    }
}
