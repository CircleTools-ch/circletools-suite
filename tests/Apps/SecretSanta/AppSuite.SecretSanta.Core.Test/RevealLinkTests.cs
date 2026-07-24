using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Reveal;

namespace AppSuite.SecretSanta.Core.Test;

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
        "Bring a dessert");

    private static void AssertPayloadEqual(RevealPayload expected, RevealPayload actual)
    {
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.RecipientName, actual.RecipientName);
        Assert.Equal(expected.Wishlist, actual.Wishlist);
        Assert.Equal(expected.BudgetAmount, actual.BudgetAmount);
        Assert.Equal(expected.BudgetCurrency, actual.BudgetCurrency);
        Assert.Equal(expected.EventDate, actual.EventDate);
        Assert.Equal(expected.EventLocation, actual.EventLocation);
        Assert.Equal(expected.EventNotes, actual.EventNotes);
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

        var link = builder.Build("Bob & Carol", payload);

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
}
