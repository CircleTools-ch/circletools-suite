using CircleTools.Circles.Core.Domain;

namespace CircleTools.Circles.Core.Test.TestSupport;

/// <summary>An in-memory stand-in for the persistence port. Whole circles in, whole circles out.</summary>
internal sealed class InMemoryCircleRepository : ICircleRepository
{
    private readonly List<Circle> _circles = [];

    public IReadOnlyList<Circle> Circles => _circles;

    public int Saves { get; private set; }

    public int Deletes { get; private set; }

    public Task<Circle?> FindAsync(Guid circleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_circles.FirstOrDefault(c => c.Id == circleId));

    public Task<IReadOnlyList<Circle>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Circle>>([.. _circles.Where(c => c.RoleOf(userId) is not null)]);

    public Task<int> CountOwnedByAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_circles.Count(c => c.RoleOf(userId) == CircleRole.Owner));

    public Task<int> CountCreatedSinceAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_circles.Count(c => c.RoleOf(userId) == CircleRole.Owner && c.CreatedAt >= since));

    public Task<Circle?> FindByInvitationHandleAsync(
        string secretHandle,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_circles.FirstOrDefault(c => c.FindInvitationByHandle(secretHandle) is not null));

    public Task<IReadOnlyList<Circle>> ListWithInvitationForAsync(
        string emailHash,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Circle>>(
            [.. _circles.Where(c => c.Invitations.Any(i =>
                string.Equals(i.EmailHash, emailHash, StringComparison.Ordinal)))]);

    public Task<IReadOnlyList<Circle>> ListOwnedByAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Circle>>([.. _circles.Where(c => c.RoleOf(userId) == CircleRole.Owner)]);

    public Task AddAsync(Circle circle, CancellationToken cancellationToken = default)
    {
        _circles.Add(circle);
        return Task.CompletedTask;
    }

    public Task SaveAsync(Circle circle, CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Circle circle, CancellationToken cancellationToken = default)
    {
        _circles.Remove(circle);
        Deletes++;
        return Task.CompletedTask;
    }
}

/// <summary>A stand-in for the account directory. Only what is added here is verified and resolvable.</summary>
internal sealed class StubAccountDirectory : IAccountDirectory
{
    private readonly List<CircleAccount> _accounts = [];

    public StubAccountDirectory Add(Guid userId, string displayName, string email, string? language = null)
    {
        _accounts.Add(new CircleAccount(userId, displayName, email, language));
        return this;
    }

    public Task<CircleAccount?> FindByVerifiedEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var hash = EmailAddress.HashOf(email);

        return Task.FromResult(_accounts.FirstOrDefault(a =>
            string.Equals(EmailAddress.HashOf(a.Email), hash, StringComparison.Ordinal)));
    }

    public Task<CircleAccount?> FindAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_accounts.FirstOrDefault(a => a.UserId == userId));
}

/// <summary>A mailer that records rather than sends, so tests can assert on what would go out.</summary>
internal sealed class RecordingInvitationMailer : IInvitationMailer
{
    private readonly List<InvitationMail> _queued = [];

    public IReadOnlyList<InvitationMail> Queued => _queued;

    public ValueTask QueueAsync(InvitationMail mail, CancellationToken cancellationToken = default)
    {
        _queued.Add(mail);
        return ValueTask.CompletedTask;
    }
}

/// <summary>A rate limiter that answers however the test needs, and counts what it was asked.</summary>
internal sealed class StubInvitationRateLimiter(bool isAllowed = true) : IInvitationRateLimiter
{
    public int Consumptions { get; private set; }

    public Task<InvitationRateLimitDecision> TryConsumeAsync(
        Guid inviterUserId,
        string recipientEmailHash,
        CircleLimits limits,
        CancellationToken cancellationToken = default)
    {
        Consumptions++;

        return Task.FromResult(isAllowed
            ? InvitationRateLimitDecision.Allowed
            : InvitationRateLimitDecision.Refused(CircleFactory.Now.AddHours(1)));
    }
}
