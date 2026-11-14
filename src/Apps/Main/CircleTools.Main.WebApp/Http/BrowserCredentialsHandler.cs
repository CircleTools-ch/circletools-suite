// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.ApiClient;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CircleTools.Main.WebApp.Http;

/// <summary>
/// Turns the platform-neutral <see cref="ApiRequestOptions.IncludeCredentials"/> marker into the
/// browser's <c>credentials: "include"</c>.
/// </summary>
/// <remarks>
/// <para>
/// Only the WebAuthn ceremonies set the marker. Identity keeps the challenge in a cookie between the
/// "options" call and the call that submits the credential, and a cross-origin <c>fetch</c> neither
/// sends nor stores cookies unless it asks to. The API answers with
/// <c>Access-Control-Allow-Credentials</c> for exactly this reason.
/// </para>
/// <para>
/// It is a handler rather than a call at each site so that the retry in
/// <c>AuthorizedHttpMessageHandler</c> gets it too: the retry is a clone, and a clone that lost this
/// would fail as a missing challenge — a symptom that points nowhere near its cause.
/// </para>
/// </remarks>
public sealed class BrowserCredentialsHandler : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Options.TryGetValue(ApiRequestOptions.IncludeCredentials, out var include) && include)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
