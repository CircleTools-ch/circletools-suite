// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.Ui.Shared;

/// <summary>
/// One "get the app" link shown on an app's web landing page: a native-store or TestFlight/beta URL for
/// a platform. Like <see cref="SuiteAppLink"/> it is built from host configuration (the
/// <c>AppStoreLinks</c> section) and injected as an enumerable, so a host that registers none — every
/// MAUI head, and the web head until the store listings exist — resolves an empty set and the
/// <c>AppStoreLinks</c> section self-hides (ADR-0065). Showing store links inside the installed app would
/// make no sense, which the web-only registration already prevents.
/// </summary>
/// <param name="Platform">
/// Which store the <see cref="Url"/> points at, matched case-insensitively: <c>ios</c> / <c>apple</c> /
/// <c>appstore</c> / <c>testflight</c> pick the Apple presentation, anything else the Android/Play one.
/// </param>
/// <param name="Url">The absolute store, TestFlight or beta URL to open (in a new tab).</param>
/// <param name="Label">
/// Optional label override. When empty, the component shows the localized default for the platform
/// ("Download on the App Store" / "Get it on Google Play"). Useful during the test phase to say, e.g.,
/// "Join the iOS beta (TestFlight)".
/// </param>
public sealed record AppStoreLink(string Platform, string Url, string? Label = null);
