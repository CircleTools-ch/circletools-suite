using System.Security.Cryptography;
using System.Text;
using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Remembers which participants the organizer has already handed their reveal link to, per event, so the
/// guided hand-out (MNF-7, FR-045) can show "X of Y done · Z left" and be resumed days later. Progress is
/// a per-device convenience — where <em>this</em> device got to in handing links out — so it lives in the
/// plain local <see cref="IKeyValueStore"/> (like <see cref="RecentAccessService"/>), never synced.
/// </summary>
/// <remarks>
/// A person is keyed by a hash of their reveal link, and the whole draw by a hash of all links in order.
/// Because a re-draw mints new links, the draw signature changes and the stored progress no longer
/// matches — so a re-draw cleanly resets the hand-out (FR-045), with no explicit reset step. Every read
/// and write is best-effort: a storage failure yields empty progress rather than an error.
/// </remarks>
/// <param name="store">The device-local key/value store.</param>
public sealed class HandoutProgressService(IKeyValueStore store)
{
    /// <summary>The stable per-person key for a reveal link (used to mark and query one participant).</summary>
    /// <param name="url">The participant's reveal link.</param>
    /// <returns>A short stable hash of the link.</returns>
    public static string MemberKey(string url) => Hash(url);

    /// <summary>
    /// The set of member keys already handed out for the <em>current</em> draw. Empty when nothing has been
    /// handed out or when the draw has changed since (a re-draw resets progress).
    /// </summary>
    /// <param name="groupId">The owning group.</param>
    /// <param name="eventId">The occasion.</param>
    /// <param name="urls">The current reveal links, in order (defines the draw signature).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The handed-out member keys for the current draw.</returns>
    public async ValueTask<HashSet<string>> GetDoneAsync(
        Guid groupId, Guid eventId, IReadOnlyList<string> urls, CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await store.GetAsync(StorageScope.Durable, Key(groupId, eventId), cancellationToken);
            if (raw is null)
            {
                return [];
            }

            var lines = raw.Split('\n');
            // First line is the draw signature; a mismatch means the draw was redone → progress is stale.
            if (lines.Length == 0 || lines[0] != Signature(urls))
            {
                return [];
            }

            return lines.Skip(1).Where(line => line.Length > 0).ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Marks (or unmarks) one participant as handed out for the current draw.</summary>
    /// <param name="groupId">The owning group.</param>
    /// <param name="eventId">The occasion.</param>
    /// <param name="urls">The current reveal links, in order.</param>
    /// <param name="memberKey">The participant's key (see <see cref="MemberKey"/>).</param>
    /// <param name="done"><see langword="true"/> to mark handed out; <see langword="false"/> to clear.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The updated handed-out set.</returns>
    public async ValueTask<HashSet<string>> SetDoneAsync(
        Guid groupId,
        Guid eventId,
        IReadOnlyList<string> urls,
        string memberKey,
        bool done,
        CancellationToken cancellationToken = default)
    {
        var current = await GetDoneAsync(groupId, eventId, urls, cancellationToken);
        if (done)
        {
            current.Add(memberKey);
        }
        else
        {
            current.Remove(memberKey);
        }

        try
        {
            var value = string.Join('\n', new[] { Signature(urls) }.Concat(current));
            await store.SetAsync(StorageScope.Durable, Key(groupId, eventId), value, cancellationToken);
        }
        catch
        {
            // Best-effort: a failed write just means the ✓ is not remembered next time.
        }

        return current;
    }

    private static string Key(Guid groupId, Guid eventId) => $"secretsanta.handout.{groupId}.{eventId}";

    private static string Signature(IReadOnlyList<string> urls) => Hash(string.Join("|", urls));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
}
