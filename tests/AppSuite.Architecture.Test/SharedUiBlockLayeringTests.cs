using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace AppSuite.Architecture.Test;

/// <summary>
/// Fitness functions for the shared UI building blocks (ADR-0025, ADR-0028).
/// </summary>
/// <remarks>
/// These blocks exist so a browser head and a MAUI head can share feature code and disagree only
/// about the platform. That only holds if the separation is real: the moment a seam references JS
/// interop, or a Razor component references a browser adapter, the MAUI head inherits a dependency
/// it cannot satisfy — and it finds out at the far end of a workload install, not here.
/// </remarks>
public sealed class SharedUiBlockLayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(AppSuite.Ui.Abstractions.IKeyValueStore).Assembly,
            typeof(AppSuite.Ui.Web.BrowserKeyValueStore).Assembly,
            typeof(AppSuite.Ui.Shared.SharedStrings).Assembly,
            typeof(AppSuite.Localization.AppCultures).Assembly,
            typeof(AppSuite.ApiClient.ApiResult).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _jsInterop =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.JSInterop").As("JS interop");

    private static readonly IObjectProvider<IType> _webAssembly =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore\\.Components\\.WebAssembly")
            .As("Blazor WebAssembly");

    private static readonly IObjectProvider<IType> _apiClient =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.ApiClient").As("ApiClient");

    private static readonly IObjectProvider<IType> _uiAbstractions =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.Ui\\.Abstractions").As("Ui.Abstractions");

    private static readonly IObjectProvider<IType> _uiWeb =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.Ui\\.Web").As("Ui.Web");

    private static readonly IObjectProvider<IType> _uiShared =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.Ui\\.Shared").As("Ui.Shared");

    private static readonly IObjectProvider<IType> _localization =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.Localization").As("Localization");

    private static readonly IObjectProvider<IType> _apps =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.(Main|SecretSanta)").As("app code");

    /// <summary>
    /// The seams reference nothing platform-shaped. This is ADR-0025's first rule, and the one that
    /// makes every other block's dependency set statable.
    /// </summary>
    [Fact]
    public void UiAbstractions_AreJustSeams() =>
        Types().That().Are(_uiAbstractions)
            .Should().NotDependOnAny(_jsInterop)
            .AndShould().NotDependOnAny(_webAssembly)
            .AndShould().NotDependOnAny(_apiClient)
            .AndShould().NotDependOnAny(_uiWeb)
            .AndShould().NotDependOnAny(_uiShared)
            .AndShould().NotDependOnAny(_localization)
            .Check(_architecture);

    /// <summary>
    /// <c>Ui.Shared</c> holds Razor components that every head renders, so it must not reach for the
    /// browser adapters. This is the whole reason <c>Ui.Web</c> is a separate block rather than a
    /// folder inside this one (ADR-0028): a MAUI head references <c>Ui.Shared</c>, and would
    /// otherwise inherit <c>localStorage</c> along with the language picker.
    /// </summary>
    [Fact]
    public void UiShared_DoesNotDependOnTheBrowserAdapters() =>
        Types().That().Are(_uiShared)
            .Should().NotDependOnAny(_uiWeb)
            .AndShould().NotDependOnAny(_webAssembly)
            .Check(_architecture);

    /// <summary>
    /// <c>Ui.Web</c> implements seams and nothing else: no components, no API knowledge, and above
    /// all no app. Two apps register it, and a block that knew about either would stop being
    /// registrable by the other.
    /// </summary>
    [Fact]
    public void UiWeb_ImplementsSeamsAndKnowsNoApp() =>
        Types().That().Are(_uiWeb)
            .Should().NotDependOnAny(_apps)
            .AndShould().NotDependOnAny(_apiClient)
            .AndShould().NotDependOnAny(_uiShared)
            .AndShould().NotDependOnAny(_webAssembly)
            .Check(_architecture);

    /// <summary>
    /// Localization is host-neutral: it reads the chosen language through the
    /// <c>IKeyValueStore</c> seam, never through a browser API of its own.
    /// </summary>
    [Fact]
    public void Localization_StaysPlatformNeutral() =>
        Types().That().Are(_localization)
            .Should().NotDependOnAny(_jsInterop)
            .AndShould().NotDependOnAny(_webAssembly)
            .AndShould().NotDependOnAny(_uiWeb)
            .AndShould().NotDependOnAny(_apps)
            .Check(_architecture);
}
