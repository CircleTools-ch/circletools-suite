// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Core.Application;
using CircleTools.SecretSanta.Core.Test.TestSupport;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>"Use as template for a new group" (Q34): members and exclusions come along, nothing else.</summary>
public sealed class CopyGroupTests
{
    [Fact]
    public async Task TheCopy_HasTheMembersAndExclusions_ButNoEventsWishlistsOrDraw()
    {
        var repository = new InMemoryGroupRepository();
        var sourceId = await new CreateGroupCommandHandler(repository).HandleAsync(new CreateGroupCommand("Family 2025"), default);
        var add = new AddMemberCommandHandler(repository);
        var ann = await add.HandleAsync(new AddMemberCommand(sourceId, "Ann"), default);
        var ben = await add.HandleAsync(new AddMemberCommand(sourceId, "Ben"), default);
        await add.HandleAsync(new AddMemberCommand(sourceId, "Cat"), default);
        await new SetMemberGiftNoteCommandHandler(repository).HandleAsync(new SetMemberGiftNoteCommand(sourceId, ann, "No nuts"), default);
        await new AddExclusionCommandHandler(repository).HandleAsync(new AddExclusionCommand(sourceId, ann, ben, Mutual: true), default);
        var eventId = await new AddEventCommandHandler(repository, TimeProvider.System).HandleAsync(new AddEventCommand(sourceId, "Christmas"), default);
        await new SetMemberWishlistCommandHandler(repository).HandleAsync(new SetMemberWishlistCommand(sourceId, eventId, ann, ["Book"]), default);

        var copyId = await new CopyGroupCommandHandler(repository).HandleAsync(new CopyGroupCommand(sourceId, "Family 2026"), default);

        var copy = (await new GetGroupQueryHandler(repository, TimeProvider.System).HandleAsync(new GetGroupQuery(copyId), default))!;
        Assert.NotEqual(sourceId, copyId);
        Assert.Equal("Family 2026", copy.Name);
        Assert.Equal(["Ann", "Ben", "Cat"], copy.Members.Select(m => m.Name));
        Assert.DoesNotContain(copy.Members, m => m.Id == ann); // fresh identities
        Assert.Equal("No nuts", copy.Members.Single(m => m.Name == "Ann").GiftNote);
        var exclusion = Assert.Single(copy.Exclusions);
        Assert.True(exclusion.Mutual);
        Assert.Empty(copy.Events);
    }
}
