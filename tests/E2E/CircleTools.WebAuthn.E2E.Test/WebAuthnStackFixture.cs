// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net.Http;

namespace CircleTools.WebAuthn.E2E.Test;

/// <summary>
/// Boots the whole CircleTools stack for the browser end-to-end tests (arc42 D4, ADR-0045): the
/// Aspire AppHost is launched as a child process on its <c>https</c> launch profile, which brings up
/// Postgres, Mailpit, the Main API (<c>https://localhost:7180</c>) and the Main web client
/// (<c>https://localhost:7080</c>) on the fixed ports the WASM client's baked-in
/// <c>ApiBaseUrl</c>/<c>SsoAuthorityUrl</c> require.
/// </summary>
/// <remarks>
/// <para>
/// The process approach is deliberate. <c>Aspire.Hosting.Testing</c> is the idiomatic way to boot an
/// AppHost from a test, but it assigns <em>dynamic</em> endpoint ports; the standalone WASM client
/// reads its API origin from a static <c>appsettings.json</c> pinned to <c>https://localhost:7180</c>,
/// so the stack must bind those exact ports. The <c>https</c> launch profile does; a dynamic-port
/// host would leave the browser calling an origin nothing is listening on.
/// </para>
/// <para>
/// Requires a container runtime (Docker) and an HTTPS dev certificate so Kestrel can bind
/// <c>https</c> — CI runs <c>dotnet dev-certs https</c>; Playwright ignores the cert's trust chain.
/// Where neither is available the fixture fails loudly rather than skipping: an E2E suite that
/// quietly does nothing is worse than one that is red.
/// </para>
/// </remarks>
public sealed class WebAuthnStackFixture : IAsyncLifetime
{
    /// <summary>The Main web client (Blazor WASM) origin — where the browser drives the UI.</summary>
    public const string WebOrigin = "https://localhost:7080";

    /// <summary>The Main API origin the client calls across origins during the ceremony.</summary>
    public const string ApiOrigin = "https://localhost:7180";

    /// <summary>Mailpit's HTTP API, for reading the account-verification link out of band.</summary>
    public const string MailpitApi = "http://localhost:8025";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(3);

    private Process? _appHost;

    /// <summary>An <see cref="HttpClient"/> that ignores the dev certificate, for the API assertions.</summary>
    public HttpClient Api { get; } = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    })
    {
        BaseAddress = new Uri(ApiOrigin),
    };

    async Task IAsyncLifetime.InitializeAsync()
    {
        var repoRoot = FindRepoRoot();

        _appHost = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project src/Aspire/CircleTools.AppHost --launch-profile https",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Could not start the Aspire AppHost.");

        // Surface the host's output to the test log so a failed boot is diagnosable.
        _appHost.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Console.WriteLine($"[apphost] {e.Data}");
            }
        };
        _appHost.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Console.WriteLine($"[apphost:err] {e.Data}");
            }
        };
        _appHost.BeginOutputReadLine();
        _appHost.BeginErrorReadLine();

        await WaitForStackAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Api.Dispose();

        if (_appHost is { HasExited: false })
        {
            // Kills the AppHost and every service process it spawned. Aspire's own containers
            // (Postgres, Mailpit) survive a SIGKILL, so they are cleaned up explicitly below.
            try { _appHost.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await _appHost.WaitForExitAsync();
        }

        _appHost?.Dispose();
        RemoveAspireContainers();
    }

    private async Task WaitForStackAsync()
    {
        using var probe = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        })
        {
            Timeout = TimeSpan.FromSeconds(5),
        };

        var deadline = DateTime.UtcNow + ReadyTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_appHost is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"The Aspire AppHost exited early (code {_appHost.ExitCode}) before the stack was ready.");
            }

            if (await IsUpAsync(probe, $"{WebOrigin}/") && await IsUpAsync(probe, $"{ApiOrigin}/health"))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(3));
        }

        throw new TimeoutException($"The stack was not ready within {ReadyTimeout.TotalSeconds:N0}s.");
    }

    private static async Task<bool> IsUpAsync(HttpClient probe, string url)
    {
        try
        {
            var response = await probe.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    // Aspire tags every container it starts with the "aspire-resource-name" label, so this removes
    // exactly the Postgres/Mailpit this run created and nothing else on the machine.
    private static void RemoveAspireContainers()
    {
        try
        {
            var list = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "ps -aq --filter label=aspire-resource-name",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });

            if (list is null)
            {
                return;
            }

            var ids = list.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            list.WaitForExit();

            foreach (var id in ids)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"rm -f {id}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                })?.WaitForExit();
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Docker not on PATH (nothing to clean) — the process-tree kill already handled the app.
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CircleTools.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repository root (CircleTools.slnx).");
    }
}

/// <summary>Shares one running stack across the browser end-to-end tests.</summary>
[CollectionDefinition(CollectionName)]
public sealed class WebAuthnE2E : ICollectionFixture<WebAuthnStackFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "webauthn-stack";
}
