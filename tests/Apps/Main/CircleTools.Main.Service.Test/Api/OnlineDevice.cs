// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using CircleTools.Main.Service.Test.Sync.TestSupport;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.LocalStore.Online;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// One device running the apps' real online-group client (ADR-0086, FEAT-027 phase 2) — its own local store,
/// the real HTTP client — against the real API and Postgres.
/// </summary>
internal sealed class OnlineDevice
{
    private OnlineDevice(HttpClient client, Guid? accountId)
    {
        AccountId = accountId;
        Store = new GroupRecordStore(new InMemoryKeyValueStore());
        Repository = new GroupRecordRepository(Store);
        Sync = new OnlineGroupSync(Store, new HttpOnlineGroupApi(client));
    }

    /// <summary>Gets the signed-in account, if any.</summary>
    public Guid? AccountId { get; }

    /// <summary>Gets the local record store.</summary>
    public GroupRecordStore Store { get; }

    /// <summary>Gets the domain repository the pages use.</summary>
    public GroupRecordRepository Repository { get; }

    /// <summary>Gets the online sync.</summary>
    public OnlineGroupSync Sync { get; }

    /// <summary>A device signed in as <paramref name="account"/>.</summary>
    public static OnlineDevice SignedIn(CircleTestAccount account) => new(account.Client, AccountOf(account));

    /// <summary>A device with nobody signed in.</summary>
    public static OnlineDevice SignedOut(MailApiFactory factory) => new(factory.CreateClient(), null);

    /// <summary>Creates a drawn group of four on this device and returns its id and member ids.</summary>
    public async Task<(Guid GroupId, Guid EventId, IReadOnlyDictionary<string, Guid> Members)> CreateDrawnGroupAsync(string name = "Family")
    {
        var group = Group.Create(name);
        var members = new Dictionary<string, Guid>();
        foreach (var member in new[] { "Anna", "Ben", "Cleo", "Dan" })
        {
            members[member] = group.AddMember(member);
        }

        var eventId = group.AddEvent("Christmas 2026");
        Assert.True(group.RunDraw(eventId, new DrawGenerator(new SystemRandomSource()), DateTimeOffset.UtcNow).IsSuccess);
        await Repository.SaveAsync(group, CancellationToken.None);
        return (group.Id, eventId, members);
    }

    /// <summary>Applies a domain edit to a group this device holds the document of.</summary>
    public async Task EditAsync(Guid groupId, Action<Group> edit)
    {
        var group = (await Repository.GetAsync(groupId, CancellationToken.None))!;
        edit(group);
        await Repository.SaveAsync(group, CancellationToken.None);
    }

    /// <summary>This device's record of a group.</summary>
    public async Task<GroupRecord?> RecordAsync(Guid groupId) =>
        (await Store.LoadAsync()).Records.FirstOrDefault(r => r.Id == groupId);

    /// <summary>Runs one sync cycle.</summary>
    public Task SyncAsync() => Sync.SyncAsync(AccountId);

    // The bearer is a JWT whose "sub" is the account id.
    private static Guid AccountOf(CircleTestAccount account)
    {
        var token = account.Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token.Split('.')[1]));
        using var json = JsonDocument.Parse(payload);
        return Guid.Parse(json.RootElement.GetProperty("sub").GetString()!);
    }
}
