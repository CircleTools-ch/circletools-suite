// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Ui;

/// <summary>
/// Holds the join token of an invitation while the invitee signs in to join with their account (Q7), so the
/// sign-in can return to the join page without carrying the token in the sign-in URL's query string — a
/// reload there would put it into the web server's access log. In memory only; gone with the app session.
/// </summary>
public sealed class PendingJoinLink
{
    /// <summary>Gets or sets the join token waiting for the sign-in to finish.</summary>
    public string? Token { get; set; }
}
