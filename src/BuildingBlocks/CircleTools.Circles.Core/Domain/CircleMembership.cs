using CircleTools.SharedKernel;

namespace CircleTools.Circles.Core.Domain;

/// <summary>
/// One person's place in a circle (ADR-0018): their role, and either the account behind them or
/// the fact that there is none.
/// </summary>
/// <remarks>
/// A membership is <em>account-backed</em> when it carries a <see cref="UserId"/>, and
/// <em>managed</em> when it does not — a person with a profile but no account, who can never sign
/// in (FR-019). The two are the same entity on purpose: an invitation, a Secret-Santa draw or a
/// sync must never bake in the assumption "a person is an account", which is very expensive to
/// unpick afterwards.
/// <para>
/// The <see cref="UserId"/> is a value, not a foreign key. Identity owns accounts; circles hold the
/// identifier and tolerate it resolving to nothing — the same one-way rule ADR-0019 sets for
/// Secret-Santa members.
/// </para>
/// </remarks>
public sealed class CircleMembership : Entity
{
    // EF Core materialization constructor.
    private CircleMembership() => DisplayName = string.Empty;

    private CircleMembership(
        Guid id,
        Guid? userId,
        string displayName,
        string? language,
        CircleRole role,
        DateTimeOffset joinedAt)
    {
        Id = id;
        UserId = userId;
        DisplayName = displayName;
        Language = language;
        Role = role;
        JoinedAt = joinedAt;
    }

    /// <summary>Gets the account behind this person, or <c>null</c> for a managed member.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Gets the name this person is known by in the circle.</summary>
    public string DisplayName { get; private set; }

    /// <summary>Gets this person's preferred language, when one is known.</summary>
    public string? Language { get; private set; }

    /// <summary>Gets this person's role in the circle.</summary>
    public CircleRole Role { get; private set; }

    /// <summary>Gets the moment this membership began.</summary>
    public DateTimeOffset JoinedAt { get; private set; }

    /// <summary>Gets a value indicating whether this is a managed member — a person with no account.</summary>
    public bool IsManaged => UserId is null;

    internal static CircleMembership ForAccount(
        Guid userId,
        string displayName,
        string? language,
        CircleRole role,
        DateTimeOffset now) =>
        new(GuidV7.New(), userId, displayName.Trim(), language, role, now);

    internal static CircleMembership Managed(string displayName, string? language, DateTimeOffset now) =>
        new(GuidV7.New(), userId: null, displayName.Trim(), language, CircleRole.Member, now);

    internal void ChangeRole(CircleRole role) => Role = role;

    internal void Rename(string displayName) => DisplayName = displayName.Trim();

    internal void SetLanguage(string? language) => Language = language;

    /// <summary>
    /// Links this managed membership to the account of the person it describes (FR-019). The
    /// membership is kept rather than replaced, so the person keeps their place and their history,
    /// and the account's display name wins: one person, one name.
    /// </summary>
    internal void LinkToAccount(Guid userId, string displayName, string? language)
    {
        UserId = userId;
        DisplayName = displayName.Trim();

        if (!string.IsNullOrWhiteSpace(language))
        {
            Language = language;
        }
    }
}
