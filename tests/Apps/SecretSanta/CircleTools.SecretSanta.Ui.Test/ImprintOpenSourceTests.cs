// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Shared;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// About &amp; legal names the license and, once the repository is public, links its source (ADR-0099).
/// </summary>
public sealed class ImprintOpenSourceTests : IAsyncDisposable
{
    private readonly Bunit.BunitContext _ctx = new();

    public ImprintOpenSourceTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton<IStringLocalizer<SharedStrings>>(new PassThroughStringLocalizer<SharedStrings>());
    }

    [Fact]
    public void ShowsTheOpenSourceSection_WithALinkToTheLicenseInANewTab()
    {
        var cut = _ctx.Render<LegalImprintContent>();

        Assert.Contains("Legal_Imprint_OpenSourceHeading", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Legal_Imprint_OpenSourceBody", cut.Markup, StringComparison.Ordinal);

        var license = cut.Find("[data-testid=imprint-license-link]");
        Assert.Equal(OpenSource.LicenseUrl, license.GetAttribute("href"));
        Assert.Equal("_blank", license.GetAttribute("target"));
        Assert.Contains("noopener", license.GetAttribute("rel"), StringComparison.Ordinal);
    }

    [Fact]
    public void LinksTheSourceCode_OnlyOnceTheRepositoryIsPublic()
    {
        var cut = _ctx.Render<LegalImprintContent>();

        var source = cut.FindAll("[data-testid=imprint-source-link]");

        if (OpenSource.IsSourceCodePublic)
        {
            Assert.Equal(OpenSource.SourceCodeUrl, Assert.Single(source).GetAttribute("href"));
        }
        else
        {
            Assert.Empty(source);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}
