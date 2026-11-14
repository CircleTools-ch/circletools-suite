// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace CircleTools.SecretSanta.App.Platform;

/// <summary>
/// MAUI <see cref="IShareService"/> over <c>Microsoft.Maui.ApplicationModel.DataTransfer.Share</c>
/// (ADR-0028) — the device-head counterpart of <c>BrowserShareService</c>.
/// </summary>
/// <remarks>
/// A device always has a share sheet, so <see cref="IsSupportedAsync"/> is always <see langword="true"/>.
/// The link is carried in <see cref="ShareTextRequest.Text"/> (not <c>Uri</c>): the plain-text extra is
/// the field every target reads, so the reveal link reliably lands in the WhatsApp / SMS / e-mail body.
/// </remarks>
public sealed class MauiShareService : IShareService
{
    /// <inheritdoc />
    public ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(true);

    /// <inheritdoc />
    public async ValueTask<ShareOutcome> ShareAsync(
        string title,
        string text,
        string? url = null,
        CancellationToken cancellationToken = default)
    {
#if ANDROID
        // Android can tell whether a target was chosen (D5): a chooser with a callback.
        return await ShareChooser.ShareAsync(title, text);
#else
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = title,
            Text = text,
        });
        return ShareOutcome.Unknown;
#endif
    }
}
