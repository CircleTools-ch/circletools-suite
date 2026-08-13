namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// Why an account may not be deleted yet, and what would unblock it (FR-015).
/// </summary>
/// <param name="Code">
/// A stable code the client translates, in the same vocabulary as every other refusal in the suite
/// (ADR-0011). Prose cannot be translated by a DE/EN/FR client; a code can.
/// </param>
/// <param name="Title">The English fallback, for clients and logs that have no translation.</param>
/// <param name="Blockers">
/// The specific things standing in the way, named so the user can act on them rather than being
/// told only that something is wrong.
/// </param>
/// <remarks>
/// M1 shipped this as a bare <c>string?</c> reason. FEAT-003 needs more than a sentence: the guard
/// has to <em>name the circles</em> so the UI can offer transfer or deletion for each one, and the
/// reason has to survive translation. Changing the shape costs nothing because M1 deliberately
/// registered no guards.
/// </remarks>
public sealed record AccountDeletionRefusal(
    string Code,
    string Title,
    IReadOnlyList<AccountDeletionBlocker> Blockers);
