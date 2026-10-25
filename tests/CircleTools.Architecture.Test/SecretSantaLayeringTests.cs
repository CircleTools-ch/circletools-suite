using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace CircleTools.Architecture.Test;

/// <summary>
/// Fitness functions for the Secret-Santa slice: the domain/application core and the contracts
/// must stay free of persistence and web concerns (EF Core, Npgsql, ASP.NET Core, JS interop) and
/// must not depend on the outer LocalStore or UI layers.
/// </summary>
public sealed class SecretSantaLayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(CircleTools.SecretSanta.Contracts.RevealPayload).Assembly,
            typeof(CircleTools.SecretSanta.Core.Domain.Group).Assembly,
            typeof(CircleTools.SecretSanta.LocalStore.SyncService).Assembly,
            typeof(CircleTools.SecretSanta.Ui.AppStrings).Assembly,
            typeof(CircleTools.Identity.CircleToolsUser).Assembly,
            typeof(CircleTools.Circles.Contracts.InviteRequest).Assembly,
            typeof(CircleTools.Circles.Core.Domain.Circle).Assembly)
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
        Types().That().ResideInNamespaceMatching("^CircleTools\\.SecretSanta\\.LocalStore").As("LocalStore");

    private static readonly IObjectProvider<IType> _ui =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.SecretSanta\\.Ui").As("UI");

    private static readonly IObjectProvider<IType> _identity =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Identity").As("CircleTools.Identity");

    private static readonly IObjectProvider<IType> _circles =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.Circles").As("CircleTools.Circles");

    private static readonly IObjectProvider<IType> _secretSantaTypes =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.SecretSanta").As("Secret-Santa");

    private static readonly IObjectProvider<IType> _coreTypes =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.SecretSanta\\.Core").As("Secret-Santa Core");

    private static readonly IObjectProvider<IType> _contractsTypes =
        Types().That().ResideInNamespaceMatching("^CircleTools\\.SecretSanta\\.Contracts").As("Secret-Santa Contracts");

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

    /// <summary>
    /// No Secret-Santa project may reference <c>CircleTools.Identity</c> (ADR-0019, arc42 §5.3).
    /// </summary>
    /// <remarks>
    /// A <see href="../../docs/glossary/member.md">Member</see> carries an <em>optional</em>
    /// <c>UserId</c> and nothing more: the game must keep working with no account at all
    /// (NFR-001), and reaching into the identity model is how that quietly stops being true.
    /// The link lands in M4, so this rule exists before there is anything tempting to break it.
    /// </remarks>
    [Fact]
    public void SecretSanta_DoesNotDependOnTheIdentityModel() =>
        Types().That().Are(_secretSantaTypes)
            .Should().NotDependOnAny(_identity)
            .Check(_architecture);

    /// <summary>
    /// The Secret-Santa <strong>core</strong> (domain + application) must reference neither the Circles
    /// context nor its contracts (ADR-0019, FEAT-005 DoD).
    /// </summary>
    /// <remarks>
    /// Create-from-circle <em>reads</em> a circle, but only the client layer does: it translates circle
    /// DTOs into the Secret-Santa-owned <c>CirclePersonInput</c> so the core is told an account
    /// identifier and an opaque origin id, never a circle. The rule is scoped to Core on purpose — the
    /// client (LocalStore/Ui) legitimately references <c>Circles.Contracts</c> to read the picker — but
    /// if the core reached into the Circles model, a deleted circle could make a group fail to open
    /// (FR-022) and the snapshot semantics would quietly become a live link.
    /// </remarks>
    [Fact]
    public void SecretSantaCore_DoesNotDependOnTheCirclesContext() =>
        Types().That().Are(_coreTypes)
            .Should().NotDependOnAny(_circles)
            .Check(_architecture);

    [Fact]
    public void Core_DoesNotDependOnLocalStoreOrUi() =>
        Types().That().Are(_coreTypes)
            .Should().NotDependOnAny(_localStore)
            .AndShould().NotDependOnAny(_ui)
            .Check(_architecture);
}
