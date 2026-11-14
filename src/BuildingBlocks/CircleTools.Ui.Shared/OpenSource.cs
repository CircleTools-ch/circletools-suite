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
    /// Whether the repository at <see cref="SourceCodeUrl"/> is public yet. Until it is (ADR-0093, backlog
    /// OSS-5), the source link is hidden so a released app never shows a link that leads to a 404. Set it
    /// to <see langword="true"/> in the same change that makes the repository public.
    /// </summary>
    public static bool IsSourceCodePublic => false;
}
