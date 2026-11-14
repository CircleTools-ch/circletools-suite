// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace CircleTools.SecretSanta.E2E.Test;

/// <summary>
/// Boots the whole CircleTools stack for the Secret-Santa browser end-to-end tests (ADR-0045,
/// FEAT-013): the Aspire AppHost is launched on its <c>https</c> launch profile, bringing up Postgres,
/// the Main API (<c>https://localhost:7180</c>) and the standalone Secret-Santa WASM app
/// (<c>https://localhost:7081</c>) on the fixed ports the WASM client's baked-in origins require.
/// </summary>
/// <remarks>
/// Mirrors the WebAuthn E2E fixture. Requires Docker and an HTTPS dev certificate; where neither is
/// available the fixture fails loudly rather than skipping.
/// </remarks>
public sealed class SecretSantaStackFixture : IAsyncLifetime
{
    /// <summary>The Secret-Santa WASM app origin — where the browser drives the UI.</summary>
    public const string WebOrigin = "https://localhost:7081";

    /// <summary>The Main shell WASM app origin — where account management (incl. deletion) lives.</summary>
    public const string MainWebOrigin = "https://localhost:7080";

    /// <summary>The Main API origin the shared-group calls reach.</summary>
    public const string ApiOrigin = "https://localhost:7180";

    /// <summary>Mailpit's HTTP API, for reading the account-verification link out of band.</summary>
    public const string MailpitApi = "http://localhost:8025";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(5);

    private Process? _appHost;

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

        _appHost.OutputDataReceived += (_, e) => { if (e.Data is not null) { Console.WriteLine($"[apphost] {e.Data}"); } };
        _appHost.ErrorDataReceived += (_, e) => { if (e.Data is not null) { Console.WriteLine($"[apphost:err] {e.Data}"); } };
        _appHost.BeginOutputReadLine();
        _appHost.BeginErrorReadLine();

        await WaitForStackAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_appHost is { HasExited: false })
        {
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

            if (await IsUpAsync(probe, $"{WebOrigin}/")
                && await IsUpAsync(probe, $"{MainWebOrigin}/")
                && await IsUpAsync(probe, $"{ApiOrigin}/health"))
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

/// <summary>Shares one running stack across the Secret-Santa browser end-to-end tests.</summary>
[CollectionDefinition(CollectionName)]
public sealed class SecretSantaE2E : ICollectionFixture<SecretSantaStackFixture>
{
    /// <summary>The xUnit collection name.</summary>
    public const string CollectionName = "secretsanta-stack";
}
