// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CircleTools.Main.Service;

/// <summary>
/// Decides whether an SSO <c>return_uri</c> may receive an authorization code (FR-031, ADR-0039).
/// </summary>
/// <remarks>
/// This is the control against open-redirect and code exfiltration: the authority only ever hands a
/// code to an origin the suite already trusts. It reads the <strong>same</strong>
/// <c>Cors:AllowedOrigins</c> configuration the CORS policy in <c>Program</c> reads, so the allow-list
/// cannot drift from the origins already permitted to call the API (ADR-0013) — a second, hand-kept
/// list would be exactly the divergence a code-exfiltration hole hides in. In Development, loopback
/// origins are allowed for the same reason the CORS policy allows them: Aspire assigns proxy ports at
/// start-up that cannot be pinned in advance.
/// </remarks>
public sealed class ConnectReturnUriValidator
{
    private readonly HashSet<string> _allowedOrigins;
    private readonly bool _allowLoopback;

    /// <summary>Creates the validator from configuration and the host environment.</summary>
    /// <param name="configuration">Configuration holding <c>Cors:AllowedOrigins</c>.</param>
    /// <param name="environment">The host environment; loopback is allowed only in Development.</param>
    public ConnectReturnUriValidator(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        _allowedOrigins = new HashSet<string>(origins, StringComparer.OrdinalIgnoreCase);
        _allowLoopback = environment.IsDevelopment();
    }

    /// <summary>Tests whether a return URI's origin is on the suite allow-list.</summary>
    /// <param name="returnUri">The absolute URI the requesting app asked the code to be returned to.</param>
    /// <returns><see langword="true"/> only if the URI is absolute and its origin is trusted.</returns>
    public bool IsAllowed(string? returnUri)
    {
        if (string.IsNullOrWhiteSpace(returnUri)
            || !Uri.TryCreate(returnUri, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Compare origins (scheme + host + port) only; the code is appended as a query by the caller.
        var origin = uri.GetLeftPart(UriPartial.Authority);

        if (_allowedOrigins.Contains(origin))
        {
            return true;
        }

        return _allowLoopback && uri.IsLoopback;
    }
}
