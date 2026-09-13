namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// How a person-claim on a shared-by-link group is remembered (ADR-0058, FEAT-013). A claim locks a
/// member to exactly one claimant; only an admin can release it.
/// </summary>
public enum PersonClaimKind
{
    /// <summary>The claim lives on one device only — no account behind it.</summary>
    Device,

    /// <summary>The claim is bound to a signed-in account, durable across that user's devices.</summary>
    Account,
}
