namespace CircleTools.Ui.Abstractions;

/// <summary>
/// Hands a short piece of text (and optionally a link) to whatever the platform calls "share" — the
/// OS share sheet a user picks WhatsApp, Messages or e-mail from.
/// </summary>
/// <remarks>
/// <para>
/// One of the platform seams (ADR-0028): the browser goes through the Web Share API
/// (<c>navigator.share</c>) in <c>wwwroot/js/share.js</c>, the MAUI head wraps
/// <c>Microsoft.Maui.ApplicationModel.DataTransfer.Share</c>. Everything above it — the Share page that
/// offers a reveal link — is shared feature code that never knows which head it runs on.
/// </para>
/// <para>
/// It is a convenience, never the only path. A copy-to-clipboard action stays available for every
/// platform, so a browser without the Web Share API (most desktops) simply doesn't show the Share
/// button; nothing depends on the sheet being present.
/// </para>
/// </remarks>
public interface IShareService
{
    /// <summary>Whether this platform can present a native share sheet at all.</summary>
    /// <remarks>
    /// Always <see langword="true"/> on the device heads; on the browser it reflects whether the Web
    /// Share API is present (mobile browsers and installed PWAs — rarely desktop). Callers use it to
    /// decide whether to show a Share button, falling back to plain copy when it is <see langword="false"/>.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> when <see cref="ShareAsync"/> can present a share sheet.</returns>
    ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default);

    /// <summary>Presents the platform share sheet for the given content.</summary>
    /// <remarks>
    /// Best-effort: dismissing the sheet is a normal outcome and completes without error. For maximum
    /// reach the caller should include any link inside <paramref name="text"/> as well, since some share
    /// targets read only the message body; <paramref name="url"/> is an extra hint for platforms (like the
    /// Web Share API) that render a link preview.
    /// </remarks>
    /// <param name="title">A short title for the shared item (used by some targets, e.g. the e-mail subject).</param>
    /// <param name="text">The message body to share. Include the link here too for the widest reach.</param>
    /// <param name="url">An optional link, surfaced to platforms that treat it specially.</param>
    /// <param name="cancellationToken">Cancels the share.</param>
    /// <returns>A task that completes once the sheet has been shown (and dismissed or acted on).</returns>
    ValueTask ShareAsync(
        string title,
        string text,
        string? url = null,
        CancellationToken cancellationToken = default);
}
