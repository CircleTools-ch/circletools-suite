// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.App.Platform;

/// <summary>
/// The device head's <see cref="IAppForeground"/> (ADR-0044 amendment 2026-10-15): fed by the MAUI window's
/// Resumed / Stopped events, which <see cref="App"/> forwards through <see cref="AppForegroundSignal"/>. A
/// WebView does not reliably report page visibility when the app goes to the background, so the native
/// lifecycle is the source of truth here.
/// </summary>
public sealed class MauiForeground : IAppForeground, IDisposable
{
    private bool _subscribed;

    /// <inheritdoc />
    public bool IsForeground { get; private set; } = true;

    /// <inheritdoc />
    public event Action<bool>? Changed;

    /// <inheritdoc />
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!_subscribed)
        {
            _subscribed = true;
            AppForegroundSignal.Changed += OnChanged;
        }

        return ValueTask.CompletedTask;
    }

    private void OnChanged(bool foreground)
    {
        if (foreground == IsForeground)
        {
            return;
        }

        IsForeground = foreground;
        Changed?.Invoke(foreground);
    }

    /// <inheritdoc />
    public void Dispose() => AppForegroundSignal.Changed -= OnChanged;
}

/// <summary>
/// The bridge from the MAUI window lifecycle (owned by <see cref="App"/>, outside the Blazor scope) to the
/// scoped <see cref="MauiForeground"/> inside it.
/// </summary>
public static class AppForegroundSignal
{
    /// <summary>Raised with <see langword="true"/> when the window resumes, <see langword="false"/> when it stops.</summary>
    public static event Action<bool>? Changed;

    /// <summary>Raises <see cref="Changed"/>.</summary>
    /// <param name="foreground">Whether the app is now in the foreground.</param>
    public static void Raise(bool foreground) => Changed?.Invoke(foreground);
}
