// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CircleTools.Architecture.Test;

/// <summary>
/// Fitness functions for the shared account/session UI block (ADR-0034).
/// </summary>
/// <remarks>
/// The block holds the account pages and the session/token plumbing every frontend shares. It is an
/// API consumer and nothing else: a reference to the server's <c>Main.Service</c>, to
/// <c>CircleTools.Identity</c>, or to EF Core would move a decision the API owns (token issuance,
/// password policy, persistence) into a place the browser can read. And it must stay host-neutral —
/// the moment it references Blazor WebAssembly or JS interop, a MAUI head inherits a dependency it
/// cannot satisfy, and finds out at the far end of a workload install rather than here. Its one app
/// edge is <c>CircleTools.Main.Contracts</c>, the backend's account contract, which every frontend
/// tracks regardless (the same edge <c>SecretSanta.LocalStore</c> already has for sync).
/// </remarks>
public sealed class UiAccountLayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(CircleTools.Ui.Account.AccountStrings).Assembly,
            typeof(CircleTools.ApiClient.ApiResult).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _uiAccount =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Ui\\.Account").As("Ui.Account");

    private static readonly IObjectProvider<IType> _efCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore").As("EF Core");

    private static readonly IObjectProvider<IType> _npgsql =
        Types().That().ResideInNamespaceMatching("^Npgsql").As("Npgsql");

    private static readonly IObjectProvider<IType> _identity =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Identity").As("CircleTools.Identity");

    private static readonly IObjectProvider<IType> _server =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Main\\.Service").As("Main.Service");

    private static readonly IObjectProvider<IType> _jsInterop =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.JSInterop").As("JS interop");

    private static readonly IObjectProvider<IType> _webAssembly =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore\\.Components\\.WebAssembly")
            .As("Blazor WebAssembly");

    [Fact]
    public void UiAccount_DoesNotDependOnTheServerOrItsPersistence() =>
        Types().That().Are(_uiAccount)
            .Should().NotDependOnAny(_identity)
            .AndShould().NotDependOnAny(_server)
            .AndShould().NotDependOnAny(_efCore)
            .AndShould().NotDependOnAny(_npgsql)
            .Check(_architecture);

    [Fact]
    public void UiAccount_StaysHostNeutral() =>
        Types().That().Are(_uiAccount)
            .Should().NotDependOnAny(_jsInterop)
            .AndShould().NotDependOnAny(_webAssembly)
            .Check(_architecture);
}
