namespace CircleTools.Main.Service.Repository;

/// <summary>
/// One app aggregate as the sync store keeps it: a versioned, account-scoped opaque JSON document.
/// Platform never deserializes <see cref="Body"/> — it is the Secret-Santa group (or profile)
/// document, passed through untouched (ADR-0016, ADR-0033).
/// </summary>
public sealed class SyncAggregate
{
    /// <summary>The owning account (a value, never a foreign key to Identity — see ADR-0019).</summary>
    public Guid AccountId { get; set; }

    /// <summary>The client-minted aggregate identity (ADR-0008).</summary>
    public Guid AggregateId { get; set; }

    /// <summary>The aggregate kind — <c>"group"</c> or <c>"profile"</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The aggregate's full body as opaque JSON (stored as <c>jsonb</c>).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>The per-aggregate version the client bumps on each local edit.</summary>
    public long Version { get; set; }

    /// <summary>The per-account monotonic sequence assigned when this state was applied.</summary>
    public long ServerSeq { get; set; }

    /// <summary>The server-clock instant this state was applied — the last-write-wins ordering key.</summary>
    public DateTimeOffset ServerModifiedAt { get; set; }
}

/// <summary>Whether a marker records a deletion or a removal-from-account.</summary>
public enum SyncMarkerType
{
    /// <summary>The aggregate was deleted; it must disappear on every device (FR-024).</summary>
    Tombstone,

    /// <summary>The group left the account; devices drop the account scope but keep the local copy (FR-026).</summary>
    Unclaim,
}

/// <summary>
/// A tombstone or un-claim marker: the record that an aggregate is gone (or has left the account)
/// so the fact can propagate. Carries no body — the two dispositions must never be the same signal
/// (FEAT-004).
/// </summary>
public sealed class SyncMarker
{
    /// <summary>The owning account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>The aggregate the marker refers to.</summary>
    public Guid AggregateId { get; set; }

    /// <summary>The aggregate kind the marker applies to.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The version at the point the marker was written (so a later re-creation is not re-deleted).</summary>
    public long Version { get; set; }

    /// <summary>The per-account monotonic sequence assigned when the marker was written.</summary>
    public long ServerSeq { get; set; }

    /// <summary>Whether this is a deletion or a removal-from-account.</summary>
    public SyncMarkerType MarkerType { get; set; }
}

/// <summary>
/// One row per device that has synced an account. Retention prunes markers only below the slowest
/// active device's cursor, so this registry is what keeps a delete from resurrecting on a device that
/// never saw it (FEAT-004).
/// </summary>
public sealed class SyncDevice
{
    /// <summary>The owning account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>The device's stable identifier, as the client mints it.</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>The highest sequence this device has pulled — how far it has caught up.</summary>
    public long LastCursor { get; set; }

    /// <summary>When the device last ran a cycle, so a device gone quiet can be aged out of retention.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}

/// <summary>
/// The per-account sequence counter. A dedicated row lets a change be assigned a monotonic
/// <c>server_seq</c> under an atomic upsert, so two concurrent cycles on one account never mint the
/// same sequence (FEAT-004 — "cursor &amp; ordering").
/// </summary>
public sealed class SyncAccountCounter
{
    /// <summary>The account this counter belongs to.</summary>
    public Guid AccountId { get; set; }

    /// <summary>The highest sequence handed out so far for this account.</summary>
    public long NextSeq { get; set; }
}
