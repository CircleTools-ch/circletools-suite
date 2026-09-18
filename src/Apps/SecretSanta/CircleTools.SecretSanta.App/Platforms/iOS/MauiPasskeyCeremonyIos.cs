using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using AuthenticationServices;
using CircleTools.Ui.Abstractions;
using Foundation;
using UIKit;

namespace CircleTools.SecretSanta.App.Platform.Ios;

/// <summary>
/// The iOS passkey ceremony (ADR-0066), driven by <c>AuthenticationServices</c>
/// (<see cref="ASAuthorizationPlatformPublicKeyCredentialProvider"/>).
/// </summary>
/// <remarks>
/// <para>
/// Unlike Android's Credential Manager (which round-trips the WebAuthn JSON verbatim), the iOS API works
/// in binary <see cref="NSData"/>. So this reads the fields the platform request needs out of the
/// server's options JSON (<c>rp.id</c>/<c>rpId</c>, <c>challenge</c>, <c>user.id</c>/<c>user.name</c>,
/// and — for assertion — <c>allowCredentials</c>), runs the system sheet, then reassembles the exact
/// WebAuthn response JSON the server already verifies for the web head, base64url-encoding each binary
/// field.
/// </para>
/// <para>
/// The platform authenticator always creates ES256 resident (discoverable) keys with user verification,
/// so <c>pubKeyCredParams</c>/<c>authenticatorSelection</c>/<c>attestation</c> are implicitly satisfied
/// and are not expressible on the request; <c>excludeCredentials</c> is not honoured by the platform
/// provider (server-side duplicate-registration prevention still applies). Requires iOS 16 for working
/// passkeys; below that it throws (FR-011 fallback). <b>Device-unverified</b> — written against the API
/// docs; verify on the first TestFlight build (ADR-0065: no local iOS build loop).
/// </para>
/// </remarks>
internal static class MauiPasskeyCeremonyIos
{
    public static async ValueTask<string?> RunAsync(string optionsJson, bool register, CancellationToken cancellationToken)
    {
        // The types exist from iOS 15, but synced/usable passkeys only from iOS 16 — gate on 16.
        if (!OperatingSystem.IsIOSVersionAtLeast(16))
        {
            throw new PasskeyCeremonyException("Passkeys need iOS 16 or later.");
        }

        var root = JsonNode.Parse(optionsJson) as JsonObject
            ?? throw new PasskeyCeremonyException("The passkey options were not valid JSON.");

        var challenge = FromBase64Url(RequireString(root, "challenge"));
        ASAuthorizationRequest request;

        if (register)
        {
            var rpId = RequireString(root["rp"] as JsonObject, "id", "rp.id");
            var user = root["user"] as JsonObject
                ?? throw new PasskeyCeremonyException("The passkey options were missing the user.");
            var userId = FromBase64Url(RequireString(user, "id", "user.id"));
            var userName = RequireString(user, "name", "user.name");

            var provider = new ASAuthorizationPlatformPublicKeyCredentialProvider(rpId);
            request = provider.CreateCredentialRegistrationRequest(challenge, userName, userId);
        }
        else
        {
            // The request options carry the RP id as "rpId" (flat), not "rp.id".
            var rpId = RequireString(root, "rpId");
            var provider = new ASAuthorizationPlatformPublicKeyCredentialProvider(rpId);
            var assertion = provider.CreateCredentialAssertionRequest(challenge);

            // Best-effort allowCredentials → platform descriptors; absent/empty means a discoverable
            // (usernameless) sign-in, which platform passkeys support.
            if (root["allowCredentials"] is JsonArray allow && allow.Count > 0)
            {
                assertion.AllowedCredentials = allow
                    .OfType<JsonObject>()
                    .Where(c => c["id"] is not null)
                    .Select(c => new ASAuthorizationPlatformPublicKeyCredentialDescriptor(FromBase64Url((string)c["id"]!)))
                    .ToArray();
            }

            request = assertion;
        }

        var tcs = new TaskCompletionSource<string?>();
        var handler = new PasskeyDelegate(tcs, register);
        var controller = new ASAuthorizationController(new[] { request })
        {
            Delegate = handler,
            PresentationContextProvider = handler,
        };

        using (cancellationToken.Register(() => tcs.TrySetResult(null)))
        {
            controller.PerformRequests();
            var result = await tcs.Task.ConfigureAwait(false);
            // Keep the delegate + controller rooted until the ceremony has resolved.
            GC.KeepAlive(handler);
            GC.KeepAlive(controller);
            return result;
        }
    }

    // The platform passkey types are iOS 16+; RunAsync gates on IsIOSVersionAtLeast(16) before any
    // instance is created, but these members hold the iOS-16-only types outside that guarded flow, so
    // they carry the attribute explicitly (the app-wide SupportedOSPlatformVersion stays 15.0 to keep
    // the FR-011 sub-16 fallback).
    [SupportedOSPlatform("ios16.0")]
    private sealed class PasskeyDelegate(TaskCompletionSource<string?> tcs, bool register)
        : NSObject, IASAuthorizationControllerDelegate, IASAuthorizationControllerPresentationContextProviding
    {
        // The AuthenticationServices error domain, so a code 1001 from an unrelated NSError domain is not
        // misread as a user cancel.
        private const string AuthorizationErrorDomain = "com.apple.AuthenticationServices.AuthorizationError";

        [Export("authorizationController:didCompleteWithAuthorization:")]
        public void DidComplete(ASAuthorizationController controller, ASAuthorization authorization)
        {
            try
            {
                // ASAuthorization exposes only the generic GetCredential<T>(); RunAsync already knows
                // which ceremony ran, so ask for the matching concrete credential type.
                if (register)
                {
                    var registration = authorization.GetCredential<ASAuthorizationPlatformPublicKeyCredentialRegistration>();
                    tcs.TrySetResult(registration is null
                        ? throw new PasskeyCeremonyException("The passkey ceremony returned an unexpected credential.")
                        : BuildRegistrationJson(registration));
                }
                else
                {
                    var assertion = authorization.GetCredential<ASAuthorizationPlatformPublicKeyCredentialAssertion>();
                    tcs.TrySetResult(assertion is null
                        ? throw new PasskeyCeremonyException("The passkey ceremony returned an unexpected credential.")
                        : BuildAssertionJson(assertion));
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(new PasskeyCeremonyException("The passkey ceremony failed.", ex));
            }
        }

        [Export("authorizationController:didCompleteWithError:")]
        public void DidError(ASAuthorizationController controller, NSError error)
        {
            // A dismissed sheet is an ordinary choice, not an error (FR-011) — but only treat the
            // AuthenticationServices "canceled" as a cancel, not a same-numbered code from another domain.
            if (error.Domain == AuthorizationErrorDomain && error.Code == (nint)ASAuthorizationError.Canceled)
            {
                tcs.TrySetResult(null);
                return;
            }

            tcs.TrySetException(new PasskeyCeremonyException(error.LocalizedDescription ?? "The passkey ceremony failed."));
        }

        [Export("presentationAnchorForAuthorizationController:")]
        public UIWindow GetPresentationAnchor(ASAuthorizationController controller) =>
            UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIWindowScene>()
                .SelectMany(scene => scene.Windows)
                .FirstOrDefault(window => window.IsKeyWindow)
            ?? throw new PasskeyCeremonyException("No key window to present the passkey prompt.");
    }

    // Registration → WebAuthn PublicKeyCredential (attestation) JSON.
    [SupportedOSPlatform("ios16.0")]
    private static string BuildRegistrationJson(ASAuthorizationPlatformPublicKeyCredentialRegistration registration)
    {
        var id = ToBase64Url(registration.CredentialId);
        return new JsonObject
        {
            ["id"] = id,
            ["rawId"] = id,
            ["type"] = "public-key",
            ["authenticatorAttachment"] = "platform",
            ["response"] = new JsonObject
            {
                ["clientDataJSON"] = ToBase64Url(registration.RawClientDataJson),
                ["attestationObject"] = ToBase64Url(registration.RawAttestationObject),
            },
            ["clientExtensionResults"] = new JsonObject(),
        }.ToJsonString();
    }

    // Assertion → WebAuthn PublicKeyCredential (assertion) JSON.
    [SupportedOSPlatform("ios16.0")]
    private static string BuildAssertionJson(ASAuthorizationPlatformPublicKeyCredentialAssertion assertion)
    {
        var id = ToBase64Url(assertion.CredentialId);
        return new JsonObject
        {
            ["id"] = id,
            ["rawId"] = id,
            ["type"] = "public-key",
            ["authenticatorAttachment"] = "platform",
            ["response"] = new JsonObject
            {
                ["clientDataJSON"] = ToBase64Url(assertion.RawClientDataJson),
                ["authenticatorData"] = ToBase64Url(assertion.RawAuthenticatorData),
                ["signature"] = ToBase64Url(assertion.Signature),
                ["userHandle"] = ToBase64Url(assertion.UserId),
            },
            ["clientExtensionResults"] = new JsonObject(),
        }.ToJsonString();
    }

    private static string RequireString(JsonObject? obj, string key, string? label = null) =>
        (obj?[key] as JsonValue)?.ToString() is { Length: > 0 } value
            ? value
            : throw new PasskeyCeremonyException($"The passkey options were missing '{label ?? key}'.");

    private static NSData FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return NSData.FromArray(Convert.FromBase64String(padded));
    }

    private static string ToBase64Url(NSData? data) =>
        data is null
            ? string.Empty
            : Convert.ToBase64String(data.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
