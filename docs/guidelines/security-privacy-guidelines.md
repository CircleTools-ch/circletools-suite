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
