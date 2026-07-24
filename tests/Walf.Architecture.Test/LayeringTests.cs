using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Walf.Architecture.Test;

/// <summary>
/// Fitness functions guarding the dependency direction: the domain and application core
/// must stay free of infrastructure and web concerns (EF Core, Npgsql, ASP.NET Core).
/// </summary>
public sealed class LayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Walf.SharedKernel.Entity).Assembly,
            typeof(Walf.Main.Service.Core.Domain.Heartbeat).Assembly,
            typeof(Walf.Main.Service.Repository.MainDbContext).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> EfCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore").As("EF Core");

    private static readonly IObjectProvider<IType> Npgsql =
        Types().That().ResideInNamespaceMatching("^Npgsql").As("Npgsql");

    private static readonly IObjectProvider<IType> AspNetCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore").As("ASP.NET Core");

    private static readonly IObjectProvider<IType> ServiceRepository =
        Types().That().ResideInNamespaceMatching("^Walf\\.Main\\.Service\\.Repository").As("Service.Repository");

    private static readonly IObjectProvider<IType> SharedKernelTypes =
        Types().That().ResideInNamespaceMatching("^Walf\\.SharedKernel").As("SharedKernel");

    private static readonly IObjectProvider<IType> ServiceCoreTypes =
        Types().That().ResideInNamespaceMatching("^Walf\\.Main\\.Service\\.Core").As("Service.Core");

    [Fact]
    public void SharedKernel_DoesNotDependOnInfrastructure() =>
        Types().That().Are(SharedKernelTypes)
            .Should().NotDependOnAny(EfCore)
            .AndShould().NotDependOnAny(Npgsql)
            .AndShould().NotDependOnAny(AspNetCore)
            .Check(Architecture);

    [Fact]
    public void ServiceCore_DoesNotDependOnInfrastructure() =>
        Types().That().Are(ServiceCoreTypes)
            .Should().NotDependOnAny(EfCore)
            .AndShould().NotDependOnAny(Npgsql)
            .AndShould().NotDependOnAny(AspNetCore)
            .Check(Architecture);

    [Fact]
    public void ServiceCore_DoesNotDependOnServiceRepository() =>
        Types().That().Are(ServiceCoreTypes)
            .Should().NotDependOnAny(ServiceRepository)
            .Check(Architecture);
}
