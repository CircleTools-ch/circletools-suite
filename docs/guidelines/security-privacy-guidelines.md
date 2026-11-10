# Security & Privacy Guidelines

> **Status: outline.**

## 1. Privacy by design
- Collect the **minimum** personal data. With **no account**, nothing leaves the device.
- Family/friends data is sensitive — treat names, relationships, and gift info carefully.
- Swiss-first: align with the Swiss DPA (revDSG) and GDPR where relevant.

## 2. Authentication & secrets
- Email+password with modern hashing; optional **passkeys/WebAuthn**.
- Tokens in `SecureStorage` on device; never in logs or source.
- No secrets in the repo — use user-secrets / environment / Aspire parameters. `.gitignore`
  excludes `secrets.json`, `.env*`, and `appsettings.*.local.json`.

## 3. Data protection
- Encrypt in transit (HTTPS everywhere); consider at-rest encryption for sensitive local data.
- Clear data deletion & account-closure paths (to be specified).

## 4. Input & API safety
- Validate all inputs at boundaries; parameterized queries via EF Core; `ProblemDetails` errors
  without leaking internals.

## 5. Telemetry & consent
- Client telemetry is optional and **consent-gated**; no PII in traces/metrics/logs (open ADR).

## 6. Dependencies
- Packages only via CPM; watch for advisories; keep the SDK/workloads current.

## 7. Address-accepting endpoints
Anything taking an email address from an untrusted caller falls under
[NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md). Identical body
and status are the easy half and are not the half that has failed here.

- **Assume the branches differ in time until something makes them equal.** Four endpoints shipped
  with identical responses and timing gaps of 1.18x–1.9x, purely from a token and an outbox row on
  one path. Queueing the mail bounds the gap; it does not close it.
- **Pick the mechanism to fit the asymmetry.**
  - One dominant symmetric cost (a password hash) → spend it on both paths via
    `DecoyPasswordVerifier`.
  - A token, a database write, or several → `RequireConstantTime` on the route. Chain it **first**,
    so it nests outside every other filter.
- **Do not equalise by padding both paths with a large constant.** Spending an unrelated password
  hash on both sides shrinks the *ratio* while leaving the absolute difference exactly where it was.
  It defeats the test, not the attacker.
- **A new address-accepting route needs a timing test**, not only a body/status one — added to
  `EnumerationTimingTests` or `InvitationTimingTests`, through
  `ConstantTimeEnvelope.AssertHeldToBudget`. Assert that the budget is reached and that the handler
  fits inside it, rather than that the paths are close: the real leaks measured 0.70–3.01 ms, which
  is inside the resolution of the timer you would measure them with. Identical requests sent as
  separate arms differ by up to 4.64 ms on a busy machine, so a tight difference threshold fails on
  routes with nothing wrong.
