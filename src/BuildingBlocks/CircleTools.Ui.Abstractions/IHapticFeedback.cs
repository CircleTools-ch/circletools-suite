namespace CircleTools.Ui.Abstractions;

/// <summary>
/// Produces a short confirmation buzz where the platform has a vibrator — the small tactile
/// acknowledgement a native app gives on a meaningful action (running a draw, revealing a giftee,
/// pinning or deleting).
/// </summary>
/// <remarks>
/// <para>
/// One of the platform seams (ADR-0028, FEAT-008 W1/W3): the MAUI head wraps
/// <c>Microsoft.Maui.Devices.HapticFeedback</c>; the browser head is a deliberate no-op (the web
/// mobile head does not buzz — mirrors how <see cref="IShareService"/> self-hides where there is no
/// share sheet). Everything above it — the pages that call <see cref="TapAsync"/> — is shared feature
/// code that never knows which head it runs on.
/// </para>
/// <para>
/// It is pure garnish: best-effort and silent. A device with no vibrator, a denied permission, or the
/// no-op web head all complete without error and without changing anything the user can see. Nothing
/// above this seam depends on a buzz actually happening.
/// </para>
/// </remarks>
public interface IHapticFeedback
{
    /// <summary>Performs a single short confirmation tap, where the platform supports one.</summary>
    /// <remarks>Best-effort: completes normally even when no haptic is produced.</remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes once the tap has been requested.</returns>
    ValueTask TapAsync(CancellationToken cancellationToken = default);
}
