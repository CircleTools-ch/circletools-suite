namespace AppSuite.Circles.Core.Domain;

/// <summary>
/// What inviting an address actually produced: a link to mail, or nothing at all.
/// </summary>
/// <remarks>
/// The inviter is told "invitation sent" either way (FR-017). The distinction exists only inside
/// the server, because the cases where nothing happens — the address already belongs to someone in
/// the circle, or it declined recently — are precisely the cases whose visibility would answer
/// "does this address have an account?" or "did they say no?".
/// </remarks>
public sealed class InvitationDispatch
{
    private InvitationDispatch(Invitation? invitation) => Invitation = invitation;

    /// <summary>Gets the outcome of an invite that produced nothing to send.</summary>
    public static InvitationDispatch Suppressed { get; } = new(invitation: null);

    /// <summary>Gets the invitation whose link should be mailed, or <c>null</c> when suppressed.</summary>
    public Invitation? Invitation { get; }

    /// <summary>Gets a value indicating whether a mail should be queued.</summary>
    public bool ShouldSend => Invitation is not null;

    /// <summary>Creates the outcome of an invite that produced a link to mail.</summary>
    /// <param name="invitation">The invitation to send.</param>
    /// <returns>The dispatch instruction.</returns>
    public static InvitationDispatch Send(Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return new InvitationDispatch(invitation);
    }
}
