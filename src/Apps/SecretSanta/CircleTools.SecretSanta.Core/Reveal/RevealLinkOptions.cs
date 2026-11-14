// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.SecretSanta.Core.Reveal;

/// <summary>
/// Host-supplied configuration for building reveal links. The host (web or MAUI) sets the base
/// URL of the reveal page so generated links point at the right origin.
/// </summary>
/// <param name="BaseUrl">The base URL the reveal route lives under (e.g. <c>https://example.org</c>).</param>
public sealed record RevealLinkOptions(string BaseUrl);
