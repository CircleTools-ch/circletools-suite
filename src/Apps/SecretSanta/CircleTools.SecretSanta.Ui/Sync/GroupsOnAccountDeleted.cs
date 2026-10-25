using CircleTools.SecretSanta.LocalStore.Online;
using CircleTools.Ui.Account;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Settles this device's groups when the signed-in account is deleted (FR-043, ADR-0086): organized groups become
/// device groups, groups taken part in leave the device. Without it they stayed on the device, hidden for good.
/// </summary>
/// <param name="online">The online-group sync, which owns the group records.</param>
public sealed class GroupsOnAccountDeleted(OnlineGroupSync online) : IAccountDeletedHandler
{
    /// <inheritdoc />
    public Task OnAccountDeletedAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        online.ForgetAccountAsync(accountId, cancellationToken);
}
