using CircleTools.Circles.Core.Domain;

namespace CircleTools.Circles.Core.Test.TestSupport;

/// <summary>
/// Builds circles for tests, and joins people to them the only way the domain allows — through an
/// invitation and an acceptance. There is no back door that adds a membership directly, on purpose:
/// a test that could skip the invitation would stop testing the thing that grants membership.
/// </summary>
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
        CircleLimits? limits = null,
        DateTimeOffset? now = null)
    {
        var created = Circle.Create(
            ownerUserId,
            ownerDisplayName,
            ownerLanguage,
            name,
            label,
            ownedCircleCount: 0,
            limits ?? Limits(),
            now ?? Now);

        Assert.Null(created.Error);
        Assert.NotNull(created.Value);

        return created.Value;
    }

    /// <summary>Invites an address and accepts it, returning the resulting membership.</summary>
    public static CircleMembership Join(
        Circle circle,
        Guid inviterUserId,
        Guid joinerUserId,
        string email,
        string displayName,
        string? language = null,
        Guid? managedMembershipId = null,
        CircleLimits? limits = null,
        DateTimeOffset? now = null)
    {
        var effectiveLimits = limits ?? Limits();
        var at = now ?? Now;
        var invitation = Invite(circle, inviterUserId, email, managedMembershipId, effectiveLimits, at);

        var accepted = circle.Accept(
            invitation.Id,
            joinerUserId,
            email,
            displayName,
            language,
            effectiveLimits,
            at);

        Assert.Null(accepted.Error);
        Assert.NotNull(accepted.Value);

        return accepted.Value;
    }

    /// <summary>Invites an address, asserting that something was produced to send.</summary>
    public static Invitation Invite(
        Circle circle,
        Guid inviterUserId,
        string email,
        Guid? managedMembershipId = null,
        CircleLimits? limits = null,
        DateTimeOffset? now = null)
    {
        var dispatch = circle.Invite(
            inviterUserId,
            email,
            resolvedUserId: null,
            managedMembershipId,
            InvitationSecret.Create().Handle,
            limits ?? Limits(),
            now ?? Now);

        Assert.Null(dispatch.Error);
        Assert.NotNull(dispatch.Value);
        Assert.NotNull(dispatch.Value.Invitation);

        return dispatch.Value.Invitation;
    }

    /// <summary>Adds a managed member, asserting it worked.</summary>
    public static CircleMembership AddManaged(
        Circle circle,
        Guid actorUserId,
        string displayName,
        string? language = null,
        CircleLimits? limits = null,
        DateTimeOffset? now = null)
    {
        var added = circle.AddManagedMember(actorUserId, displayName, language, limits ?? Limits(), now ?? Now);

        Assert.Null(added.Error);
        Assert.NotNull(added.Value);

        return added.Value;
    }
}
