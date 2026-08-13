using AppSuite.Ui.Abstractions;

namespace AppSuite.SecretSanta.App.Platform;

/// <summary>
/// The device head's <see cref="IPasskeyCeremony"/> — currently "no passkey support on this
/// platform" (FEAT-002's deferred MAUI cross-client passkeys).
/// </summary>
/// <remarks>
/// <para>
/// Passkeys are deferred on mobile until the suite has a real registrable domain: the WebAuthn
/// RP-ID is the registrable suite domain (FR-010, FR-011), and it cannot be settled while the brand
/// and domain are deliberately deferred. Password sign-in (FR-008) works fully; only the passkey
/// buttons are inert.
/// </para>
/// <para>
/// Reporting "unsupported" is a genuine failure, not a cancellation, so both ceremonies throw
/// <see cref="PasskeyCeremonyException"/> rather than returning <see langword="null"/> — the account
/// pages already catch it and show the "passkeys aren't available here" message (FR-011), which is
/// exactly the right outcome on a platform that has no authenticator wired up yet.
/// </para>
/// </remarks>
public sealed class MauiPasskeyCeremony : IPasskeyCeremony
{
    private const string Unsupported = "Passkeys are not yet available on the mobile app.";

    /// <inheritdoc />
    public ValueTask<string?> CreateAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        throw new PasskeyCeremonyException(Unsupported);

    /// <inheritdoc />
    public ValueTask<string?> AssertAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        throw new PasskeyCeremonyException(Unsupported);
}
