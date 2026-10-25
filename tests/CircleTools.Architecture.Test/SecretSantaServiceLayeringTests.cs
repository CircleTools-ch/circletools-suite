using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CircleTools.Architecture.Test;

/// <summary>
/// Fitness functions for the Secret-Santa server module (ADR-0086): its core holds the access rules and views with
/// no I/O, and no part of the module depends on the platform host — the host adapts the platform to the module's
/// ports, never the other way round.
/// </summary>
public sealed class SecretSantaServiceLayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(CircleTools.SecretSanta.Service.Core.Groups.GroupService).Assembly,
            typeof(CircleTools.SecretSanta.Service.Repository.GroupStore).Assembly,
            typeof(CircleTools.SecretSanta.Service.GroupEndpoints).Assembly,
            typeof(CircleTools.Main.Service.Core.Domain.IAccountDeletionCleanup).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _serviceCore =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.SecretSanta\\.Service\\.Core").As("Secret-Santa service core");

    private static readonly IObjectProvider<IType> _module =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.SecretSanta\\.Service").As("Secret-Santa server module");

    private static readonly IObjectProvider<IType> _platform =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Main\\.").As("Main platform");

    private static readonly IObjectProvider<IType> _efCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore").As("EF Core");

    private static readonly IObjectProvider<IType> _aspNetCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore").As("ASP.NET Core");

    [Fact]
    public void ServiceCore_HasNoIo() =>
        Types().That().Are(_serviceCore)
            .Should().NotDependOnAny(_efCore)
            .AndShould().NotDependOnAny(_aspNetCore)
            .Check(_architecture);

    [Fact]
    public void TheModule_DoesNotDependOnThePlatformHost() =>
        Types().That().Are(_module)
            .Should().NotDependOnAny(_platform)
            .Check(_architecture);
}
