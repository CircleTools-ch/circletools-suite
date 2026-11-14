// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.Ui.Test.TestSupport;

/// <summary>
/// Records how many times the haptic seam was tapped, so a page test can assert a meaningful action
/// produced a buzz (FEAT-008 W3). The production web head is itself a no-op
/// (<c>BrowserHapticFeedback</c>); this fake only adds the counter.
/// </summary>
public sealed class FakeHapticFeedback : IHapticFeedback
{
    /// <summary>The number of <see cref="TapAsync"/> calls so far.</summary>
    public int Taps { get; private set; }

    /// <inheritdoc />
    public ValueTask TapAsync(CancellationToken cancellationToken = default)
    {
        Taps++;
        return ValueTask.CompletedTask;
    }
}
