// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

namespace CircleTools.ApiClient;

/// <summary>Markers a caller can attach to a request for handlers further down the chain.</summary>
public static class ApiRequestOptions
{
    /// <summary>
    /// Marks a request that must carry cookies cross-origin.
    /// </summary>
    /// <remarks>
    /// Only the WebAuthn ceremonies need this: ASP.NET Core Identity keeps the challenge in a cookie
    /// between the "options" call and the call that submits the credential, and a cross-origin fetch
    /// drops cookies unless it asks for them. The marker is deliberately platform-neutral — the
    /// browser host translates it into <c>credentials: "include"</c>, so this assembly and the pages
    /// that set it stay free of WebAssembly-specific types.
    /// </remarks>
    public static HttpRequestOptionsKey<bool> IncludeCredentials { get; } = new("CircleTools.IncludeCredentials");

    /// <summary>
    /// Marks a request that must not be retried after a token refresh — the refresh call itself.
    /// </summary>
    public static HttpRequestOptionsKey<bool> SkipAuthorization { get; } = new("CircleTools.SkipAuthorization");
}
