using AppSuite.Circles.Core.Domain;

namespace AppSuite.Circles.Repository.Test.TestSupport;

/// <summary>
/// Builds circles for the repository tests, and joins people to them the only way the domain
/// allows — through an invitation and an acceptance.
/// </summary>
/// <remarks>
/// There is deliberately no back door that inserts a membership row directly. These tests are about
/// whether the aggregate survives a round trip through Postgres unchanged; a fixture that built the
/// rows itself would be testing its own assumptions about the mapping instead.
/// </remarks>
internal static class CircleFactory
{
    /// <summary>A fixed instant. Nothing in these tests waits for real time to pass.</summary>
    public static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    public static CircleLimits Limits() => new();

    public static Circle WithOwner(
        Guid ownerUserId,
        string ownerDisplayName = "Anna",
        string? ownerLanguage = "de-CH",
        string name = "Müller",
        string? label = null,
        DateTimeOffset? now = null)
    {
        var created = Circle.Create(
            ownerUserId,
            ownerDisplayName,
            ownerLanguage,
            name,
            label,
            ownedCircleCount: 0,
            Limits(),
            now ?? Now);

        Assert.Null(created.Error);
        return created.Value!;
    }

    /// <summary>Invites an address, asserting that something was produced to send.</summary>
    public static Invitation Invite(
        Circle circle,
        Guid inviterUserId,
        string email,
        Guid? managedMembershipId = null,
        DateTimeOffset? now = null)
    {
        var dispatch = circle.Invite(
            inviterUserId,
            email,
            resolvedUserId: null,
            managedMembershipId,
            InvitationSecret.Create().Handle,
            Limits(),
            now ?? Now);

        Assert.Null(dispatch.Error);
        Assert.NotNull(dispatch.Value!.Invitation);

        return dispatch.Value.Invitation;
    }

    /// <summary>Invites an address and accepts it, returning the resulting membership.</summary>
    public static CircleMembership Join(
        Circle circle,
        Guid inviterUserId,
        Guid joinerUserId,
        string email,
        string displayName,
        string? language = null,
        DateTimeOffset? now = null)
    {
        var at = now ?? Now;
        var invitation = Invite(circle, inviterUserId, email, now: at);

        var accepted = circle.Accept(invitation.Id, joinerUserId, email, displayName, language, Limits(), at);

        Assert.Null(accepted.Error);
        return accepted.Value!;
    }

    /// <summary>Adds a managed member, asserting it worked.</summary>
    public static CircleMembership AddManaged(
        Circle circle,
        Guid actorUserId,
        string displayName,
        string? language = null,
        DateTimeOffset? now = null)
    {
        var added = circle.AddManagedMember(actorUserId, displayName, language, Limits(), now ?? Now);

        Assert.Null(added.Error);
        return added.Value!;
    }
}
