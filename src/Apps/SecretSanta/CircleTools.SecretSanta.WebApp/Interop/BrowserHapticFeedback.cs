using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.WebApp.Interop;

/// <summary>
/// The browser <see cref="IHapticFeedback"/> — a deliberate no-op (ADR-0028, FEAT-008 W3). The web
/// mobile head does not buzz; haptics are a native-shell affordance, so the browser simply does
/// nothing, the same way <see cref="BrowserShareService"/> falls back to Copy where there is no share
/// sheet.
/// </summary>
/// <remarks>
/// No JS interop and no exceptions, so a page that taps on a meaningful action behaves identically on
/// the web — it just produces no vibration.
/// </remarks>
public sealed class BrowserHapticFeedback : IHapticFeedback
{
    /// <inheritdoc />
    public ValueTask TapAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
