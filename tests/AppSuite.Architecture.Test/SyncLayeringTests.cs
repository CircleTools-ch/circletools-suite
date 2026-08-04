using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace AppSuite.Architecture.Test;

/// <summary>
/// Fitness functions for the M3 sync slice (FEAT-004): sync stays <em>behind the ports</em>. The
/// Platform sync surface stores app aggregates as opaque documents and must not reach into the
/// Secret-Santa domain, and the Secret-Santa core must not reach into the sync store or the web
/// stack. This is ADR-0016's "sync sits behind the ports" made a build-time guarantee.
/// </summary>
public sealed class SyncLayeringTests
{
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(AppSuite.Main.Contracts.SyncChange).Assembly,
            typeof(AppSuite.Main.Service.Core.Domain.ISyncStore).Assembly,
            typeof(AppSuite.Main.Service.Repository.SyncStore).Assembly,
            typeof(AppSuite.SecretSanta.Core.Domain.Group).Assembly,
            typeof(AppSuite.SecretSanta.Contracts.RevealPayload).Assembly)
        .Build();

    private static readonly IObjectProvider<IType> _platform =
        Types().That()
            .ResideInNamespaceMatching("^AppSuite\\.Main\\.Service\\.Repository|^AppSuite\\.Main\\.Contracts")
            .As("Platform sync");

    private static readonly IObjectProvider<IType> _secretSanta =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.SecretSanta").As("Secret-Santa");

    private static readonly IObjectProvider<IType> _secretSantaCore =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.SecretSanta\\.Core").As("Secret-Santa Core");

    private static readonly IObjectProvider<IType> _syncStore =
        Types().That().ResideInNamespaceMatching("^AppSuite\\.Main\\.Service\\.Repository").As("sync store");

    private static readonly IObjectProvider<IType> _aspNetCore =
        Types().That().ResideInNamespaceMatching("^Microsoft\\.AspNetCore").As("ASP.NET Core");

    /// <summary>
    /// The sync store and its wire contracts keep a group as a <c>jsonb</c> body they never
    /// deserialize (ADR-0033). If Platform took a dependency on the Secret-Santa schema, the opaque
    /// document would stop being opaque and the "one mechanism, any aggregate kind" design would leak.
    /// </summary>
    [Fact]
    public void Platform_DoesNotDependOnTheSecretSantaDomain() =>
        Types().That().Are(_platform)
            .Should().NotDependOnAny(_secretSanta)
            .Check(_architecture);

    /// <summary>
    /// The <see cref="AppSuite.SecretSanta.Core.Domain.Group"/> aggregate gains nothing from sync: the
    /// syncing behaviour lives in the client's LocalStore/ApiClient adapters (M3 slice 2), never in the
    /// core. The core must therefore see neither the server sync store nor the web stack.
    /// </summary>
    [Fact]
    public void SecretSantaCore_DependsOnNoSyncOrHttpType() =>
        Types().That().Are(_secretSantaCore)
            .Should().NotDependOnAny(_syncStore)
            .AndShould().NotDependOnAny(_aspNetCore)
            .Check(_architecture);
}
