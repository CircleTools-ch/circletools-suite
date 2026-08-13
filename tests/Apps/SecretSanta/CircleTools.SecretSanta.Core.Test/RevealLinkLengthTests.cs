using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Reveal;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>
/// Tests for the reveal-link URL-length budget (FEAT-001 open question — resolved with a
/// <see cref="RevealLinkBuilder.MaxRecommendedUrlLength"/> threshold the organizer is warned about).
/// </summary>
public sealed class RevealLinkLengthTests
{
    private static RevealLinkBuilder CreateBuilder() =>
        new(new RevealLinkOptions("https://secretsanta.example"), new RevealLinkCodec());

    private static RevealPayload PayloadWithWishlist(IReadOnlyList<string> wishlist) =>
        new(RevealPayload.CurrentVersion, "Recipient", wishlist, 25m, "CHF", null, null, null);

    [Fact]
    public void ModestWishlist_ProducesLinkWithinRecommendedLength()
    {
        var link = CreateBuilder().Build("Alice", PayloadWithWishlist(["Scarf", "Book", "Tea"]));

        Assert.False(RevealLinkBuilder.ExceedsRecommendedLength(link.Url));
        Assert.True(link.Url.Length <= RevealLinkBuilder.MaxRecommendedUrlLength);
    }

    [Fact]
    public void OversizedWishlist_ProducesLinkFlaggedAsTooLong()
    {
        // High-entropy entries defeat compression, so the encoded link exceeds the URL budget.
        var wishlist = Enumerable.Range(0, 200)
            .Select(_ => Guid.NewGuid().ToString("N"))
            .ToList();

        var link = CreateBuilder().Build("Alice", PayloadWithWishlist(wishlist));

        Assert.True(RevealLinkBuilder.ExceedsRecommendedLength(link.Url));
        Assert.True(link.Url.Length > RevealLinkBuilder.MaxRecommendedUrlLength);
    }
}
