using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>Links in wishlist lines are recognised wherever they are typed (ADR-0083, I8).</summary>
public sealed class WishlistItemTests
{
    [Fact]
    public void APlainLine_IsJustText()
    {
        Assert.Equal(new WishlistItem("Warm socks", null), WishlistItem.Parse("Warm socks"));
    }

    [Fact]
    public void ALinkAnywhere_IsTheLink_AndLeavesTheText()
    {
        var item = WishlistItem.Parse("The blue one https://shop.example/blue?c=1 please");

        Assert.Equal("https://shop.example/blue?c=1", item.Link!.ToString());
        Assert.Equal("The blue one please", item.Text);
    }

    [Fact]
    public void ALineThatIsOnlyALink_HasNoText()
    {
        var item = WishlistItem.Parse("https://shop.example/x.");

        Assert.Equal("https://shop.example/x", item.Link!.ToString());
        Assert.Equal(string.Empty, item.Text);
    }

    [Fact]
    public void TextBeforeALink_LosesTheDanglingSeparator()
    {
        Assert.Equal("Headphones", WishlistItem.Parse("Headphones - https://shop.example/h").Text);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://files.example/x")]
    [InlineData("mail me at mailto:a@b.c")]
    public void OnlyWebLinks_AreLinks(string line) =>
        Assert.Null(WishlistItem.Parse(line).Link);
}
