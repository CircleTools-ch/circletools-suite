using MauiDevices = Microsoft.Maui.Devices;
using AppHaptics = CircleTools.Ui.Abstractions.IHapticFeedback;

namespace CircleTools.SecretSanta.App.Platform;

/// <summary>
/// MAUI <see cref="IHapticFeedback"/> over <c>Microsoft.Maui.Devices.HapticFeedback</c> (ADR-0028,
/// FEAT-008 W3) — the device-head counterpart of <c>BrowserHapticFeedback</c>.
/// </summary>
/// <remarks>
/// Uses <see cref="MauiDevices.HapticFeedbackType.Click"/>, the short single tap (not the longer
/// long-press pattern). The call is marshalled onto the UI thread — the platform haptic needs the main
/// looper, and a Blazor event handler is not guaranteed to be on it (without this the buzz silently does
/// not fire, verified on a Pixel 9 Pro). Best-effort per the seam contract: a device without a vibrator
/// throws <see cref="FeatureNotSupportedException"/> and a missing permission throws
/// <see cref="PermissionException"/> — both are swallowed so a meaningful action never fails for the want
/// of a buzz. Android needs the <c>VIBRATE</c> permission (declared in the manifest).
/// </remarks>
public sealed class MauiHapticFeedback : AppHaptics
{
    /// <inheritdoc />
    public ValueTask TapAsync(CancellationToken cancellationToken = default)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                MauiDevices.HapticFeedback.Default.Perform(MauiDevices.HapticFeedbackType.Click);
            }
            catch (FeatureNotSupportedException)
            {
                // No vibrator on this device: garnish only, nothing to do.
            }
            catch (PermissionException)
            {
                // VIBRATE not granted: same — the action itself already happened.
            }
        });

        return ValueTask.CompletedTask;
    }
}
