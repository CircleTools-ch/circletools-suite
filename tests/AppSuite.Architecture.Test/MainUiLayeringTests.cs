using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace AppSuite.Architecture.Test;

/// <summary>
/// Fitness functions for the Main app's client (FEAT-002).
/// </summary>
/// <remarks>
/// The client is an API consumer and nothing else. Reaching for <c>AppSuite.Identity</c> or the
/// server's <c>Service.Core</c> would compile and even work, and would quietly move a security
/// decision into a place the browser can read — token issuance and password policy belong to the
/// API alone (ADR-0021). Persistence is the same story from the other direction: the client has no
/// database, and a reference to EF Core would mean someone thought it did.
/// </remarks>
public sealed class MainUiLayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(AppSuite.Main.Ui.AppStrings).Assembly,
            typeof(AppSuite.ApiClient.ApiResult).Assembly,
            typeof(AppSuite.Ui.Abstractions.IKeyValueStore).Assembly,
            typeof(AppSuite.Localization.AppCultures).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _efCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore").As("EF Core");

    private static readonly IObjectProvider<IType> _npgsql =
        Types().That().ResideInNamespaceMatching("^Npgsql").As("Npgsql");

    private static readonly IObjectProvider<IType> _identity =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.Identity").As("AppSuite.Identity");

    private static readonly IObjectProvider<IType> _serviceCore =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.Main\\.Service").As("Main.Service");

    private static readonly IObjectProvider<IType> _jsInterop =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.JSInterop").As("JS interop");

    private static readonly IObjectProvider<IType> _webAssembly =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore\\.Components\\.WebAssembly")
            .As("Blazor WebAssembly");

    private static readonly IObjectProvider<IType> _mainUi =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.Main\\.Ui").As("Main.Ui");

    private static readonly IObjectProvider<IType> _apiClient =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.ApiClient").As("ApiClient");

    private static readonly IObjectProvider<IType> _uiAbstractions =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.Ui\\.Abstractions").As("Ui.Abstractions");

    [Fact]
    public void MainUi_DoesNotDependOnTheServerOrItsPersistence() =>
        Types().That().Are(_mainUi)
            .Should().NotDependOnAny(_identity)
            .AndShould().NotDependOnAny(_serviceCore)
            .AndShould().NotDependOnAny(_efCore)
            .AndShould().NotDependOnAny(_npgsql)
            .Check(_architecture);

    [Fact]
    public void MainUi_DoesNotDependOnTheWebAssemblyHost() =>
        Types().That().Are(_mainUi)
            .Should().NotDependOnAny(_webAssembly)
            .Check(_architecture);

    [Fact]
    public void ApiClient_StaysPlatformNeutral() =>
        Types().That().Are(_apiClient)
            .Should().NotDependOnAny(_jsInterop)
            .AndShould().NotDependOnAny(_webAssembly)
            .Check(_architecture);

    [Fact]
    public void UiAbstractions_AreJustSeams() =>
        Types().That().Are(_uiAbstractions)
            .Should().NotDependOnAny(_jsInterop)
            .AndShould().NotDependOnAny(_webAssembly)
            .AndShould().NotDependOnAny(_apiClient)
            .Check(_architecture);
}
