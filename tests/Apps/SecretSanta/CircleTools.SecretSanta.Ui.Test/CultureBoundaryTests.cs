using Bunit;
using CircleTools.SecretSanta.Ui.Test.TestSupport;
using CircleTools.Ui.Abstractions;
using CircleTools.Ui.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// BTP-7: <c>&lt;html lang&gt;</c> names the language on screen, from the start and after every switch, so
/// a screen reader does not read a German page with an English voice.
/// </summary>
public sealed class CultureBoundaryTests : IAsyncDisposable
{
    private const string Module = "./_content/CircleTools.Ui.Shared/js/documentLanguage.js";

    private readonly Bunit.BunitContext _ctx = new();
    private readonly RecordingCultureProvider _culture = new("de-CH");
    private readonly BunitJSModuleInterop _module;

    public CultureBoundaryTests()
    {
        _ctx.Services.AddSingleton<ICultureProvider>(_culture);
        _module = _ctx.JSInterop.SetupModule(Module);
        _module.SetupVoid("setLanguage", _ => true).SetVoidResult();
    }

    private IRenderedComponent<CultureBoundary> Render() =>
        _ctx.Render<CultureBoundary>(parameters => parameters.Add(
            boundary => boundary.ChildContent,
            (RenderFragment<string>)(name => builder => builder.AddContent(0, name))));

    private List<string?> LanguagesSet() =>
        [.. _module.Invocations["setLanguage"].Select(call => (string?)call.Arguments[0])];

    [Fact]
    public void NamesTheLanguageOnStart()
    {
        var cut = Render();

        cut.WaitForAssertion(() => Assert.Equal(["de-CH"], LanguagesSet()));
    }

    [Fact]
    public async Task FollowsASwitch()
    {
        var cut = Render();
        cut.WaitForAssertion(() => Assert.Single(LanguagesSet()));

        await cut.InvokeAsync(async () => await _culture.SetCultureAsync("fr-CH"));

        cut.WaitForAssertion(() => Assert.Equal(["de-CH", "fr-CH"], LanguagesSet()));
    }

    [Fact]
    public async Task DoesNotCallAgain_WhileTheLanguageStaysTheSame()
    {
        var cut = Render();
        cut.WaitForAssertion(() => Assert.Single(LanguagesSet()));

        await cut.InvokeAsync(() => cut.Render());

        Assert.Single(LanguagesSet());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}
