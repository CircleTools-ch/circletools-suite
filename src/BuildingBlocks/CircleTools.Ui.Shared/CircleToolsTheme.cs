using MudBlazor;

namespace CircleTools.Ui.Shared;

/// <summary>
/// The suite's single MudBlazor theme (ADR-0040): a deliberately restrained ("sobre") neutral palette
/// with one calm accent, in light and dark. The festive per-app brand colours (naming.md) live on the
/// app icon, splash and store assets — not on in-app surfaces, which stay neutral here.
/// </summary>
public static class CircleToolsTheme
{
    // The single interactive accent — a desaturated indigo/blue in the suite family. Lifted on dark
    // surfaces so it keeps contrast. Tunable; this is the agreed starting point.
    private const string Accent = "#5b7cc4";
    private const string AccentDark = "#8aa6e0";

    // A system-ui font stack: no web-font download, so font-src 'self' is satisfied and the look
    // stays native/light on each platform.
    private static readonly string[] FontStack =
    [
        "system-ui", "-apple-system", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"
    ];

    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Accent,
            Secondary = "#5b6270",
            AppbarBackground = "#ffffff",
            AppbarText = "#1f2430",
            Background = "#f6f7f9",
            BackgroundGray = "#eef0f3",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            DrawerText = "#1f2430",
            DrawerIcon = "#5b6270",
            TextPrimary = "#1f2430",
            TextSecondary = "#5b6270",
            ActionDefault = "#5b6270",
            Divider = "#e6e8ec",
            LinesDefault = "#e6e8ec",
        },
        PaletteDark = new PaletteDark
        {
            Primary = AccentDark,
            Secondary = "#9aa1ad",
            AppbarBackground = "#1a1d22",
            AppbarText = "#e7e9ee",
            Background = "#121417",
            BackgroundGray = "#0d0f12",
            Surface = "#1a1d22",
            DrawerBackground = "#1a1d22",
            DrawerText = "#e7e9ee",
            DrawerIcon = "#9aa1ad",
            TextPrimary = "#e7e9ee",
            TextSecondary = "#9aa1ad",
            ActionDefault = "#9aa1ad",
            Divider = "#2a2e35",
            LinesDefault = "#2a2e35",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = FontStack },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
        },
    };

    /// <summary>
    /// The suite theme with a per-app app-bar colour applied (ADR-0040 amendment). Every surface stays
    /// the neutral suite palette; only the app bar (background + text) takes the app's brand colour, so
    /// the apps are distinguishable without turning the whole UI festive. The app-bar colour is the same
    /// in light and dark on purpose — it is an identity cue, not a themed surface.
    /// </summary>
    public static MudTheme ForApp(AppBranding branding)
    {
        ArgumentNullException.ThrowIfNull(branding);

        // A per-palette copy that overrides only the app-bar colours; the shared instance is never
        // mutated. Light and dark keep their own subtype (MudThemeProvider swaps between them on the
        // dark-mode toggle), so each is copied onto a fresh instance of its own kind.
        return new MudTheme
        {
            PaletteLight = (PaletteLight)WithAppBar(new PaletteLight(), Instance.PaletteLight, branding),
            PaletteDark = (PaletteDark)WithAppBar(new PaletteDark(), Instance.PaletteDark, branding),
            Typography = Instance.Typography,
            LayoutProperties = Instance.LayoutProperties,
        };
    }

    // Copies every colour token from the suite palette onto the target, replacing only the app-bar pair.
    private static Palette WithAppBar(Palette target, Palette source, AppBranding branding)
    {
        target.Primary = source.Primary;
        target.Secondary = source.Secondary;
        target.AppbarBackground = branding.AppBarColor;
        target.AppbarText = branding.AppBarText;
        target.Background = source.Background;
        target.BackgroundGray = source.BackgroundGray;
        target.Surface = source.Surface;
        target.DrawerBackground = source.DrawerBackground;
        target.DrawerText = source.DrawerText;
        target.DrawerIcon = source.DrawerIcon;
        target.TextPrimary = source.TextPrimary;
        target.TextSecondary = source.TextSecondary;
        target.ActionDefault = source.ActionDefault;
        target.Divider = source.Divider;
        target.LinesDefault = source.LinesDefault;
        return target;
    }
}
