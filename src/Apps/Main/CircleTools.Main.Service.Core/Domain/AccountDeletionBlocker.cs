namespace CircleTools.Main.Service.Core.Domain;

/// <summary>
/// One thing standing between an account and its deletion, with the way to clear it (FR-015).
/// </summary>
/// <param name="Id">Its identity, so the UI can link straight to it instead of making the user hunt.</param>
/// <param name="Name">Its name, as the user knows it.</param>
/// <param name="Resolution">
/// A stable code naming what would clear this blocker — for circles,
/// <c>owner_must_transfer_ownership</c> or <c>owner_must_delete_circle</c>, the same two codes the
/// circle endpoints already use when an owner tries to leave (FR-021). One vocabulary, translated
/// once.
/// </param>
public sealed record AccountDeletionBlocker(Guid Id, string Name, string Resolution);
