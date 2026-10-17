namespace CircleTools.Identity.Tokens;

/// <summary>One signed-in device in the list on <em>Security</em> (ADR-0079 amendment).</summary>
/// <param name="Id">The session (refresh chain) id — what "sign out this device" names.</param>
/// <param name="Device">The coarse device label recorded at sign-in; empty when unknown.</param>
/// <param name="SignedInAt">When the session started.</param>
/// <param name="LastActiveAt">When it last refreshed.</param>
/// <param name="IsCurrent">Whether it is the device asking.</param>
public sealed record SessionSummary(Guid Id, string Device, DateTimeOffset SignedInAt, DateTimeOffset LastActiveAt, bool IsCurrent);
