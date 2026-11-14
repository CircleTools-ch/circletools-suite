// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Contracts;
using CircleTools.SecretSanta.Core.Domain;
using CircleTools.SecretSanta.LocalStore.Online;

namespace CircleTools.Main.Service.Test.Api;

/// <summary>
/// The apps' online-group client against the real server (ADR-0086, TP-027): several devices, each with its own
/// local store, converge through the one group store — whatever their role.
/// </summary>
[Collection(MailApi.CollectionName)]
public sealed class OnlineGroupClientTests(MailApiFactory factory)
{
    [Fact]
    public async Task AGroupMadeOnline_AppearsOnTheAccountsOtherDevice_AndEditsConverge()
    {
        var account = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var phone = OnlineDevice.SignedIn(account);
        var laptop = OnlineDevice.SignedIn(account);
        var (groupId, _, _) = await phone.CreateDrawnGroupAsync();

        Assert.True((await phone.Sync.MakeOnlineAsync(groupId, phone.AccountId, joinEnabled: false)).IsSuccess);
        await laptop.SyncAsync();

        var onLaptop = (await laptop.RecordAsync(groupId))!;
        Assert.Equal(GroupPlace.Online, onLaptop.Place);
        Assert.Equal("Family", onLaptop.Document!.Name);
        Assert.Equal(4, onLaptop.Document.Members.Count);
        Assert.True(onLaptop.Access!.Can(OnlinePermissions.WriteDocument));

        await laptop.EditAsync(groupId, g => g.Rename("Family 2026"));
        await laptop.SyncAsync();
        await phone.SyncAsync();

        Assert.Equal("Family 2026", (await phone.RecordAsync(groupId))!.Document!.Name);
        Assert.False((await laptop.RecordAsync(groupId))!.Dirty);
    }

    [Fact]
    public async Task AParticipantsEntry_ReachesTheOrganizer_AndAnOfflineOrganizerEditSurvives()
    {
        var account = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var organizer = OnlineDevice.SignedIn(account);
        var (groupId, eventId, members) = await organizer.CreateDrawnGroupAsync();
        await organizer.Sync.MakeOnlineAsync(groupId, organizer.AccountId, joinEnabled: true);
        var joinToken = (await organizer.RecordAsync(groupId))!.Access!.JoinToken!;

        var guest = OnlineDevice.SignedOut(factory);
        var joined = await guest.Sync.JoinAsync(joinToken, new JoinRequest(members["Ben"], null, WithAccount: false), null);
        Assert.True(joined.IsSuccess);
        var view = (await guest.RecordAsync(groupId))!.View!;
        Assert.Equal("Ben", view.MemberName);
        Assert.NotNull(Assert.Single(view.Events).Assignment);

        // The organizer edits offline (not synced yet) while Ben writes his wishes.
        await organizer.EditAsync(groupId, g => g.Rename("Edited offline"));
        await guest.Sync.WriteOwnEntryAsync(groupId, new OwnEntryRequest("Vegan", [new OwnWishlist(eventId, ["Plant"])]));
        Assert.Null((await guest.RecordAsync(groupId))!.PendingEntry);

        await organizer.SyncAsync();

        var record = (await organizer.RecordAsync(groupId))!;
        var group = Group.FromState(record.Document!);
        Assert.Equal("Edited offline", group.Name);
        Assert.Equal(["Plant"], group.FindEvent(eventId)!.WishlistFor(members["Ben"]).Items);
        Assert.Contains(members["Ben"], record.SelfMaintained!);
        Assert.False(record.Dirty);
    }

    [Fact]
    public async Task DeletingTheGroup_RemovesItFromParticipants_AndKeepOnDeviceKeepsTheOrganizersCopy()
    {
        var account = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var organizer = OnlineDevice.SignedIn(account);
        var (groupId, _, members) = await organizer.CreateDrawnGroupAsync();
        await organizer.Sync.MakeOnlineAsync(groupId, organizer.AccountId, joinEnabled: true);
        var joinToken = (await organizer.RecordAsync(groupId))!.Access!.JoinToken!;
        var guest = OnlineDevice.SignedOut(factory);
        await guest.Sync.JoinAsync(joinToken, new JoinRequest(members["Cleo"], null, false), null);

        Assert.True((await organizer.Sync.KeepOnDeviceAsync(groupId)).IsSuccess);
        await guest.SyncAsync();

        Assert.Null(await guest.RecordAsync(groupId));
        var kept = (await organizer.RecordAsync(groupId))!;
        Assert.Equal(GroupPlace.Device, kept.Place);
        Assert.Equal(4, kept.Document!.Members.Count);
    }

    [Fact]
    public async Task AnInvitation_IsDiscovered_AndAcceptingAddsTheGroupToEveryDeviceOfTheAccount()
    {
        var organizerAccount = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var cleoAccount = await CircleTestAccounts.SignUpAsync(factory, "Cleo");
        var organizer = OnlineDevice.SignedIn(organizerAccount);
        var cleoPhone = OnlineDevice.SignedIn(cleoAccount);
        var cleoLaptop = OnlineDevice.SignedIn(cleoAccount);
        var (groupId, _, members) = await organizer.CreateDrawnGroupAsync();
        await organizer.Sync.MakeOnlineAsync(groupId, organizer.AccountId, joinEnabled: false);

        Assert.True((await organizer.Sync.AddParticipantAsync(
            groupId, new AddParticipantRequest(members["Cleo"], ParticipantRoute.Invitation, cleoAccount.Email))).IsSuccess);
        Assert.Contains((await organizer.RecordAsync(groupId))!.Roles!, r => r.MemberId == members["Cleo"] && r.Invited);

        await cleoPhone.SyncAsync();
        Assert.Contains((await cleoPhone.Store.LoadAsync()).Invitations, i => i.GroupId == groupId);
        Assert.Null(await cleoPhone.RecordAsync(groupId));

        Assert.True((await cleoPhone.Sync.AnswerInvitationAsync(groupId, accept: true, cleoPhone.AccountId!.Value)).IsSuccess);
        await cleoLaptop.SyncAsync();

        Assert.Equal("Cleo", (await cleoPhone.RecordAsync(groupId))!.View!.MemberName);
        Assert.Equal("Cleo", (await cleoLaptop.RecordAsync(groupId))!.View!.MemberName);
        Assert.Empty((await cleoLaptop.Store.LoadAsync()).Invitations);
    }

    [Fact]
    public async Task TheJoinLink_ShowsAnOpenInvitationAsInvited_AndAJoinAsTaken()
    {
        // ST-3 (2026-10-28): an invitee who had not answered yet showed as "Already joined" on the join page.
        var organizerAccount = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var cleoAccount = await CircleTestAccounts.SignUpAsync(factory, "Cleo");
        var organizer = OnlineDevice.SignedIn(organizerAccount);
        var (groupId, _, members) = await organizer.CreateDrawnGroupAsync();
        await organizer.Sync.MakeOnlineAsync(groupId, organizer.AccountId, joinEnabled: true);
        var joinToken = (await organizer.RecordAsync(groupId))!.Access!.JoinToken!;
        await organizer.Sync.AddParticipantAsync(
            groupId, new AddParticipantRequest(members["Cleo"], ParticipantRoute.Invitation, cleoAccount.Email));
        await OnlineDevice.SignedOut(factory).Sync.JoinAsync(joinToken, new JoinRequest(members["Ben"], null, WithAccount: false), null);

        var preview = (await new HttpOnlineGroupApi(factory.CreateClient()).PreviewJoinAsync(joinToken)).Value!;

        var cleo = preview.Members.Single(m => m.MemberId == members["Cleo"]);
        var ben = preview.Members.Single(m => m.MemberId == members["Ben"]);
        Assert.True(cleo is { Taken: true, Invited: true });
        Assert.True(ben is { Taken: true, Invited: false });
    }

    [Fact]
    public async Task AMemberJustAddedOnTheDevice_CanBeInvitedRightAway()
    {
        var organizerAccount = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var guestAccount = await CircleTestAccounts.SignUpAsync(factory, "Guest");
        var organizer = OnlineDevice.SignedIn(organizerAccount);
        var (groupId, _, _) = await organizer.CreateDrawnGroupAsync();
        await organizer.Sync.MakeOnlineAsync(groupId, organizer.AccountId, joinEnabled: false);

        // Added here and not synced yet: the invitation must still find the member on the server.
        Guid newcomer = Guid.Empty;
        await organizer.EditAsync(groupId, g => newcomer = g.AddMember("Newcomer"));

        var invited = await organizer.Sync.AddParticipantAsync(
            groupId, new AddParticipantRequest(newcomer, ParticipantRoute.Invitation, guestAccount.Email));

        Assert.True(invited.IsSuccess);
        Assert.Contains((await organizer.RecordAsync(groupId))!.Roles!, r => r.MemberId == newcomer && r.Invited);
    }

    [Fact]
    public async Task AnOrganizerInTheRoster_GetsTheirOwnAssignmentOnSync()
    {
        var account = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var organizer = OnlineDevice.SignedIn(account);
        var (groupId, _, members) = await organizer.CreateDrawnGroupAsync();
        await organizer.Sync.MakeOnlineAsync(groupId, organizer.AccountId, joinEnabled: false);
        Assert.True((await organizer.Sync.AddParticipantAsync(
            groupId, new AddParticipantRequest(members["Ben"], ParticipantRoute.OwnMember))).IsSuccess);

        await organizer.SyncAsync();

        var record = (await organizer.RecordAsync(groupId))!;
        Assert.NotNull(record.Document); // still organizing
        Assert.Equal("Ben", record.View!.MemberName);
        Assert.NotNull(Assert.Single(record.View.Events).Assignment);
        Assert.False(record.Access!.Can(OnlinePermissions.WriteOwnEntry)); // their entry stays in the document
    }

    [Fact]
    public async Task ALinkOwnedGroup_IsTakenOverBySigningIn()
    {
        var device = OnlineDevice.SignedOut(factory);
        var (groupId, _, _) = await device.CreateDrawnGroupAsync();
        Assert.True((await device.Sync.MakeOnlineAsync(groupId, null, joinEnabled: false)).IsSuccess);
        var byLink = (await device.RecordAsync(groupId))!.Access!;
        Assert.Equal(AccessPrincipal.OrganizerLink, byLink.Principal);
        Assert.True(byLink.Can(OnlinePermissions.WriteDocument));

        var account = await CircleTestAccounts.SignUpAsync(factory, "Taker");
        var signedIn = OnlineDevice.SignedIn(account);
        await signedIn.Store.SaveAsync(await device.Store.LoadAsync());

        Assert.True((await signedIn.Sync.TakeOverAsync(groupId, signedIn.AccountId!.Value)).IsSuccess);

        var taken = (await signedIn.RecordAsync(groupId))!.Access!;
        Assert.Equal(AccessPrincipal.Account, taken.Principal);
        Assert.True(taken.Can(OnlinePermissions.WriteDocument));

        // The old link no longer works: the signed-out copy loses access and keeps its document.
        await device.SyncAsync();
        Assert.Equal(GroupPlace.Device, (await device.RecordAsync(groupId))!.Place);
    }

    [Fact]
    public async Task RevealTokens_PushAnUnsentDrawFirst()
    {
        var account = await CircleTestAccounts.SignUpAsync(factory, "Organizer");
        var organizer = OnlineDevice.SignedIn(account);
        var (groupId, eventId, _) = await organizer.CreateDrawnGroupAsync();
        await organizer.Sync.MakeOnlineAsync(groupId, organizer.AccountId, joinEnabled: false);

        // A second event drawn offline: its givers need tokens too, so the draw must reach the server first.
        Guid second = Guid.Empty;
        await organizer.EditAsync(groupId, g =>
        {
            second = g.AddEvent("Easter");
            Assert.True(g.RunDraw(second, new DrawGenerator(new SystemRandomSource()), DateTimeOffset.UtcNow).IsSuccess);
        });

        var tokens = await organizer.Sync.GetRevealTokensAsync(groupId);

        Assert.True(tokens.IsSuccess);
        Assert.Equal(4, tokens.Value!.Count(t => t.EventId == eventId));
        Assert.Equal(4, tokens.Value!.Count(t => t.EventId == second));
    }
}
