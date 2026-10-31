using CircleTools.SecretSanta.Core.Persistence;
using CircleTools.SecretSanta.Service.Core.Access;
using CircleTools.SecretSanta.Service.Core.Entries;

namespace CircleTools.SecretSanta.Service.Core.Groups;

/// <summary>
/// The persistence port of the Secret-Santa group store (ADR-0086). It stores; it does not authorize — the
/// <see cref="GroupService"/> resolves the caller's role assignment and checks permissions before calling it.
/// Every write that touches the document locks the group row, so concurrent pushes and entry writes can't lose
/// each other's changes (review finding C3).
/// </summary>
public interface IGroupStore
{
    /// <summary>Creates an online group with its first role assignment (the owner).</summary>
    /// <param name="group">The group.</param>
    /// <param name="owner">The owner's assignment.</param>
    /// <param name="ownerSecretHash">The organizer link's token hash, for a link-owned group.</param>
    /// <param name="ownerSecretCipher">The organizer link's token, encrypted, for a link-owned group.</param>
    /// <param name="creatorKeyHash">The creator key's hash, so a retried create can recover the links.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="false"/> when a group with that id already exists.</returns>
    Task<bool> CreateAsync(
        OnlineGroup group,
        RoleAssignment owner,
        string? ownerSecretHash,
        string? ownerSecretCipher,
        string? creatorKeyHash,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a group with its role assignments and member entries.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The group, or <see langword="null"/>.</returns>
    Task<OnlineGroupDetails?> GetAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Whether the creator key matches the one stored when the group was created.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="creatorKeyHash">The presented key's hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    Task<bool> IsCreatorAsync(Guid groupId, string creatorKeyHash, CancellationToken cancellationToken = default);

    /// <summary>The role assignment the caller holds in a group, if any (active or invited).</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="accountId">The signed-in account, if any.</param>
    /// <param name="secretHashes">Hashes of the organizer token and device key presented, if any.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The assignment, preferring an active one; or <see langword="null"/>.</returns>
    Task<RoleAssignment?> FindAssignmentAsync(
        Guid groupId,
        Guid? accountId,
        IReadOnlyCollection<string> secretHashes,
        CancellationToken cancellationToken = default);

    /// <summary>The encrypted organizer-link token of a link-owned group, if any.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The ciphertext, or <see langword="null"/>.</returns>
    Task<string?> GetOrganizerSecretCipherAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new document version: re-applies the member entries, raises the version, records the writer.
    /// Refused as <see cref="WriteStatus.Conflict"/> when <paramref name="schemaVersion"/> is lower than the stored one.
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <param name="baseVersion">The version the pusher's copy was based on (newest write wins for now, ADR-0016).</param>
    /// <param name="schemaVersion">The pusher's document schema version.</param>
    /// <param name="document">The pushed document.</param>
    /// <param name="writer">The writer's role assignment.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome.</returns>
    Task<PushResult> PushAsync(
        Guid groupId,
        long baseVersion,
        int schemaVersion,
        GroupState document,
        Guid writer,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a group and everything stored for it.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Records that someone used the group, at most once a day, so retention spares it (NFR-010).</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    Task TouchAsync(Guid groupId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>The groups no account owns that nobody has used since <paramref name="cutoff"/> (NFR-010).</summary>
    /// <param name="cutoff">The last activity a group must have had to be kept.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Their ids.</returns>
    Task<IReadOnlyList<Guid>> ListOwnerlessInactiveAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    /// <summary>Deletes the invitations that lapsed unanswered before <paramref name="now"/> (NFR-004).</summary>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many were deleted.</returns>
    Task<int> DeleteExpiredInvitationsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Stores a member's entry and merges it into the document, raising the version.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="entry">The entry; it replaces the stored one (a reveal-link write passes the merged entry).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The new version, or <see langword="null"/> when the group is gone.</returns>
    Task<long?> WriteEntryAsync(Guid groupId, MemberEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Removes a member's entry; what was merged stays in the document.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="memberId">The member.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    Task DeleteEntryAsync(Guid groupId, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a role assignment. Refused (<see langword="false"/>) when the member, the account or the secret already
    /// holds one in the group.
    /// </summary>
    /// <param name="assignment">The assignment.</param>
    /// <param name="secretHash">The device key's or organizer token's hash, if the principal has a secret.</param>
    /// <param name="secretCipher">The organizer token, encrypted, for an organizer link.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether it was added.</returns>
    Task<bool> AddAssignmentAsync(RoleAssignment assignment, string? secretHash, string? secretCipher, CancellationToken cancellationToken = default);

    /// <summary>Marks an invited assignment active.</summary>
    /// <param name="assignmentId">The assignment.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    Task ActivateAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Removes a role assignment, and the member's entry with it (merged content stays in the document).</summary>
    /// <param name="assignmentId">The assignment.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    Task RemoveAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken = default);

    /// <summary>Replaces an assignment's principal (take-over and hand-over move ownership between principals).</summary>
    /// <param name="assignment">The assignment with its new principal.</param>
    /// <param name="secretHash">The new principal's secret hash, if any.</param>
    /// <param name="secretCipher">The new principal's encrypted token, for an organizer link.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    Task ReplacePrincipalAsync(RoleAssignment assignment, string? secretHash, string? secretCipher, CancellationToken cancellationToken = default);

    /// <summary>Every role assignment an account holds, in any group.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The assignments.</returns>
    Task<IReadOnlyList<RoleAssignment>> ListAssignmentsOfAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Several groups at once (for an account's overview).</summary>
    /// <param name="groupIds">The groups.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The groups found.</returns>
    Task<IReadOnlyList<OnlineGroup>> GetManyAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default);

    /// <summary>Sets whether the join link lets people in; mints the link the first time.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="enabled">On or off.</param>
    /// <param name="newTokenHash">The hash of a token to mint when none is active.</param>
    /// <param name="newTokenCipher">That token, encrypted.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The active join link's encrypted token, or <see langword="null"/> when off.</returns>
    Task<string?> SetJoinLinkAsync(Guid groupId, bool enabled, string newTokenHash, string newTokenCipher, CancellationToken cancellationToken = default);

    /// <summary>Retires the active join link and activates a new one.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="newTokenHash">The new token's hash.</param>
    /// <param name="newTokenCipher">The new token, encrypted.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    Task RotateJoinLinkAsync(Guid groupId, string newTokenHash, string newTokenCipher, CancellationToken cancellationToken = default);

    /// <summary>The active join link's encrypted token, while the join link is on.</summary>
    /// <param name="groupId">The group.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The ciphertext, or <see langword="null"/>.</returns>
    Task<string?> GetJoinLinkCipherAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>The group an active join link belongs to, while its join link is on.</summary>
    /// <param name="tokenHash">The join token's hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The group id, or <see langword="null"/>.</returns>
    Task<Guid?> ResolveJoinLinkAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// The reveal tokens of a group's givers, minting any missing ones from <paramref name="mint"/>; tokens stay
    /// stable across re-draws.
    /// </summary>
    /// <param name="groupId">The group.</param>
    /// <param name="givers">The (event, giver) pairs that need a token.</param>
    /// <param name="mint">Mints a token: returns (token, hash, cipher).</param>
    /// <param name="decrypt">Decrypts a stored token.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The tokens.</returns>
    Task<IReadOnlyList<RevealToken>> EnsureRevealTokensAsync(
        Guid groupId,
        IReadOnlyCollection<(Guid EventId, Guid GiverMemberId)> givers,
        Func<(string Token, string Hash, string Cipher)> mint,
        Func<string, string> decrypt,
        CancellationToken cancellationToken = default);

    /// <summary>What a reveal token points at, if it exists.</summary>
    /// <param name="tokenHash">The token's hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The target, or <see langword="null"/>.</returns>
    Task<RevealTarget?> ResolveRevealTokenAsync(string tokenHash, CancellationToken cancellationToken = default);
}
