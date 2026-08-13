using CircleTools.SharedKernel;

namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// The trace a declined invitation leaves behind: the hash of the address and when it said no.
/// </summary>
/// <remarks>
/// It exists only to enforce the re-invite cooldown, so it keeps only what that needs. Holding the
/// address itself would mean a circle retaining the address of someone who explicitly declined to
/// have anything to do with it (NFR-004).
/// <para>
/// The cooldown is enforced <em>silently</em>. A re-invite inside the window answers "invitation
/// sent" like every other case and sends nothing — because a refusal the inviter could see would
/// tell them the invitation was declined rather than ignored, which is exactly the difference
/// FR-018 hides.
/// </para>
/// </remarks>
public sealed class DeclineRecord : Entity
{
    // EF Core materialization constructor.
    private DeclineRecord() => EmailHash = string.Empty;

    private DeclineRecord(Guid id, string emailHash, DateTimeOffset declinedAt)
    {
        Id = id;
        EmailHash = emailHash;
        DeclinedAt = declinedAt;
    }

    /// <summary>Gets the hash of the address that declined.</summary>
    public string EmailHash { get; private set; }

    /// <summary>Gets the moment it declined.</summary>
    public DateTimeOffset DeclinedAt { get; private set; }

    /// <summary>Checks whether the cooldown is still running.</summary>
    /// <param name="now">The current instant.</param>
    /// <param name="cooldown">How long a decline suppresses re-invitations.</param>
    /// <returns><c>true</c> while a re-invite must be silently swallowed.</returns>
    public bool IsActiveAt(DateTimeOffset now, TimeSpan cooldown) => now - DeclinedAt < cooldown;

    internal static DeclineRecord For(string emailHash, DateTimeOffset now) =>
        new(GuidV7.New(), emailHash, now);
}
