using CircleTools.Ui.Abstractions;

namespace CircleTools.SecretSanta.App.Platform;

/// <summary>
/// The device head's <see cref="IPasskeyCeremony"/> — runs the two WebAuthn ceremonies on the
/// platform's system passkey provider (ADR-0066).
/// </summary>
/// <remarks>
/// <para>
/// The seam is opaque WebAuthn JSON in, opaque WebAuthn JSON out: the server issues the options and
/// verifies the result, exactly as for the browser heads. On <b>Android</b> the Credential Manager
/// takes and returns that JSON verbatim; on <b>iOS</b> the AuthenticationServices API works in binary
/// <c>NSData</c>, so this class translates the fields to and from base64url. The relying-party ID is the
/// settled suite domain <c>circletools.ch</c> ([ADR-0023], NFR-007).
/// </para>
/// <para>
/// <b>A cancelled ceremony returns <see langword="null"/>; a genuine failure throws
/// <see cref="PasskeyCeremonyException"/>.</b> "This platform/OS version has no usable passkey support"
/// is a genuine failure, so it throws too — the account pages already catch it and show the
/// "passkeys aren't available here" message (FR-011), which is the right outcome until the relying-party
/// domain serves the association files with real identifiers (ADR-0066 follow-ups; password sign-in,
/// FR-008, works throughout).
/// </para>
/// </remarks>
public sealed partial class MauiPasskeyCeremony : IPasskeyCeremony
{
    private const string Unsupported = "Passkeys are not available on this device.";

#if ANDROID
    /// <inheritdoc />
    public ValueTask<string?> CreateAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        RunAsync(optionsJson, register: true, cancellationToken);

    /// <inheritdoc />
    public ValueTask<string?> AssertAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        RunAsync(optionsJson, register: false, cancellationToken);

    // Credential Manager needs Android 9 (API 28); on API 24–27 there is no passkey provider at all, so
    // fall back to the "unsupported" path (password sign-in remains). On 28–33 the Google Play services
    // provider answers (the PlayServicesAuth companion package registers it); on 34+ the system provider.
    private static async ValueTask<string?> RunAsync(string optionsJson, bool register, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            throw new PasskeyCeremonyException(Unsupported);
        }

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
            ?? throw new PasskeyCeremonyException("No foreground activity to present the passkey prompt.");
        var manager = AndroidX.Credentials.CredentialManager.Create(activity);

        try
        {
            if (register)
            {
                var request = new AndroidX.Credentials.CreatePublicKeyCredentialRequest(optionsJson);
                var callback = new CreateCallback();
                manager.CreateCredentialAsync(
                    activity,
                    request,
                    cancellationSignal: null,
                    Java.Util.Concurrent.Executors.NewSingleThreadExecutor()!,
                    callback);

                var response = await callback.Task.ConfigureAwait(false);
                // Standard WebAuthn attestation response JSON — posted to the server unchanged.
                return (response as AndroidX.Credentials.CreatePublicKeyCredentialResponse)?.RegistrationResponseJson;
            }
            else
            {
                var option = new AndroidX.Credentials.GetPublicKeyCredentialOption(optionsJson);
                var request = new AndroidX.Credentials.GetCredentialRequest(
                    new List<AndroidX.Credentials.CredentialOption> { option });
                var callback = new GetCallback();
                manager.GetCredentialAsync(
                    activity,
                    request,
                    cancellationSignal: null,
                    Java.Util.Concurrent.Executors.NewSingleThreadExecutor()!,
                    callback);

                var response = await callback.Task.ConfigureAwait(false);
                // Standard WebAuthn assertion response JSON.
                return (response.Credential as AndroidX.Credentials.PublicKeyCredential)?.AuthenticationResponseJson;
            }
        }
        catch (OperationCanceledException)
        {
            // The user dismissed the system sheet — an ordinary choice, not an error (FR-011).
            return null;
        }
    }

    // The Java Credential Manager API is callback-based (no idiomatic Task overload), so bridge each
    // ceremony's callback onto a TaskCompletionSource. A user dismissal arrives as a *Cancellation*
    // exception, surfaced as OperationCanceledException so RunAsync maps it to null; every other failure
    // becomes a PasskeyCeremonyException.
    private sealed class CreateCallback : Java.Lang.Object, AndroidX.Credentials.ICredentialManagerCallback
    {
        private readonly TaskCompletionSource<AndroidX.Credentials.CreateCredentialResponse> _tcs = new();
        public Task<AndroidX.Credentials.CreateCredentialResponse> Task => _tcs.Task;

        public void OnResult(Java.Lang.Object? result) =>
            _tcs.TrySetResult((AndroidX.Credentials.CreateCredentialResponse)result!);

        public void OnError(Java.Lang.Object? error) => Fail(_tcs, error);
    }

    private sealed class GetCallback : Java.Lang.Object, AndroidX.Credentials.ICredentialManagerCallback
    {
        private readonly TaskCompletionSource<AndroidX.Credentials.GetCredentialResponse> _tcs = new();
        public Task<AndroidX.Credentials.GetCredentialResponse> Task => _tcs.Task;

        public void OnResult(Java.Lang.Object? result) =>
            _tcs.TrySetResult((AndroidX.Credentials.GetCredentialResponse)result!);

        public void OnError(Java.Lang.Object? error) => Fail(_tcs, error);
    }

    // A user dismissal arrives as a *Cancellation* exception — cancel the task so RunAsync maps it to
    // null (FR-011); anything else becomes a PasskeyCeremonyException. (Java.Lang.Object.ToString() on a
    // Throwable yields its class + message, which is enough context without a Throwable cast.)
    private static void Fail<T>(TaskCompletionSource<T> tcs, Java.Lang.Object? error)
    {
        if ((error?.GetType().Name ?? string.Empty).Contains("Cancellation", StringComparison.Ordinal))
        {
            tcs.TrySetCanceled();
            return;
        }

        tcs.TrySetException(new PasskeyCeremonyException(error?.ToString() ?? "The passkey ceremony failed."));
    }
#elif IOS
    /// <inheritdoc />
    public ValueTask<string?> CreateAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        Ios.MauiPasskeyCeremonyIos.RunAsync(optionsJson, register: true, cancellationToken);

    /// <inheritdoc />
    public ValueTask<string?> AssertAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        Ios.MauiPasskeyCeremonyIos.RunAsync(optionsJson, register: false, cancellationToken);
#else
    // Windows / Mac Catalyst: no system passkey provider is wired up. A genuine "unsupported" failure,
    // which the account pages render as the FR-011 "not available here" message.
    /// <inheritdoc />
    public ValueTask<string?> CreateAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        throw new PasskeyCeremonyException(Unsupported);

    /// <inheritdoc />
    public ValueTask<string?> AssertAsync(string optionsJson, CancellationToken cancellationToken = default) =>
        throw new PasskeyCeremonyException(Unsupported);
#endif
}
