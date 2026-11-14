// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace CircleTools.SecretSanta.App.Platform;

/// <summary>
/// MAUI <see cref="IFileSaver"/> over the device share sheet (ADR-0028) — the device-head counterpart
/// of <c>BrowserFileSaver</c>.
/// </summary>
/// <remarks>
/// The export (FR-014) has to end up somewhere the user controls, which on a device is the share sheet:
/// the file is written to the cache directory and handed to
/// <c>Microsoft.Maui.ApplicationModel.DataTransfer.Share</c>, from where the user keeps it in Files,
/// Drive, or sends it to themselves. <see cref="IFileSaver"/> explicitly allows a device head to offer a
/// share sheet rather than a silent download.
/// </remarks>
public sealed class MauiFileSaver : IFileSaver
{
    /// <inheritdoc />
    public async ValueTask SaveAsync(
        string fileName,
        string contentType,
        string content,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(path, content, cancellationToken);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = fileName,
            File = new ShareFile(path, contentType),
        });
    }
}
