using AppSuite.Circles.Contracts;
using AppSuite.SecretSanta.Contracts;
using AppSuite.SecretSanta.Core.Domain;
using AppSuite.SecretSanta.Ui;
using AppSuite.SecretSanta.Ui.Pages;
using AppSuite.SecretSanta.Ui.Test.TestSupport;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace AppSuite.SecretSanta.Ui.Test;

/// <summary>Unit tests for the create-from-circle DTO → member-input mapping (FR-027).</summary>
public sealed class CreateFromCircleMappingTests
{
    [Fact]
    public void AnAccountedPersonBecomesLinked_AManagedMemberBecomesUnlinked()
    {
        var accountId = Guid.NewGuid();
        var accounted = new CirclePersonResponse(Guid.NewGuid(), "Alice", "member", IsManaged: false, IsYou: false, AccountId: accountId);
        var managed = new CirclePersonResponse(Guid.NewGuid(), "Child", "member", IsManaged: true, IsYou: false, AccountId: null);

        var linked = CreateFromCircleService.ToInput(accounted);
        var unlinked = CreateFromCircleService.ToInput(managed);

        Assert.Equal(accountId, linked.UserId);
        Assert.Equal(accounted.MembershipId, linked.CirclePersonId); // origin recorded
        Assert.Null(unlinked.UserId);
        Assert.Empty(linked.Wishlist);
    }
}

/// <summary>Unit tests for applying member-link resolutions onto local groups (FEAT-005).</summary>
public sealed class LinkResolutionApplierTests
{
    [Fact]
    public async Task ApplyPending_SetsUserIdOnALinkedResolution_AndAcknowledgesIt()
    {
        var repository = new InMemoryGroupRepository();
        var group = Group.Create("Family");
        var aliceId = group.AddMember("Alice", Wishlist.Empty);
        await repository.SaveAsync(group, default);

        var userId = Guid.NewGuid();
        var resolutionId = Guid.NewGuid();
        var api = new FakeLinkingApi
        {
            Resolutions = new MemberLinkResolutionsResponse(
                [new MemberLinkResolution(resolutionId, group.Id, aliceId, MemberLinkResolutionKind.Linked, userId)]),
        };

        var applied = await new LinkResolutionApplier(api, repository).ApplyPendingAsync();

        Assert.Equal(1, applied);
        Assert.Contains(resolutionId, api.Acked);
        var reloaded = await repository.GetAsync(group.Id, default);
        Assert.Equal(userId, reloaded!.FindMember(aliceId)!.UserId);
    }

    [Fact]
    public async Task ApplyPending_ClearsUserIdOnAnUnlinkResolution()
    {
        var repository = new InMemoryGroupRepository();
        var group = Group.Create("Family");
        var aliceId = group.AddMember("Alice", Wishlist.Empty);
        var userId = Guid.NewGuid();
        group.LinkMember(aliceId, userId);
        await repository.SaveAsync(group, default);

        var api = new FakeLinkingApi
        {
            Resolutions = new MemberLinkResolutionsResponse(
                [new MemberLinkResolution(Guid.NewGuid(), group.Id, aliceId, MemberLinkResolutionKind.Unlinked, null)]),
        };

        await new LinkResolutionApplier(api, repository).ApplyPendingAsync();

        var reloaded = await repository.GetAsync(group.Id, default);
        Assert.Null(reloaded!.FindMember(aliceId)!.UserId);
    }
}

/// <summary>Component tests for the signed-in assignment view (FR-029).</summary>
public sealed class MyAssignmentsPageTests : IDisposable
{
    private const string RecipientName = "Recipient-Zoe";
    private readonly Bunit.TestContext _ctx = new();

    public MyAssignmentsPageTests()
    {
        _ctx.Services.AddSingleton<IStringLocalizer<AppStrings>>(new PassThroughStringLocalizer<AppStrings>());
        _ctx.AddTestAuthorization().SetAuthorized("member");
    }

    [Fact]
    public void TheRecipientIsHiddenBehindADeliberateRevealStep()
    {
        var api = new FakeLinkingApi
        {
            Assignments = new MyAssignmentsResponse(
                [new MyAssignmentResponse(Guid.NewGuid(), "Family", RecipientName, ["Book"], null, null)]),
        };
        _ctx.Services.AddSingleton<AppSuite.SecretSanta.LocalStore.Linking.ILinkingApi>(api);

        var cut = _ctx.RenderComponent<MyAssignments>();

        // Before revealing, the recipient must not appear anywhere in the markup.
        Assert.DoesNotContain(RecipientName, cut.Markup);

        cut.Find("button").Click();

        Assert.Contains(RecipientName, cut.Markup);
    }

    /// <inheritdoc />
    public void Dispose() => _ctx.Dispose();
}
