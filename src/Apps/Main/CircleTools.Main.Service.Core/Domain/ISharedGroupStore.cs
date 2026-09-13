using System.Text.Json;
using CircleTools.Main.Contracts;

namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// The server side of the account-free, shared-by-link storage tier (ADR-0058, FEAT-013): a group
/// stored on the server keyed by an unguessable <em>share token</em> rather than by an account, and
/// reached by an admin or join link.
/// </summary>
/// <remarks>
/// Like <see cref="ISyncStore"/> this is Platform: the group body is an opaque, versioned JSON
/// document the store keeps and returns without understanding the Secret-Santa schema. What differs is
/// authorization — the token, not a bearer identity, is the credential — and the presence of an
/// optional owning account (set when someone claims admin). Structural abuse limits (NFR-009) are the
/// only inspection the store makes of the body.
/// </remarks>
public interface ISharedGroupStore
{
    /// <summary>Uploads a group into shared-by-link storage and mints its admin + join tokens.</summary>
    /// <param name="request">The group id, version and opaque body.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// The minted links, or <see langword="null"/> when the body exceeds the NFR-009 abuse limits.
    /// Re-creating a group already shared returns its current active links (idempotent share).
    /// </returns>
    Task<MintedLinks?> CreateAsync(CreateSharedGroupRequest request, CancellationToken cancellationToken = default);

    /// <summary>Resolves a token to the group it addresses and the capability it grants.</summary>
    /// <param name="token">The admin or join token from a link.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// The group view, or <see langword="null"/> when the token does not resolve — a wrong, disabled,
    /// or never-existed token are indistinguishable (no oracle). A retired admin token resolves to a
    /// view with <see cref="SharedGroupView.AdminRetired"/> set, so the client can explain it.
    /// </returns>
    Task<SharedGroupView?> GetAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Pushes a new body for the group an admin token addresses (last-write-wins by server clock).</summary>
    /// <param name="token">The token; must grant the admin capability and not be retired.</param>
    /// <param name="version">The version the client holds — its half of last-write-wins.</param>
    /// <param name="body">The new opaque body.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The write outcome (applied / collision / not-found / forbidden / too-large).</returns>
    Task<SharedWriteResult> PushAsync(string token, long version, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>Disables the current admin token for a group and mints a fresh one (the previous stops resolving).</summary>
    /// <param name="adminToken">The current admin token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The new admin token, or <see langword="null"/> when the token does not grant admin.</returns>
    Task<RotatedAdminLink?> RotateAdminAsync(string adminToken, CancellationToken cancellationToken = default);

    /// <summary>Deletes the online copy of a group and all its tokens (owner-less delete, NFR-010).</summary>
    /// <param name="adminToken">The current admin token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> when a group was deleted; <see langword="false"/> when the token does not grant admin.</returns>
    Task<bool> DeleteAsync(string adminToken, CancellationToken cancellationToken = default);
}

/// <summary>Why a push to a shared-by-link group ended as it did.</summary>
public enum SharedWriteStatus
{
    /// <summary>The push was stored (a clean fast-forward).</summary>
    Applied,

    /// <summary>The push arrived later by server clock and won, discarding an older concurrent body in full (ADR-0016).</summary>
    Collision,

    /// <summary>The token did not resolve to a group.</summary>
    NotFound,

    /// <summary>The token does not grant the admin capability, or has been retired by an account admin-claim.</summary>
    Forbidden,

    /// <summary>The body exceeded the NFR-009 abuse limits and was rejected; nothing was stored.</summary>
    TooLarge,
}

/// <summary>The outcome of <see cref="ISharedGroupStore.PushAsync"/>.</summary>
/// <param name="Status">How the push ended.</param>
/// <param name="Version">The version now stored (0 when nothing was stored).</param>
public sealed record SharedWriteResult(SharedWriteStatus Status, long Version);
