using CircleTools.SecretSanta.LocalStore;

namespace CircleTools.Main.Service.Test.Sync.TestSupport;

/// <summary>
/// One signed-in account in the two-client harness (FEAT-004): its identity, its bearer token, and a
/// factory for the <see cref="SyncClient"/> devices that sync under it. Every device created here is a
/// genuinely separate store — its own <c>deviceId</c>, cursor and change tracker — so "the same user on
/// two devices" is modelled as two independent callers, never one shared tracker (TP-004).
/// </summary>
/// <param name="factory">The host the devices sync against.</param>
/// <param name="accountId">The account's identity, the only scope sync has.</param>
/// <param name="accessToken">The bearer token the account signed in with.</param>
public sealed class SyncTestAccount(TwoClientSyncFactory factory, Guid accountId, string accessToken)
{
    /// <summary>The account's identity — passed to every cycle as its only sync scope.</summary>
    public Guid AccountId => accountId;

    /// <summary>The bearer token the account signed in with.</summary>
    public string AccessToken => accessToken;

    /// <summary>
    /// Creates a new device for this account: a fresh local store and an authorized transport to the
    /// host. A device minted here has never synced, so its cursor is zero and its store is empty until
    /// it either creates a group or pulls one.
    /// </summary>
    /// <param name="decorate">
    /// An optional wrapper around the real transport — e.g. <see cref="InterruptingSyncApi"/> to model
    /// a cycle whose response is lost. Null for a plain device.
    /// </param>
    /// <returns>The new device.</returns>
    public SyncClient NewDevice(Func<ISyncApi, ISyncApi>? decorate = null)
    {
        ISyncApi api = new HttpSyncApi(factory.CreateAuthorizedClient(accessToken));
        if (decorate is not null)
        {
            api = decorate(api);
        }

        return new SyncClient(accountId, api);
    }
}
