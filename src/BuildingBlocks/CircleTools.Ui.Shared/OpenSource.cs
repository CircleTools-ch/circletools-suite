// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Ui.Shared;

/// <summary>
/// Where the license and the source code of CircleTools can be found (ADR-0099), shown on About &amp; legal.
/// </summary>
public static class OpenSource
{
    /// <summary>The license text, at the Apache Software Foundation.</summary>
    public const string LicenseUrl = "https://www.apache.org/licenses/LICENSE-2.0";

    /// <summary>The public repository holding this version's source code.</summary>
    public const string SourceCodeUrl = "https://github.com/CircleTools-ch/circletools-suite";

    /// <summary>
    /// Whether the repository at <see cref="SourceCodeUrl"/> is public, which shows the source link on
    /// About &amp; legal. It was <see langword="false"/> while the repository was private, so a released app
    /// never showed a link leading to a 404; it became <see langword="true"/> when the repository went
    /// public (ADR-0093, backlog OSS-5).
    /// </summary>
    public static bool IsSourceCodePublic => true;
}
