using System.Text;
using System.Text.Json;
using CircleTools.Main.Contracts;
using CircleTools.Main.Service.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CircleTools.Main.Service.Repository;

/// <summary>
/// EF Core implementation of <see cref="ISharedGroupStore"/> over the <c>shared</c> schema
/// (ADR-0058, FEAT-013). The group body is opaque; last-write-wins is by the server clock, mirroring
/// <see cref="SyncStore"/>. The token is the credential — the store never reads a bearer identity.
/// </summary>
/// <param name="context">The shared-group database context.</param>
/// <param name="tokens">The share-token factory (NFR-009 entropy).</param>
/// <param name="timeProvider">The server clock — the only ordering authority.</param>
/// <param name="limits">The abuse-resistance limits (NFR-009).</param>
public sealed class SharedGroupStore(
    SharedGroupDbContext context,
    IShareTokenFactory tokens,
    TimeProvider timeProvider,
    IOptions<SharedGroupLimits> limits) : ISharedGroupStore
{
    private readonly SharedGroupLimits _limits = limits.Value;

    /// <inheritdoc />
    public async Task<MintedLinks?> CreateAsync(
        CreateSharedGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!WithinLimits(request.Body))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var bodyText = request.Body.GetRawText();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var existing = await context.Groups
            .FindAsync([request.GroupId], cancellationToken).ConfigureAwait(false);

        // Sharing a group that is already shared is idempotent: return its current active links rather
        // than duplicating the row (a retried ShareByLink, or a device re-uploading).
        if (existing is not null)
        {
            var activeTokens = await context.Tokens
                .Where(t => t.GroupId == request.GroupId && t.Active)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var admin = activeTokens.FirstOrDefault(t => t.Capability == SharedCapability.Admin);
            var join = activeTokens.FirstOrDefault(t => t.Capability == SharedCapability.Join);
            if (admin is not null && join is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new MintedLinks(request.GroupId, admin.Token, join.Token);
            }
        }

        if (existing is null)
        {
            context.Groups.Add(new SharedGroupRow
            {
                GroupId = request.GroupId,
                Body = bodyText,
                Version = request.Version,
                ServerModifiedAt = now,
                OwnerAccountId = null,
                CreatedAt = now,
                LastActivityAt = now,
            });
        }

        var adminToken = tokens.Create();
        var joinToken = tokens.Create();
        context.Tokens.Add(NewToken(adminToken, request.GroupId, SharedCapability.Admin, now));
        context.Tokens.Add(NewToken(joinToken, request.GroupId, SharedCapability.Join, now));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new MintedLinks(request.GroupId, adminToken, joinToken);
    }

    /// <inheritdoc />
    public async Task<SharedGroupView?> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var tokenRow = await context.Tokens
            .FindAsync([token], cancellationToken).ConfigureAwait(false);
        if (tokenRow is null)
        {
            return null;
        }

        var group = await context.Groups
            .FindAsync([tokenRow.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return null;
        }

        if (!tokenRow.Active)
        {
            // A retired admin link (an account claimed admin) resolves to a retired view so the holder
            // learns who took over; any other inactive token (rotated) is indistinguishable from
            // never-existed — no oracle.
            if (tokenRow is { RetiredByClaim: true, Capability: SharedCapability.Admin })
            {
                return new SharedGroupView(
                    group.GroupId, group.Version, ParseBody(group.Body),
                    SharedCapability.Admin, AdminRetired: true, group.AdminClaimedByName);
            }

            return null;
        }

        // Any access keeps the group alive for retention (NFR-010).
        group.LastActivityAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SharedGroupView(
            group.GroupId, group.Version, ParseBody(group.Body),
            tokenRow.Capability, AdminRetired: false, AdminClaimedByName: null);
    }

    /// <inheritdoc />
    public async Task<SharedWriteResult> PushAsync(
        string token,
        long version,
        JsonElement body,
        CancellationToken cancellationToken = default)
    {
        if (!WithinLimits(body))
        {
            return new SharedWriteResult(SharedWriteStatus.TooLarge, 0);
        }

        var tokenRow = await context.Tokens
            .FindAsync([token], cancellationToken).ConfigureAwait(false);
        if (tokenRow is null)
        {
            return new SharedWriteResult(SharedWriteStatus.NotFound, 0);
        }

        // Only an active admin token may write. A retired or rotated admin token, and any join token,
        // is forbidden.
        if (!tokenRow.Active || tokenRow.Capability != SharedCapability.Admin)
        {
            return new SharedWriteResult(SharedWriteStatus.Forbidden, 0);
        }

        var now = timeProvider.GetUtcNow();
        var bodyText = body.GetRawText();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var group = await context.Groups
            .FindAsync([tokenRow.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SharedWriteResult(SharedWriteStatus.NotFound, 0);
        }

        var collided = version <= group.Version;

        // Last-write-wins: a clean fast-forward moves the version to the pushed one; a concurrent push
        // arrives later by the server clock and wins in full, and the stored body is discarded in full
        // (ADR-0016). Either way the incoming body is what remains.
        group.Body = bodyText;
        group.Version = Math.Max(version, group.Version);
        group.ServerModifiedAt = now;
        group.LastActivityAt = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new SharedWriteResult(
            collided ? SharedWriteStatus.Collision : SharedWriteStatus.Applied,
            group.Version);
    }

    /// <inheritdoc />
    public async Task<RotatedAdminLink?> RotateAdminAsync(
        string adminToken,
        CancellationToken cancellationToken = default)
    {
        var tokenRow = await context.Tokens
            .FindAsync([adminToken], cancellationToken).ConfigureAwait(false);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Admin })
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var group = await context.Groups
            .FindAsync([tokenRow.GroupId], cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            // An active token with no group is an invariant violation; treat it as unresolvable.
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        // Deactivate every currently-active admin token for the group (a rotation, not a claim), then
        // mint a fresh one. Old admin links stop resolving immediately.
        var activeAdmins = await context.Tokens
            .Where(t => t.GroupId == tokenRow.GroupId && t.Active && t.Capability == SharedCapability.Admin)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var admin in activeAdmins)
        {
            admin.Active = false;
            admin.RetiredByClaim = false;
        }

        var fresh = tokens.Create();
        context.Tokens.Add(NewToken(fresh, tokenRow.GroupId, SharedCapability.Admin, now));
        group.LastActivityAt = now;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return new RotatedAdminLink(fresh);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string adminToken, CancellationToken cancellationToken = default)
    {
        var tokenRow = await context.Tokens
            .FindAsync([adminToken], cancellationToken).ConfigureAwait(false);
        if (tokenRow is not { Active: true, Capability: SharedCapability.Admin })
        {
            return false;
        }

        var groupId = tokenRow.GroupId;

        await using var transaction = await context.Database
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await context.MemberClaims.Where(c => c.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.Tokens.Where(t => t.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await context.Groups.Where(g => g.GroupId == groupId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static SharedGroupToken NewToken(string token, Guid groupId, SharedCapability capability, DateTimeOffset now) =>
        new()
        {
            Token = token,
            GroupId = groupId,
            Capability = capability,
            Active = true,
            RetiredByClaim = false,
            CreatedAt = now,
        };

    private bool WithinLimits(JsonElement body)
    {
        // Schema-light abuse caps (NFR-009): a hard byte ceiling, plus a roster ceiling checked only
        // when a members array is present. The store stays opaque otherwise.
        var bytes = Encoding.UTF8.GetByteCount(body.GetRawText());
        if (bytes > _limits.MaxBodyBytes)
        {
            return false;
        }

        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("members", out var members)
            && members.ValueKind == JsonValueKind.Array
            && members.GetArrayLength() > _limits.MaxMembers)
        {
            return false;
        }

        return true;
    }

    private static JsonElement ParseBody(string bodyJson)
    {
        using var document = JsonDocument.Parse(bodyJson);
        return document.RootElement.Clone();
    }
}
