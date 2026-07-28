namespace AppSuite.Ui.Abstractions;

/// <summary>
/// Runs the two WebAuthn ceremonies on whatever authenticator the platform provides.
/// </summary>
/// <remarks>
/// <para>
/// The server hands out the options and verifies the result; only the ceremony itself is
/// platform-specific, which is why it sits behind this seam. In a browser it is
/// <c>navigator.credentials.create/get</c>; a MAUI head would use the platform passkey APIs.
/// </para>
/// <para>
/// <b>A cancelled ceremony returns <see langword="null"/>, never an exception.</b> Dismissing the
/// system prompt is an ordinary thing to do, and
/// <c>FR-011</c> requires that it produce no error and no error styling. Only a genuine failure —
/// malformed options, an authenticator error, a platform with no passkey support — throws, and it
/// throws <see cref="PasskeyCeremonyException"/> so callers never have to know what the platform is.
/// </para>
/// </remarks>
public interface IPasskeyCeremony
{
    /// <summary>Creates a new credential (registration).</summary>
    /// <param name="optionsJson">The server's <c>PublicKeyCredentialCreationOptions</c>, as JSON.</param>
    /// <param name="cancellationToken">Cancels the ceremony.</param>
    /// <returns>The credential as JSON, or <see langword="null"/> if the user cancelled.</returns>
    ValueTask<string?> CreateAsync(string optionsJson, CancellationToken cancellationToken = default);

    /// <summary>Asserts an existing credential (sign-in).</summary>
    /// <param name="optionsJson">The server's <c>PublicKeyCredentialRequestOptions</c>, as JSON.</param>
    /// <param name="cancellationToken">Cancels the ceremony.</param>
    /// <returns>The assertion as JSON, or <see langword="null"/> if the user cancelled.</returns>
    ValueTask<string?> AssertAsync(string optionsJson, CancellationToken cancellationToken = default);
}
