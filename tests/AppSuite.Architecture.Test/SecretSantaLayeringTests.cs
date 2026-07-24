using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace AppSuite.Architecture.Test;

/// <summary>
/// Fitness functions for the Secret-Santa slice: the domain/application core and the contracts
/// must stay free of persistence and web concerns (EF Core, Npgsql, ASP.NET Core, JS interop) and
/// must not depend on the outer LocalStore or UI layers.
/// </summary>
public sealed class SecretSantaLayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(AppSuite.SecretSanta.Contracts.RevealPayload).Assembly,
            typeof(AppSuite.SecretSanta.Core.Domain.Group).Assembly,
            typeof(AppSuite.SecretSanta.LocalStore.GroupRepository).Assembly,
            typeof(AppSuite.SecretSanta.Ui.AppStrings).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _efCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore").As("EF Core");

    private static readonly IObjectProvider<IType> _npgsql =
        Types().That().ResideInNamespaceMatching("^Npgsql").As("Npgsql");

    private static readonly IObjectProvider<IType> _aspNetCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore").As("ASP.NET Core");

    private static readonly IObjectProvider<IType> _jsInterop =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.JSInterop").As("JS interop");

    private static readonly IObjectProvider<IType> _localStore =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.SecretSanta\\.LocalStore").As("LocalStore");

    private static readonly IObjectProvider<IType> _ui =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.SecretSanta\\.Ui").As("UI");

    private static readonly IObjectProvider<IType> _coreTypes =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.SecretSanta\\.Core").As("Secret-Santa Core");

    private static readonly IObjectProvider<IType> _contractsTypes =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.SecretSanta\\.Contracts").As("Secret-Santa Contracts");

    [Fact]
    public void Core_DoesNotDependOnInfrastructureOrWeb() =>
        Types().That().Are(_coreTypes)
            .Should().NotDependOnAny(_efCore)
            .AndShould().NotDependOnAny(_npgsql)
            .AndShould().NotDependOnAny(_aspNetCore)
            .AndShould().NotDependOnAny(_jsInterop)
            .Check(_architecture);

    [Fact]
    public void Contracts_DoesNotDependOnInfrastructureOrWeb() =>
        Types().That().Are(_contractsTypes)
            .Should().NotDependOnAny(_efCore)
            .AndShould().NotDependOnAny(_npgsql)
            .AndShould().NotDependOnAny(_aspNetCore)
            .AndShould().NotDependOnAny(_jsInterop)
            .Check(_architecture);

    [Fact]
    public void Core_DoesNotDependOnLocalStoreOrUi() =>
        Types().That().Are(_coreTypes)
            .Should().NotDependOnAny(_localStore)
            .AndShould().NotDependOnAny(_ui)
            .Check(_architecture);
}
