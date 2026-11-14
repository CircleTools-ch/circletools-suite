// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.Ui.Abstractions;
using Microsoft.JSInterop;

namespace CircleTools.Main.WebApp.Interop;

/// <summary>
/// Runs the WebAuthn ceremonies through <c>wwwroot/js/passkeys.js</c> (FR-010, FR-011).
/// </summary>
/// <remarks>
/// The module is imported on first use rather than at startup: most visits never touch a passkey,
/// and loading it eagerly would put a script fetch in front of the first render for everyone.
/// </remarks>
/// <param name="js">The JavaScript runtime.</param>
public sealed class BrowserPasskeyCeremony(IJSRuntime js) : IPasskeyCeremony, IAsyncDisposable
{
    private IJSObjectReference? _module;

    /// <inheritdoc />
    public async ValueTask<string?> CreateAsync(string optionsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await LoadAsync(cancellationToken);
            return await module.InvokeAsync<string?>("create", cancellationToken, optionsJson);
        }
        catch (JSException ex)
        {
            throw new PasskeyCeremonyException("The passkey could not be created.", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> AssertAsync(string optionsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await LoadAsync(cancellationToken);
            return await module.InvokeAsync<string?>("assert", cancellationToken, optionsJson);
        }
        catch (JSException ex)
        {
            throw new PasskeyCeremonyException("The passkey could not be used.", ex);
        }
    }

    /// <summary>Whether this browser can do passkeys at all.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> when the WebAuthn APIs are present.</returns>
    public async ValueTask<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var module = await LoadAsync(cancellationToken);
            return await module.InvokeAsync<bool>("isSupported", cancellationToken);
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is going away; there is no runtime left to release the module in.
        }
    }

    private async ValueTask<IJSObjectReference> LoadAsync(CancellationToken cancellationToken) =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/passkeys.js");
}
