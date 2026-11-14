// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Ui.Account.Api;

/// <summary>
/// Identifies which app this host is, so account mail is branded — and its links point back — to the
/// app the user actually used (ADR-0064).
/// </summary>
/// <remarks>
/// Registered by <c>AddAccountUi</c> from each host's own id and stamped onto the outgoing account
/// requests by <see cref="AccountApiClient"/>. Absent (the two-argument test construction) leaves the
/// source app unset, so the server falls back to the default CircleTools branding.
/// </remarks>
/// <param name="AppId">A stable source-app id (e.g. <c>secretsanta</c>, <c>circletools</c>).</param>
public sealed record AccountAppContext(string AppId);
