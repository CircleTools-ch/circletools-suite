using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CircleTools.Architecture.Test;

/// <summary>
/// Fitness functions for the Circles context (FEAT-003): its core is a real domain and must stay
/// free of persistence and web concerns, and it must not reach into the identity model or be
/// reached into by Secret-Santa.
/// </summary>
public sealed class CirclesLayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(CircleTools.Circles.Contracts.CreateCircleRequest).Assembly,
            typeof(CircleTools.Circles.Core.Domain.Circle).Assembly,
            typeof(CircleTools.Circles.Repository.CirclesDbContext).Assembly,
            typeof(CircleTools.SecretSanta.Core.Domain.Group).Assembly,
            typeof(CircleTools.Identity.CircleToolsUser).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _efCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.EntityFrameworkCore").As("EF Core");

    private static readonly IObjectProvider<IType> _npgsql =
        Types().That().ResideInNamespaceMatching("^Npgsql").As("Npgsql");

    private static readonly IObjectProvider<IType> _aspNetCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore").As("ASP.NET Core");

    private static readonly IObjectProvider<IType> _identity =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Identity").As("CircleTools.Identity");

    private static readonly IObjectProvider<IType> _circles =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Circles").As("Circles");

    private static readonly IObjectProvider<IType> _circlesCore =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Circles\\.Core").As("Circles.Core");

    private static readonly IObjectProvider<IType> _circlesContracts =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Circles\\.Contracts").As("Circles.Contracts");

    private static readonly IObjectProvider<IType> _circlesRepository =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Circles\\.Repository").As("Circles.Repository");

    private static readonly IObjectProvider<IType> _secretSanta =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.SecretSanta").As("Secret-Santa");

    [Fact]
    public void Core_DoesNotDependOnInfrastructureOrWeb() =>
        Types().That().Are(_circlesCore)
            .Should().NotDependOnAny(_efCore)
            .AndShould().NotDependOnAny(_npgsql)
            .AndShould().NotDependOnAny(_aspNetCore)
            .Check(_architecture);

    [Fact]
    public void Contracts_DoesNotDependOnInfrastructureOrWeb() =>
        Types().That().Are(_circlesContracts)
            .Should().NotDependOnAny(_efCore)
            .AndShould().NotDependOnAny(_npgsql)
            .AndShould().NotDependOnAny(_aspNetCore)
            .Check(_architecture);

    /// <summary>
    /// The persistence adapter is allowed EF Core and Npgsql — that is what it is for — but not the
    /// web stack. A repository that can see ASP.NET Core is one <c>HttpContext</c> away from
    /// deciding authorization in a query, which is exactly where NFR-006 must not end up.
    /// </summary>
    [Fact]
    public void Repository_DoesNotDependOnWeb() =>
        Types().That().Are(_circlesRepository)
            .Should().NotDependOnAny(_aspNetCore)
            .Check(_architecture);

    /// <summary>
    /// Circles must not reference <c>CircleTools.Identity</c> (arc42 §5.3, FEAT-003).
    /// </summary>
    /// <remarks>
    /// A membership holds a user identifier as a <em>value</em>, not a foreign key, and an account
    /// that no longer resolves is a case the context tolerates. Everything Circles needs to know
    /// about an account goes through <c>IAccountDirectory</c>, which the API host implements —
    /// exactly one crossing point instead of a dependency.
    /// <para>
    /// This is the rule the persistence adapter is most likely to break, since it is the layer that
    /// could add a foreign key to <c>AspNetUsers</c> and get a referential guarantee for free. It
    /// would cost the ability to keep a circle intact when a member deletes their account.
    /// </para>
    /// </remarks>
    [Fact]
    public void Circles_DoesNotDependOnTheIdentityModel() =>
        Types().That().Are(_circles)
            .Should().NotDependOnAny(_identity)
            .Check(_architecture);

    /// <summary>
    /// Secret-Santa must not reference Circles. Creating a group <em>from</em> a circle is M4, and
    /// when it lands it copies people at a point in time rather than reading a live circle — so the
    /// game keeps working with no account and no circle at all (NFR-001).
    /// </summary>
    [Fact]
    public void SecretSanta_DoesNotDependOnCircles() =>
        Types().That().Are(_secretSanta)
            .Should().NotDependOnAny(_circles)
            .Check(_architecture);
}
