# 8. Cross-cutting Concepts

> _Proposed_ — concepts that apply across building blocks. Each firms up via ADRs and guidelines.

## 8.1 Domain model & DDD

Tactical patterns (entities, value objects, aggregates, domain events, repositories) in
`SharedKernel` and each `Service.Core`. Ubiquitous language per the [glossary](../glossary/index.md).

## 8.2 Persistence & local-first

Repository **ports** in the domain; **EF Core** behind both adapters — a **Postgres** adapter
(`Service.Repository`) on the server and a **SQLite** adapter (`LocalStore`) on native clients
([ADR-0007](../adr/0007-persistence-approach.md)). Keys are client-generated **GUID v7**
([ADR-0008](../adr/0008-entity-id-strategy.md)). Aggregates are the unit of persistence and sync.

## 8.3 Synchronization

Client sync engine keeps a change-log + cursor; server exposes delta endpoints backed by an
outbox + row versioning. Reconciliation is an anti-corruption boundary. The conflict strategy is
**deferred** until the Secret-Santa aggregates exist (interim: last-write-wins per aggregate — see
the [ADR register](../adr/index.md)).

## 8.4 Identity & authentication

`Walf.Identity` centralizes the single suite account using **ASP.NET Core Identity** with the
**built-in .NET 10 passkey** support: email+password and optional **passkeys/WebAuthn**
([ADR-0009](../adr/0009-authentication-and-passkeys.md)). Bearer/JWT for APIs; tokens in MAUI
`SecureStorage` on device; the web token model is confirmed at the client-shell build. Offline mode
uses **no** identity.

## 8.5 Localization (DE/EN/FR)

Central `Walf.Localization` using **RESX + `IStringLocalizer`** ([ADR-0011](../adr/0011-localization-format-and-culture.md));
`de-CH` default culture; culture is a synced user preference. Formatting via `CultureInfo`; no
locale/currency logic in the domain. See the [localization guidelines](../guidelines/localization-guidelines.md).

## 8.6 Configuration sharing

suite-wide settings (branding, theme, locale, feature flags) exposed via an `IAppConfig`
abstraction, consistent across Web and native; user/suite prefs persisted locally and synced.

## 8.7 Observability

`AddServiceDefaults()` wires OpenTelemetry (traces/metrics/logs), health checks, and HTTP
resilience for every service host. Client telemetry is optional and consent-gated
([ADR-0012](../adr/0012-observability-and-telemetry.md)).

## 8.8 Security & privacy

Privacy by design: minimal PII, input validation, secrets never in the repo, Swiss/GDPR
awareness. See the [security & privacy guidelines](../guidelines/security-privacy-guidelines.md).

## 8.9 Error handling

Domain exceptions vs. `Result` for expected failures; no control-flow-by-exception; API errors as
`ProblemDetails`.
