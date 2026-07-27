---
status: Accepted
date: 2026-07-27
deciders: [Adrian]
tags: [architecture, aspire, infrastructure, development, email, identity]
---

# ADR-0023 — Local development infrastructure in the AppHost

## Context and problem statement

The M1 identity requirements are complete ([FR-007..FR-015](../requirements/index.md)), but two of
them cannot be *finished* without decisions that belong to the deployment phase:

- **Transactional email** — [FR-007](../requirements/functional/FR-007-create-suite-account.md)
  (verification), [FR-009](../requirements/functional/FR-009-reset-password.md) (reset) and
  [FR-013](../requirements/functional/FR-013-change-email-address.md) (email change) all need a
  provider.
- **Hosting & the suite domain** — needed for the passkey Relying-Party ID
  ([FR-010](../requirements/functional/FR-010-manage-passkeys.md),
  [FR-011](../requirements/functional/FR-011-sign-in-with-passkey.md)), the CSP directive set
  ([ADR-0021](0021-web-auth-token-model.md)), and the residency target in
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md).

Both are genuinely deployment-phase decisions and neither is ready. **Must implementation wait for
them?**

## Decision drivers

- Provider and hosting choices should be made when there is evidence to make them well — not rushed
  because code is blocked.
- The project already orchestrates dev dependencies with Aspire
  ([ADR-0003](0003-modular-monolith-with-aspire.md)); Postgres is containerised this way already.
- **No external accounts, keys or spend to develop.** A contributor should get a working environment
  from a clone, matching the local-first ethos of the product itself.
- Hobby scale: no budget for provider sandboxes held open indefinitely.
- Deferring a decision must not silently become *avoiding* it — the gap has to stay visible.

## Considered options

1. **Block implementation** until the provider and hosting ADRs are decided.
2. **Code against a chosen provider SDK now** and change it later if the decision moves.
3. **Containerised dev infrastructure in the AppHost, behind provider-agnostic seams.**
4. **In-memory / logging fakes only** — no containers; a no-op `IEmailSender` that writes to the log.

## Decision outcome

**Chosen: option 3 — containerised dev infrastructure in the AppHost, behind provider-agnostic
seams.**

The seam is the decision; the container is only its development implementation. Application code
depends on an abstraction, and the production provider becomes an adapter plus configuration rather
than a code change.

### Email — now

- **Mailpit** (`axllent/mailpit`) as an AppHost container, exposing SMTP and a web inbox.
- The application sends through ASP.NET Core Identity's **`IEmailSender`** abstraction with an SMTP
  adapter; the endpoint arrives as configuration from the AppHost. **No provider SDK is referenced.**
- Added with the built-in `AddContainer`, so **no new NuGet package** and no change to
  `Directory.Packages.props`.

Chosen over option 4 because a log line does not let you check what actually matters about these
mails: that the DE/EN/FR bodies render, that the verification and reset links are correct and
single-use, and that mail dispatch is **queued rather than awaited** — which
[NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md) makes a hard
timing requirement, not a nicety.

### Domain, TLS & passkeys — phase 2, when FR-010/011 start

- A **reverse-proxy container** (Caddy or Traefik) terminating TLS for `*.appsuite.test`, with an
  **mkcert**-generated locally-trusted certificate. `.test` is reserved for this purpose (RFC 6761);
  a public wildcard-DNS service such as `localtest.me` was rejected because it reintroduces the
  external dependency this ADR exists to remove.
- This reproduces the production topology — a registrable domain plus an app subdomain — so the
  **eTLD+1 RP-ID strategy is genuinely exercised** rather than assumed.
- **The RP ID, the allowed origins and the CSP are configuration, never hard-coded**, in every client
  and in the API.

Deferred to phase 2 because FR-010/011 are `Should` while the email-dependent requirements are
`Must`, and the TLS setup carries per-machine steps (cert generation and trust, hosts entries) that
should not be imposed before they buy anything.

## Consequences

- **Positive:** M1 implementation starts now; a contributor gets a full environment from a clone with
  no accounts, keys or spend; provider choice stays open and becomes an adapter swap; the timing
  behaviour NFR-005 demands is observable locally; no new packages.
- **Negative / accepted — deliverability is not exercised at all.** Mailpit proves the code sends. It
  proves nothing about SPF, DKIM, DMARC, bounce and complaint handling, sender reputation or provider
  rate limits. Those surface only against a real provider, and some may force changes to how sending
  is structured.
- **Negative / accepted — one FR-010 criterion stays unverifiable.** "A passkey registered on the web
  is offered in the MAUI client" depends on `apple-app-site-association` and Digital Asset Links
  served over HTTPS **from the real domain**; platform validation cannot be reproduced locally. That
  acceptance criterion cannot be closed before hosting exists, and the M1 test plan must say so rather
  than appear to cover it.
- **Negative / accepted — RP IDs do not migrate.** Passkeys registered against `appsuite.test` will
  not work against the eventual real domain. Harmless for throwaway development data, but it means the
  domain must be settled **before the first real user registers a passkey** — the deferral is safe up
  to production, not past it.
- **Negative / accepted — data residency is untouched.** NFR-004's residency target remains open and
  is purely a hosting decision.
- **Follow-ups:**
  - The M1 feature spec specifies the `IEmailSender` SMTP adapter and the configuration keys.
  - Phase 2 (proxy + TLS) lands with FR-010/011, together with a dev-setup runbook in
    [deployment](../deployment/index.md) covering mkcert and hosts entries.
  - **The deployment ADRs stay open and stay due** — transactional email provider, cloud provider &
    Swiss residency, CI/CD. This ADR buys time; it does not close them.
- **New risks:** a green local run may be mistaken for deployment readiness. Mitigated by naming the
  four gaps above explicitly here, and by keeping the deployment items in the
  [ADR register's open backlog](index.md#open-decisions-backlog) rather than marking them handled.

## Pros and cons of the options

### 1. Block until the provider decisions are made
- 👍 Nothing is built twice; no throwaway setup.
- 👎 Stalls M1 indefinitely behind decisions that need evidence we get from building; pressures a
  rushed provider choice for a reason unrelated to its merits.

### 2. Code against a provider SDK now
- 👍 Production-realistic from the first commit, including deliverability behaviour.
- 👎 Picks the provider by default rather than by decision; couples application code to an SDK;
  needs an account and keys before anything runs; contributors inherit that dependency.

### 3. Containers behind provider-agnostic seams (chosen)
- 👍 Unblocks now; zero external dependencies; provider stays a configuration concern; no new packages.
- 👎 Deliverability and the platform-validated passkey paths are not exercised; some integration work
  is deferred rather than removed.

### 4. In-memory / logging fakes only
- 👍 Cheapest possible; nothing to run.
- 👎 Cannot check rendered multilingual bodies, link correctness, or queued-vs-awaited dispatch —
  which is precisely what NFR-005 measures. Trades away the feedback the seam exists to provide.

## Links

- Related ADRs: [ADR-0003](0003-modular-monolith-with-aspire.md) (Aspire orchestration),
  [ADR-0009](0009-authentication-and-passkeys.md), [ADR-0021](0021-web-auth-token-model.md) (CSP and
  token model), [ADR-0013](0013-wasm-api-dev-wiring.md) (the existing dev-wiring precedent),
  [ADR-0007](0007-persistence-approach.md)
- arc42: [07 Deployment view](../arc42/07-deployment-view.md),
  [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md)
- Requirements: [FR-007](../requirements/functional/FR-007-create-suite-account.md),
  [FR-009](../requirements/functional/FR-009-reset-password.md),
  [FR-010](../requirements/functional/FR-010-manage-passkeys.md),
  [FR-011](../requirements/functional/FR-011-sign-in-with-passkey.md),
  [FR-013](../requirements/functional/FR-013-change-email-address.md),
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
- Still open: the [deployment decisions backlog](index.md#open-decisions-backlog)
