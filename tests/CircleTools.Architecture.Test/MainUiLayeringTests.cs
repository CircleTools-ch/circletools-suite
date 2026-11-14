// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CircleTools.Architecture.Test;

/// <summary>
/// Fitness functions for the Main app's client (FEAT-002).
/// </summary>
/// <remarks>
/// The client is an API consumer and nothing else. Reaching for <c>CircleTools.Identity</c> or the
/// server's <c>Service.Core</c> would compile and even work, and would quietly move a security
/// decision into a place the browser can read — token issuance and password policy belong to the
/// API alone (ADR-0021). Persistence is the same story from the other direction: the client has no
/// database, and a reference to EF Core would mean someone thought it did.
/// </remarks>
public sealed class MainUiLayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(CircleTools.Main.Ui.AppStrings).Assembly,
            typeof(CircleTools.ApiClient.ApiResult).Assembly,
            typeof(CircleTools.Ui.Abstractions.IKeyValueStore).Assembly,
            typeof(CircleTools.Localization.AppCultures).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _efCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore").As("EF Core");

    private static readonly IObjectProvider<IType> _npgsql =
        Types().That().ResideInNamespaceMatching("^Npgsql").As("Npgsql");

    private static readonly IObjectProvider<IType> _identity =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Identity").As("CircleTools.Identity");

    private static readonly IObjectProvider<IType> _serviceCore =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Main\\.Service").As("Main.Service");

    private static readonly IObjectProvider<IType> _jsInterop =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.JSInterop").As("JS interop");

    private static readonly IObjectProvider<IType> _webAssembly =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore\\.Components\\.WebAssembly")
            .As("Blazor WebAssembly");

    private static readonly IObjectProvider<IType> _mainUi =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Main\\.Ui").As("Main.Ui");

    private static readonly IObjectProvider<IType> _apiClient =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.ApiClient").As("ApiClient");

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
}
