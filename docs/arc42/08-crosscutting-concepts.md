# 8. Cross-cutting Concepts

> _Proposed_ — concepts that apply across building blocks. Each firms up via ADRs and guidelines.

## 8.1 Domain model & DDD

Tactical patterns (entities, value objects, aggregates, domain events, repositories) in
`SharedKernel` and each `Service.Core`. Ubiquitous language per the [glossary](../glossary/index.md).

## 8.2 Persistence & local-first

Repository **ports** in the domain; **Postgres** adapter (`Service.Repository`) on the server and
**SQLite** adapter (`LocalStore`) on the client. Aggregates are the unit of persistence and sync.

## 8.3 Synchronization

Client sync engine keeps a change-log + cursor; server exposes delta endpoints backed by an
outbox + row versioning. Reconciliation is an anti-corruption boundary. Conflict strategy is an
open ADR.

## 8.4 Identity & authentication

`Suite.Identity` centralizes the single suite account: email+password and optional
**passkeys/WebAuthn**. Bearer/JWT for APIs; tokens in MAUI `SecureStorage` on device; web token
model is an open ADR. Offline mode uses **no** identity.

## 8.5 Localization (DE/EN/FR)

Central `Suite.Localization`; `de-CH` default culture; culture is a synced user preference.
Formatting via `CultureInfo`; no locale/currency logic in the domain. See the
[localization guidelines](../guidelines/localization-guidelines.md).

## 8.6 Configuration sharing

Suite-wide settings (branding, theme, locale, feature flags) exposed via an `IAppConfig`
abstraction, consistent across Web and native; user/suite prefs persisted locally and synced.

## 8.7 Observability

`AddServiceDefaults()` wires OpenTelemetry (traces/metrics/logs), health checks, and HTTP
resilience for every service host. Client telemetry is optional and consent-gated (open ADR).

## 8.8 Security & privacy

Privacy by design: minimal PII, input validation, secrets never in the repo, Swiss/GDPR
awareness. See the [security & privacy guidelines](../guidelines/security-privacy-guidelines.md).

## 8.9 Error handling

Domain exceptions vs. `Result` for expected failures; no control-flow-by-exception; API errors as
`ProblemDetails`.
