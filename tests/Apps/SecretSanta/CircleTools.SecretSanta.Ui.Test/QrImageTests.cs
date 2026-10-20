using CircleTools.SecretSanta.Ui.Shared;
using Bunit;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Component tests for <see cref="QrImage"/> (MNF-5): it encodes a value into a self-contained PNG data
/// URI rendered entirely client-side (no JS interop, no platform seam), and renders nothing for an empty
/// value so an empty src is never emitted.
/// </summary>
public sealed class QrImageTests : IAsyncDisposable
{
    private readonly Bunit.BunitContext _ctx = new();

    [Fact]
    public void RendersAPngDataUri_ForAValue()
    {
        var cut = _ctx.Render<QrImage>(p => p
            .Add(c => c.Value, "https://secretsanta.example/r#for=Al&v=1&d=blob")
            .Add(c => c.Alt, "Al"));

        var img = cut.Find("img");
        Assert.StartsWith("data:image/png;base64,", img.GetAttribute("src"));
        Assert.Equal("Al", img.GetAttribute("alt"));
    }

    [Fact]
    public void RendersNothing_ForAnEmptyValue()
    {
        var cut = _ctx.Render<QrImage>(p => p.Add(c => c.Value, string.Empty));

        Assert.Empty(cut.FindAll("img"));
    }

    [Fact]
    public void ReEncodes_WhenTheValueChanges()
    {
        var cut = _ctx.Render<QrImage>(p => p.Add(c => c.Value, "first"));
        var first = cut.Find("img").GetAttribute("src");

        cut.Render(p => p.Add(c => c.Value, "second-different-value"));
        var second = cut.Find("img").GetAttribute("src");

        Assert.NotEqual(first, second);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}
