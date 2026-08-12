using AppSuite.ApiClient;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace AppSuite.SecretSanta.WebApp.Http;

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
/// A host-local copy of the Main app's handler (ADR-0028 kept the browser adapters host-local until a
/// second host needed them; this is that second host — a later slice may lift the shareable ones into
/// a browser-adapter block, but this one depends on <c>ApiClient</c> and Blazor WebAssembly, which the
/// <c>Ui.Web</c> block's fitness rules forbid).
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
