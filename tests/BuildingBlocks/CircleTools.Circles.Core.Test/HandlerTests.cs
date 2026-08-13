using CircleTools.Circles.Core.Application;
using CircleTools.Circles.Core.Domain;
using CircleTools.Circles.Core.Test.TestSupport;
using Microsoft.Extensions.Options;

namespace CircleTools.Circles.Core.Test;

/// <summary>
/// The application handlers over in-memory ports (TP-003). The weight here is on the decisions the
/// handlers own rather than the aggregate: which mail goes out and in what language, what an
/// invitation answers when it sends nothing, and that an unknown circle and someone else's circle
/// are the same answer.
/// </summary>
public sealed class HandlerTests
{
    private static readonly Guid _anna = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid _ben = Guid.Parse("00000000-0000-0000-0000-0000000000b2");
    private static readonly Guid _stranger = Guid.Parse("00000000-0000-0000-0000-0000000000b3");

    private readonly InMemoryCircleRepository _repository = new();
    private readonly RecordingInvitationMailer _mailer = new();
    private readonly StubAccountDirectory _accounts = new StubAccountDirectory()
        .Add(_anna, "Anna", "anna@example.com", "de-CH")
        .Add(_ben, "Ben", "ben@example.com", "fr-CH")
        .Add(_stranger, "Sam", "sam@example.com", "en");

    private readonly CircleLimits _limits = new();
    private readonly TestClock _clock = new(CircleFactory.Now);

    [Fact]
    public async Task CreateCircle_MakesTheCallerTheOwner_AndDefaultsTheLabelToTheirLanguage()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateCircleCommand(_anna, "Müller", null),
            CancellationToken.None);

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(CircleLabelDefaults.German, result.Value.Label);
        Assert.Equal("owner", result.Value.Role);

        var person = Assert.Single(result.Value.People);
        Assert.Equal("Anna", person.DisplayName);
        Assert.True(person.IsYou);
    }

    [Fact]
    public async Task CreateCircle_WithoutAResolvableAccount_IsRefused()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateCircleCommand(Guid.NewGuid(), "Müller", null),
            CancellationToken.None);

        Assert.Equal(CircleError.AccountNotFound, result.Error);
        Assert.Empty(_repository.Circles);
    }

    [Fact]
    public async Task CreateCircle_BeyondTheDailyRate_IsRefused()
    {
        _limits.CircleCreationsPerDay = 1;
        var handler = CreateHandler();
        var first = await handler.HandleAsync(
            new CreateCircleCommand(_anna, "Müller", null),
            CancellationToken.None);

        var second = await handler.HandleAsync(
            new CreateCircleCommand(_anna, "Climbing", null),
            CancellationToken.None);

        Assert.Null(first.Error);
        Assert.Equal(CircleError.CircleCreationRateLimitReached, second.Error);
        Assert.Single(_repository.Circles);
    }

    [Fact]
    public async Task GetCircle_ForANonMember_AnswersExactlyAsAnUnknownCircleDoes()
    {
        var circle = await CreateCircleAsync();
        var handler = new GetCircleQueryHandler(_repository);

        var notAMember = await handler.HandleAsync(
            new GetCircleQuery(_stranger, circle.Id),
            CancellationToken.None);

        var noSuchCircle = await handler.HandleAsync(
            new GetCircleQuery(_stranger, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(CircleError.NotAMember, notAMember.Error);
        Assert.Equal(notAMember.Error, noSuchCircle.Error);
        Assert.Null(notAMember.Value);
        Assert.Null(noSuchCircle.Value);
    }

    [Fact]
    public async Task Invite_QueuesOneMailInTheRecipientsLanguage_WhenTheAddressHasAnAccount()
    {
        var circle = await CreateCircleAsync();

        var result = await InviteHandler().HandleAsync(
            new InviteToCircleCommand(_anna, circle.Id, "ben@example.com", null),
            CancellationToken.None);

        Assert.Null(result.Error);
        var mail = Assert.Single(_mailer.Queued);
        Assert.Equal("fr-CH", mail.Language);
        Assert.True(mail.RecipientHasAccount);
        Assert.Equal("Anna", mail.InviterDisplayName);
    }

    [Fact]
    public async Task Invite_UsesTheInvitersLanguage_WhenTheAddressHasNoAccount()
    {
        var circle = await CreateCircleAsync();

        await InviteHandler().HandleAsync(
            new InviteToCircleCommand(_anna, circle.Id, "carla@example.com", null),
            CancellationToken.None);

        var mail = Assert.Single(_mailer.Queued);
        Assert.Equal("de-CH", mail.Language);
        Assert.False(mail.RecipientHasAccount);
    }

    [Fact]
    public async Task Invite_ToSomeoneAlreadyInTheCircle_AnswersTheSameAndQueuesNothing()
    {
        var circle = await CreateCircleAsync();
        CircleFactory.Join(circle, _anna, _ben, "ben@example.com", "Ben");

        var result = await InviteHandler().HandleAsync(
            new InviteToCircleCommand(_anna, circle.Id, "ben@example.com", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_mailer.Queued);
        Assert.Empty(circle.Invitations);
    }

    [Fact]
    public async Task Invite_ChargesTheRateLimiterEvenWhenItSendsNothing()
    {
        var circle = await CreateCircleAsync();
        CircleFactory.Join(circle, _anna, _ben, "ben@example.com", "Ben");
        var limiter = new StubInvitationRateLimiter();

        await InviteHandler(limiter).HandleAsync(
            new InviteToCircleCommand(_anna, circle.Id, "ben@example.com", null),
            CancellationToken.None);

        // Charging only the attempts that produce a mail would let an inviter learn, from how soon
        // they hit the limit, that this address was silently skipped.
        Assert.Equal(1, limiter.Consumptions);
    }

    [Fact]
    public async Task Invite_WhenTheLimiterRefuses_IsRefusedAndSendsNothing()
    {
        var circle = await CreateCircleAsync();

        var result = await InviteHandler(new StubInvitationRateLimiter(isAllowed: false)).HandleAsync(
            new InviteToCircleCommand(_anna, circle.Id, "carla@example.com", null),
            CancellationToken.None);

        Assert.Equal(CircleError.InvitationRateLimitReached, result.Error);
        Assert.Empty(_mailer.Queued);
    }

    [Fact]
    public async Task Invite_AnInvalidAddress_IsRefusedBeforeAnythingIsCharged()
    {
        var circle = await CreateCircleAsync();
        var limiter = new StubInvitationRateLimiter();

        var result = await InviteHandler(limiter).HandleAsync(
            new InviteToCircleCommand(_anna, circle.Id, "not-an-address", null),
            CancellationToken.None);

        Assert.Equal(CircleError.EmailInvalid, result.Error);
        Assert.Equal(0, limiter.Consumptions);
    }

    [Fact]
    public async Task AcceptInvitation_FromTheLink_JoinsTheCircleAsAMember()
    {
        var circle = await CreateCircleAsync();
        var (secret, handle) = InvitationSecret.Create();
        InviteWithHandle(circle, "ben@example.com", handle);

        var result = await AcceptHandler().HandleAsync(
            new AcceptInvitationCommand(_ben, secret),
            CancellationToken.None);

        Assert.Null(result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal("member", result.Value.Role);
        Assert.Equal(CircleRole.Member, circle.RoleOf(_ben));
    }

    [Fact]
    public async Task AcceptInvitation_BySomeoneElsesVerifiedAddress_CreatesNothingAndRevealsNothing()
    {
        var circle = await CreateCircleAsync();
        var (secret, handle) = InvitationSecret.Create();
        InviteWithHandle(circle, "ben@example.com", handle);

        var result = await AcceptHandler().HandleAsync(
            new AcceptInvitationCommand(_stranger, secret),
            CancellationToken.None);

        Assert.Equal(CircleError.InvitationAddressMismatch, result.Error);
        Assert.Null(result.Value);
        Assert.Null(circle.RoleOf(_stranger));
        Assert.Single(circle.Invitations);
    }

    [Fact]
    public async Task InvitationPreview_ShowsOnlyTheCircleNameLabelAndInviter()
    {
        var circle = await CreateCircleAsync();
        var (secret, handle) = InvitationSecret.Create();
        InviteWithHandle(circle, "ben@example.com", handle);

        var result = await new GetInvitationPreviewQueryHandler(_repository, _clock).HandleAsync(
            new GetInvitationPreviewQuery(secret),
            CancellationToken.None);

        Assert.NotNull(result.Value);
        Assert.Equal("Müller", result.Value.CircleName);
        Assert.Equal(CircleLabelDefaults.German, result.Value.CircleLabel);
        Assert.Equal("Anna", result.Value.InviterDisplayName);
    }

    [Fact]
    public async Task JoinRequests_ListOnlyTheInvitationsForTheCallersVerifiedAddress()
    {
        var circle = await CreateCircleAsync();
        CircleFactory.Invite(circle, _anna, "ben@example.com");
        CircleFactory.Invite(circle, _anna, "carla@example.com");

        var requests = await new GetJoinRequestsQueryHandler(_repository, _accounts, _clock).HandleAsync(
            new GetJoinRequestsQuery(_ben),
            CancellationToken.None);

        var request = Assert.Single(requests);
        Assert.Equal(circle.Id, request.CircleId);
        Assert.Equal("Anna", request.InviterDisplayName);
    }

    [Fact]
    public async Task DeclineInvitation_NeedsNoAccount_AndConsumesTheInvitation()
    {
        var circle = await CreateCircleAsync();
        var (secret, handle) = InvitationSecret.Create();
        InviteWithHandle(circle, "carla@example.com", handle);

        var result = await new DeclineInvitationCommandHandler(_repository, _clock).HandleAsync(
            new DeclineInvitationCommand(secret),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(circle.Invitations);
        Assert.Single(circle.Declines);
    }

    [Fact]
    public async Task DeclineJoinRequest_ByAnotherAccount_IsRefused()
    {
        var circle = await CreateCircleAsync();
        var invitation = CircleFactory.Invite(circle, _anna, "ben@example.com");

        var result = await new DeclineJoinRequestCommandHandler(_repository, _accounts, _clock).HandleAsync(
            new DeclineJoinRequestCommand(_stranger, circle.Id, invitation.Id),
            CancellationToken.None);

        Assert.Equal(CircleError.InvitationNotFound, result.Error);
        Assert.Single(circle.Invitations);
    }

    [Fact]
    public async Task DeleteCircle_NeedsTheNameTypedBack()
    {
        var circle = await CreateCircleAsync();
        var handler = new DeleteCircleCommandHandler(_repository);

        var mistyped = await handler.HandleAsync(
            new DeleteCircleCommand(_anna, circle.Id, "Meier"),
            CancellationToken.None);

        var confirmed = await handler.HandleAsync(
            new DeleteCircleCommand(_anna, circle.Id, " müller "),
            CancellationToken.None);

        Assert.Equal(CircleError.NameConfirmationMismatch, mistyped.Error);
        Assert.True(confirmed.IsSuccess);
        Assert.Empty(_repository.Circles);
    }

    [Fact]
    public async Task DeleteCircle_ByAnAdmin_IsRefused()
    {
        var circle = await CreateCircleAsync();
        var admin = CircleFactory.Join(circle, _anna, _ben, "ben@example.com", "Ben");
        Assert.True(circle.ChangeRole(_anna, admin.Id, CircleRole.Admin).IsSuccess);

        var result = await new DeleteCircleCommandHandler(_repository).HandleAsync(
            new DeleteCircleCommand(_ben, circle.Id, "Müller"),
            CancellationToken.None);

        Assert.Equal(CircleError.RoleNotPermitted, result.Error);
        Assert.Single(_repository.Circles);
    }

    [Fact]
    public async Task PurgeAccountFromCircles_RemovesTheirMemberships_AndLeavesManagedMembers()
    {
        var circle = await CreateCircleAsync();
        var ben = CircleFactory.Join(circle, _anna, _ben, "ben@example.com", "Ben");
        Assert.True(circle.ChangeRole(_anna, ben.Id, CircleRole.Admin).IsSuccess);
        CircleFactory.AddManaged(circle, _ben, "Mia");

        await new PurgeAccountFromCirclesCommandHandler(_repository).HandleAsync(
            new PurgeAccountFromCirclesCommand(_ben),
            CancellationToken.None);

        Assert.Null(circle.RoleOf(_ben));
        Assert.Contains(circle.Memberships, m => m is { IsManaged: true, DisplayName: "Mia" });
    }

    [Fact]
    public async Task SoleOwnedCircles_TellTheCallerWhetherAnyoneElseCouldTakeOver()
    {
        var circle = await CreateCircleAsync();
        CircleFactory.AddManaged(circle, _anna, "Mia");

        var owned = await new GetSoleOwnedCirclesQueryHandler(_repository).HandleAsync(
            new GetSoleOwnedCirclesQuery(_anna),
            CancellationToken.None);

        var only = Assert.Single(owned);
        Assert.Equal("Müller", only.Name);
        Assert.Equal(0, only.OtherAccountBackedPeople);
    }

    private async Task<Circle> CreateCircleAsync()
    {
        var created = await CreateHandler().HandleAsync(
            new CreateCircleCommand(_anna, "Müller", null),
            CancellationToken.None);

        Assert.Null(created.Error);

        return _repository.Circles.Single(c => c.Id == created.Value!.Id);
    }

    private void InviteWithHandle(Circle circle, string email, string handle)
    {
        var dispatch = circle.Invite(
            _anna,
            email,
            resolvedUserId: null,
            managedMembershipId: null,
            handle,
            _limits,
            _clock.GetUtcNow());

        Assert.Null(dispatch.Error);
    }

    private CreateCircleCommandHandler CreateHandler() =>
        new(_repository, _accounts, Options.Create(_limits), _clock);

    private InviteToCircleCommandHandler InviteHandler(StubInvitationRateLimiter? limiter = null) =>
        new(_repository, _accounts, _mailer, limiter ?? new StubInvitationRateLimiter(), Options.Create(_limits), _clock);

    private AcceptInvitationCommandHandler AcceptHandler() =>
        new(_repository, _accounts, Options.Create(_limits), _clock);
}
