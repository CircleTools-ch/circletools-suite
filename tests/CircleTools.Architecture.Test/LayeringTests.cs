// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CircleTools.Architecture.Test;

/// <summary>
/// Fitness functions guarding the dependency direction: the domain and application core
/// must stay free of infrastructure and web concerns (EF Core, Npgsql, ASP.NET Core).
/// </summary>
public sealed class LayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(CircleTools.SharedKernel.Entity).Assembly,
            typeof(CircleTools.Main.Service.Core.Domain.IAccountDeletionGuard).Assembly,
            typeof(CircleTools.Main.Service.Repository.MainDbContext).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _efCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore").As("EF Core");

    private static readonly IObjectProvider<IType> _npgsql =
        Types().That().ResideInNamespaceMatching("^Npgsql").As("Npgsql");

    private static readonly IObjectProvider<IType> _aspNetCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore").As("ASP.NET Core");

    private static readonly IObjectProvider<IType> _serviceRepository =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Main\\.Service\\.Repository").As("Service.Repository");

    private static readonly IObjectProvider<IType> _sharedKernelTypes =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.SharedKernel").As("SharedKernel");

    private static readonly IObjectProvider<IType> _serviceCoreTypes =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Main\\.Service\\.Core").As("Service.Core");

    [Fact]
    public void SharedKernel_DoesNotDependOnInfrastructure() =>
        Types().That().Are(_sharedKernelTypes)
            .Should().NotDependOnAny(_efCore)
            .AndShould().NotDependOnAny(_npgsql)
            .AndShould().NotDependOnAny(_aspNetCore)
            .Check(_architecture);

    [Fact]
    public void ServiceCore_DoesNotDependOnInfrastructure() =>
        Types().That().Are(_serviceCoreTypes)
            .Should().NotDependOnAny(_efCore)
            .AndShould().NotDependOnAny(_npgsql)
            .AndShould().NotDependOnAny(_aspNetCore)
            .Check(_architecture);

    [Fact]
    public void ServiceCore_DoesNotDependOnServiceRepository() =>
        Types().That().Are(_serviceCoreTypes)
            .Should().NotDependOnAny(_serviceRepository)
            .Check(_architecture);
}
