---
status: Accepted
date: 2026-10-01
deciders: [Adrian]
tags: [auth, passkeys, webauthn, security, mobile, ios, android, identity]
---

# ADR-0069 — Passkey origin allow-list for the native heads

## Context and problem statement

[ADR-0066](0066-mobile-native-passkeys.md) brought native passkeys to the MAUI heads behind the
`IPasskeyCeremony` seam and stated, as a decision, **"no server or seam change."** The first on-device
TestFlight build disproved that assumption: creating a passkey fails on both native heads.

- **iOS** reaches the server and is rejected with
  *"The authenticator response had an invalid origin 'https://circletools.ch'."*
- **Android** fails one step earlier, client-side, with the generic *"Passkey could not be created
  here"* (a Digital-Asset-Links / Credential-Manager failure before any server call).

The server uses ASP.NET Core Identity's built-in WebAuthn (not Fido2NetLib). Its default origin check
(`PasskeyHandler.ValidateOriginAsync`) accepts a ceremony **only when `clientDataJSON.origin` equals the
request's `Origin` HTTP header**:

```csharp
return httpContext.Request.Headers.Origin is [var origin] && originUri.Equals(origin);
```

Browsers always send that header, so the web heads pass. But the native heads run the ceremony through
the **OS passkey APIs** (iOS AuthenticationServices, Android Credential Manager) and POST the result with
a plain `HttpClient` that sends **no `Origin` header** — so `httpContext.Request.Headers.Origin` is empty
and the check fails, even though the authenticator-stamped origin (`https://circletools.ch` on iOS;
`android:apk-key-hash:…` on Android) is legitimate for RP-ID `circletools.ch`. ADR-0066 overlooked the
missing request header, which is the whole reason the "no server change" assumption was wrong.

**So: how should the server validate the ceremony origin so the native heads are accepted without
loosening the web path?**

## Decision drivers

- **Native passkeys must work** — the affordance ADR-0066 shipped is otherwise dead on device.
- **Do not regress the web heads**, which pass today via the default request-`Origin`-header match.
- **No secrets, config-driven** — origins differ per environment and grow as apps ship; a code change
  per app head is the wrong seam.
- **RP-ID is already settled** (`circletools.ch`, ADR-0066/ADR-0023) and is not reopened here; this is
  about the *origin* check, a distinct value from the RP-ID.
- **Least privilege** — accept exactly the known origins, not "any origin that claims the RP".

## Considered options

1. **Keep the framework default** — reject native, keep passkeys web-only on device (leave the FR-011
   password fallback as the permanent mobile story).
2. **Override `IdentityPasskeyOptions.ValidateOrigin` with an explicit, config-driven allow-list** that
   validates `clientDataJSON.origin` directly, independent of the request header.
3. **Have the native `HttpClient` send a synthetic `Origin` header** matching the authenticator origin,
   so the default check passes.

## Decision outcome

**Chosen: option 2 — override `ValidateOrigin` with a configured allow-list.** The Main service reads
`Auth:Passkey:Origins` (a string array) and, when non-empty, sets
`options.ValidateOrigin` to accept exactly the origins in that list:

```csharp
var allowedOrigins = builder.Configuration.GetSection("Auth:Passkey:Origins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    var origins = new HashSet<string>(allowedOrigins, StringComparer.Ordinal);
    options.ValidateOrigin = context =>
        ValueTask.FromResult(!string.IsNullOrEmpty(context.Origin) && origins.Contains(context.Origin));
}
```

The Production list holds the **web** origins (the apex and each app subdomain, kept in step with
`Cors:AllowedOrigins`) and the **native authenticator** origins:

- iOS reports `https://circletools.ch` — its `webcredentials` association on the apex RP.
- Android reports `android:apk-key-hash:<base64url-SHA256 of the Play App Signing cert>` — the same cert
  whose colon-hex SHA-256 is in `.well-known/assetlinks.json`.

When no origins are configured (Development, test), the override is left off, so the framework default
(request-`Origin`-header match, RP-ID resolving to `localhost`) keeps the browser E2E ceremony working
unchanged.

Option 3 was rejected: forging an `Origin` header client-side is a brittle, security-smelling workaround
that duplicates the authenticator origin the server should instead validate explicitly. Option 1
abandons a feature whose blocker is a one-line server seam.

**This ADR corrects the "no server change" statement in ADR-0066** (which stays Accepted for everything
else). The origin allow-list is the server change ADR-0066 missed.

## Consequences

- **Positive:** iOS passkey registration/sign-in works once this ships (the ceremony already reaches the
  server; only the origin check blocked it). The allow-list is a strict superset of what the web path
  accepted, so the web heads are unaffected.
- **Positive:** new apps and new signing certs are added by config, not code.
- **Negative / accepted:** the override **replaces** the framework's cross-origin guard. The allow-list
  is an exact-match set of trusted first-party origins, so a cross-origin/iframe ceremony is simply not
  in the list; acceptable for first-party native + same-suite web.
- **Android is not fully fixed by this change alone.** Its failure is client-side (Digital Asset Links):
  the server fix only matters once Credential Manager succeeds. Android needs
  `https://circletools.ch/.well-known/assetlinks.json` served over HTTPS (no redirects,
  `application/json`) with a SHA-256 matching the **installed build's** Play App Signing cert.
- **Update 2026-10-02 — user verification set to "preferred".** With the origin fix deployed, iOS got
  past the origin check and then failed attestation with *"User verification is required, but the
  authenticator data flags did not have the 'UserVerified' flag"*: `IdentityPasskeyOptions.UserVerificationRequirement`
  defaults to `required`, but the OS passkey ceremony returned no UserVerified flag even though Face ID
  ran. Set it to `preferred` in `Program.cs` — platform authenticators still perform user verification, so
  the biometric UX stays while the server stops rejecting a credential that omits the flag. Needs a Main
  redeploy. **Android's** `"Passkey could not be created here"` is unrelated (client-side Digital Asset
  Links, see follow-up 2): the hosted `assetlinks.json` SHA-256 must be the **Play App Signing** cert, not
  the upload cert, and the `apk-key-hash` origin must be derived from that same cert.
- **Update 2026-10-02 (b) — Android root cause found: wrong signing cert (upload key, not app-signing key).**
  With iOS passkeys working, Android still failed, and logcat gave the exact reason:
  `ValidateRpIdOperation` → `ctrq: [50152] RP ID cannot be validated` →
  `CreatePublicKeyCredentialDomException: RP ID cannot be validated`. Google's own Digital Asset Links
  API (`digitalassetlinks.googleapis.com/v1/statements:list`) confirmed the hosted file was live and
  correct *for the fingerprint it listed* — but that fingerprint was wrong. The committed value
  `FD:D7:27:…:63:5E` was extracted earlier from a locally-signed build and is the **upload/local key**.
  The Play Console (**App integrity → App signing key certificate**) shows the real app-signing key is
  `4D:93:F9:…:70:BF:C1`; Play re-signs every distributed build (all tracks) with it, so that is the cert
  on-device that Credential Manager validates. Fix (config only, no app rebuild): put the app-signing
  fingerprint first in **both** `assetlinks.json` files and add its `apk-key-hash`
  (`android:apk-key-hash:TZP5Y67jfRjnBHyfPFzFA9NTUqPQ1Pl4fFptWhhwv8E`) to `Auth:Passkey:Origins`. The
  upload/local key is kept as a second entry so sideloaded dev builds also validate. Needs a **web-front
  redeploy** (Main + Secret-Santa WebApps serve the `.well-known` files) — the Main API redeploy only
  matters for the `Origins` list. This closes follow-up 2.
- **Follow-ups:**
  1. Deploy the Main service with `Auth:Passkey:Origins` set (Production config added here).
  2. Verify the association files are live on the apex: `assetlinks.json` and
     `apple-app-site-association` (served, correct content-type, no redirects), and that the
     `assetlinks.json` SHA-256 matches the artifact under test — reconcile against Play App Signing.
  3. Confirm the **Associated Domains** capability is enabled on the iOS App ID + provisioning profile
     (iOS already produced a `circletools.ch` origin, which indicates it is, but confirm at go-live).
  4. Device-verify both ceremonies end-to-end against the deployed API (closes an ADR-0066 follow-up).
  5. Add a server-side ceremony/origin test — the existing tests store credentials directly and never
     exercise origin validation, so this class of bug was invisible to CI.
- **New risks:** an origin string mismatch (trailing slash, case, wrong Android hash) silently rejects a
  head; mitigated by deriving the Android hash from the committed `assetlinks.json` fingerprint and
  keeping the web origins aligned with `Cors:AllowedOrigins`.

## Pros and cons of the options

### 1. Keep the framework default
- 👍 Zero code; web passkeys keep working.
- 👎 Native passkeys stay broken — the ADR-0066 feature is inert on device for no good reason.

### 2. Config-driven `ValidateOrigin` allow-list (chosen)
- 👍 One small, explicit server seam; web unaffected; per-app growth is config-only; least privilege.
- 👎 Replaces the built-in cross-origin guard (acceptable for an exact first-party allow-list).

### 3. Synthetic `Origin` header on the native client
- 👍 No server change (keeps ADR-0066's original claim literally true).
- 👎 Forging a header to satisfy a check the server should own; brittle; spreads the RP/origin knowledge
  into every native head and `HttpClient`.

## Links

- Corrects the "no server or seam change" decision in
  [ADR-0066](0066-mobile-native-passkeys.md) (native passkeys)
- Related ADRs: [ADR-0009](0009-authentication-and-passkeys.md) (Identity + built-in passkeys),
  [ADR-0021](0021-web-auth-token-model.md) (token model; the ceremony-cookie exception),
  [ADR-0013](0013-wasm-api-dev-wiring.md) (WASM ↔ API separate origins + CORS),
  [ADR-0023](0023-local-dev-infrastructure-in-apphost.md) (RP-ID = `circletools.ch`)
- Requirements: [FR-010](../requirements/functional/FR-010-manage-passkeys.md),
  [FR-011](../requirements/functional/FR-011-sign-in-with-passkey.md)
- Runbook: [Deployment — mobile apps](../deployment/mobile-apps.md)
