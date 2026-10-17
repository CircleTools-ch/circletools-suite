using CircleTools.Ui.Shared;
using MudBlazor;
using MudBlazor.Utilities;

namespace CircleTools.Ui.Shared.Test;

/// <summary>
/// Every colour pair of the suite theme passes WCAG AA (4.5:1) in light and dark (Q33, 2026-10-16 device
/// test: white text on the dark-mode primary buttons was hard to read).
/// </summary>
public sealed class ThemeContrastTests
{
    private const double AA = 4.5;

    public static TheoryData<string> Palettes => new() { "light", "dark" };

    [Theory]
    [MemberData(nameof(Palettes))]
    public void FilledButtonLabels_AreReadable(string mode)
    {
        var p = PaletteFor(mode);

        AssertReadable(p.Primary, p.PrimaryContrastText, "primary button");
        AssertReadable(p.Secondary, p.SecondaryContrastText, "secondary button");
        AssertReadable(p.Success, p.SuccessContrastText, "success chip");
        AssertReadable(p.Warning, p.WarningContrastText, "warning chip");
        AssertReadable(p.Error, p.ErrorContrastText, "delete button");
        AssertReadable(p.Info, p.InfoContrastText, "info chip");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void ColouredAndSecondaryText_IsReadableOnTheSurfaces(string mode)
    {
        var p = PaletteFor(mode);

        foreach (var (surface, name) in new[] { (p.Surface, "surface"), (p.Background, "background") })
        {
            AssertReadable(p.TextPrimary, surface, $"text on {name}");
            AssertReadable(p.TextSecondary, surface, $"hint text on {name}");
            AssertReadable(p.Primary, surface, $"link on {name}");
            AssertReadable(p.Success, surface, $"success text on {name}");
            AssertReadable(p.Warning, surface, $"warning text on {name}");
            AssertReadable(p.Error, surface, $"error text on {name}");
            AssertReadable(p.Info, surface, $"info text on {name}");
        }
    }

    private static Palette PaletteFor(string mode) =>
        mode == "dark" ? CircleToolsTheme.Instance.PaletteDark : CircleToolsTheme.Instance.PaletteLight;

    private static void AssertReadable(MudColor foreground, MudColor background, string what)
    {
        var ratio = Ratio(foreground, background);
        Assert.True(ratio >= AA, $"{what}: {foreground} on {background} is {ratio:0.00}:1, below {AA}:1");
    }

    private static double Ratio(MudColor a, MudColor b)
    {
        var (light, dark) = (Luminance(a), Luminance(b)) is var (x, y) && x > y ? (x, y) : (y, x);
        return (light + 0.05) / (dark + 0.05);
    }

    private static double Luminance(MudColor c)
    {
        static double Channel(byte value)
        {
            var v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }
}
