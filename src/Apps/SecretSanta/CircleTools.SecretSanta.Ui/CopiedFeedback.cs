// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// How long a copy button reads "Copied!" before it is back to "Copy" (BTP-9, 2026-10-30). The label used to
/// stay until the page was left, so a second copy of the same link looked like nothing had happened.
/// </summary>
public static class CopiedFeedback
{
    /// <summary>How long the confirmation shows.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(2);

    /// <summary>Waits out the confirmation, then tells whether it is still the one to clear.</summary>
    /// <param name="time">The clock; a fake one in tests.</param>
    /// <param name="copied">The copied link, read when the wait is over.</param>
    /// <param name="url">The link this confirmation was for.</param>
    /// <returns><see langword="true"/> when no other link was copied meanwhile and the label should reset.</returns>
    public static async Task<bool> ExpireAsync(TimeProvider time, Func<string?> copied, string url)
    {
        ArgumentNullException.ThrowIfNull(copied);

        await Task.Delay(Duration, time);
        return copied() == url;
    }
}
