using CircleTools.SecretSanta.LocalStore;
using CircleTools.SecretSanta.Ui;

namespace CircleTools.SecretSanta.Ui.Test;

/// <summary>
/// Unit tests for <see cref="SignInImportPlan"/> (R11, 2026-10-20 device test): a group shared by link while
/// signed out stays shared when imported, and a group only joined by link is never offered.
/// </summary>
public sealed class SignInImportPlanTests
{
    private static ClaimableGroup Group(string name, StorageMode mode, bool joinOnly = false) =>
        new(Guid.NewGuid(), name, 4, HasCurrentDraw: false, IsAccountFree: true, mode, IsJoinOnly: joinOnly);

    [Fact]
    public void ToAsk_LeavesOutJoinedGroupsAndGroupsKeptLocal()
    {
        var local = Group("Family", StorageMode.OnThisDevice);
        var kept = Group("Kept", StorageMode.OnThisDevice);
        var shared = Group("Shared", StorageMode.SharedByLink);
        var joined = Group("Joined", StorageMode.SharedByLink, joinOnly: true);

        var asked = SignInImportPlan.ToAsk([local, kept, shared, joined], new HashSet<Guid> { kept.Id });

        Assert.Equal([local, shared], asked);
    }

    [Fact]
    public void Split_SendsLocalGroupsToTheAccountStore_AndSharedGroupsToTheManagementLinkClaim()
    {
        var local = Group("Family", StorageMode.OnThisDevice);
        var shared = Group("Shared", StorageMode.SharedByLink);
        var unticked = Group("Other", StorageMode.OnThisDevice);

        var (toAccountStore, sharedToClaim) = SignInImportPlan.Split([local, shared, unticked], [local.Id, shared.Id]);

        Assert.Equal([local.Id], toAccountStore);
        Assert.Equal([shared.Id], sharedToClaim);
    }
}
