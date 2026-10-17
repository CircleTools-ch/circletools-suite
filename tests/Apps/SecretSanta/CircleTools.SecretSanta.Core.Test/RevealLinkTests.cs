using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Reveal;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>Tests for the reveal-link codec and builder.</summary>
public sealed class RevealLinkTests
{
    private static RevealPayload SamplePayload() => new(
        RevealPayload.CurrentVersion,
        "Alice",
        ["A warm scarf", "Book: The Pragmatic Programmer"],
        30m,
        "CHF",
        "2026-12-24T18:00:00.0000000+00:00",
        "Grandma's house",
        "Bring a dessert",
        BudgetMin: 10m,
        GiftNote: "No nuts, please — severe allergy");

    private static void AssertPayloadEqual(RevealPayload expected, RevealPayload actual)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.RecipientName, actual.RecipientName);
        Assert.Equal(expected.Wishlist, actual.Wishlist);
        Assert.Equal(expected.BudgetAmount, actual.BudgetAmount);
        Assert.Equal(expected.BudgetCurrency, actual.BudgetCurrency);
        Assert.Equal(expected.BudgetMin, actual.BudgetMin);
        Assert.Equal(expected.EventDate, actual.EventDate);
        Assert.Equal(expected.EventLocation, actual.EventLocation);
        Assert.Equal(expected.EventNotes, actual.EventNotes);
        Assert.Equal(expected.GiftNote, actual.GiftNote);
    }

    [Fact]
    public void Codec_RoundTrips()
    {
        var codec = new RevealLinkCodec();
        var payload = SamplePayload();

        var blob = codec.Encode(payload);
        var decoded = codec.Decode(blob);

        AssertPayloadEqual(payload, decoded);
        Assert.Equal(RevealPayload.CurrentVersion, decoded.Version);
    }

    [Fact]
    public void Codec_ProducesUrlSafeOutput()
    {
        var codec = new RevealLinkCodec();

        var blob = codec.Encode(SamplePayload());

        Assert.DoesNotContain('+', blob);
        Assert.DoesNotContain('/', blob);
        Assert.DoesNotContain('=', blob);
    }

    [Fact]
    public void Codec_OmitsBudgetMin_WhenAbsent_SoMaxOnlyLinksAreUnchanged()
    {
        // FR-036: the minimum is additive and optional. A max-only payload must not carry the "bm" key,
        // so links written before the minimum existed (and every max-only link) stay byte-for-byte the same.
        var codec = new RevealLinkCodec();
        var maxOnly = SamplePayload() with { BudgetMin = null };

        var decoded = codec.Decode(codec.Encode(maxOnly));

        Assert.Null(decoded.BudgetMin);
        Assert.Equal(30m, decoded.BudgetAmount);
    }

    [Fact]
    public void Codec_OmitsGiftNote_WhenAbsent()
    {
        // FR-037: the gift note is additive and optional; a payload without one must not carry the "gn" key.
        var codec = new RevealLinkCodec();
        var noNote = SamplePayload() with { GiftNote = null };

        var decoded = codec.Decode(codec.Encode(noNote));

        Assert.Null(decoded.GiftNote);
    }

    [Fact]
    public void Codec_Decode_ThrowsFormatExceptionForMalformedBlob()
    {
        var codec = new RevealLinkCodec();

        Assert.Throws<FormatException>(() => codec.Decode("not-a-valid-blob!!!"));
    }

    [Fact]
    public void Builder_BuildsRevealUrl_AndParserRoundTrips()
    {
        var codec = new RevealLinkCodec();
        var builder = new RevealLinkBuilder(new RevealLinkOptions("https://example.org"), codec);
        var payload = SamplePayload();

        var link = builder.Build("Bob & Carol", Guid.NewGuid(), payload);

        Assert.Equal("Bob & Carol", link.ForLabel);
        Assert.StartsWith("https://example.org/r#for=", link.Url);
        Assert.Contains("&d=", link.Url);
        Assert.Contains("&v=1", link.Url);

        var fragment = new Uri(link.Url).Fragment;
        var (label, blob) = RevealLinkBuilder.ParseFragment(fragment);

        Assert.Equal("Bob & Carol", label);
        AssertPayloadEqual(payload, codec.Decode(blob));
    }

    [Fact]
    public void ParseFragment_ThrowsWhenPayloadMissing()
    {
        Assert.Throws<FormatException>(() => RevealLinkBuilder.ParseFragment("#for=Bob"));
    }

    [Fact]
    public void ShortUrl_KeepsOnlyTheNameAndTheToken_InTheFragment()
    {
        // ADR-0082: no d= snapshot; the token stays in the fragment, never in the path or query.
        var shortUrl = RevealLinkBuilder.ShortUrl("https://santa.example/r#for=Al&v=3&d=LONGBLOB&rt=old", "Anna Bé", "tok_123-x");

        Assert.Equal("https://santa.example/r#for=Anna%20B%C3%A9&rt=tok_123-x", shortUrl);
        var fragment = shortUrl[shortUrl.IndexOf('#')..];
        Assert.True(RevealLinkBuilder.IsShort(fragment));
        Assert.Equal("Anna Bé", RevealLinkBuilder.ParseLabel(fragment));
        Assert.Equal("tok_123-x", RevealLinkBuilder.ParseRevealToken(fragment));
        Assert.False(RevealLinkBuilder.IsShort("#for=Al&v=3&d=BLOB&rt=tok"));
    }
}
