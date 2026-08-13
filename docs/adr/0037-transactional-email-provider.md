---
status: Accepted
date: 2026-08-13
deciders: [Adrian]
tags: [deployment, email, deliverability, privacy, m1]
---

# ADR-0037 — Transactional email provider

## Context and problem statement

Several account flows are **undeliverable without a real email sender**, and they are M1 flows, not
future ones: address verification ([FR-007](../requirements/functional/FR-007-create-suite-account.md)),
password reset ([FR-009](../requirements/functional/FR-009-reset-password.md)) and email change
([FR-013](../requirements/functional/FR-013-change-email-address.md)). M2 then adds **circle
invitations** ([ADR-0018](0018-circle-and-sharing-model.md)), which the code already routes through an
**outbox port** — the invitation mail is sent behind a seam, not a hard-wired SMTP client. So the app is
ready for a provider; it just does not have one, and this is listed as an open deployment decision the
suite cannot ship M1 without.

Two things make "just use Infomaniak" the wrong reflex here:

- **Infomaniak Mail is mailbox hosting, not a transactional API.** It gives the project its role
  mailbox (`dev@<brand>`), but it does not offer the per-message DKIM signing, bounce/complaint handling,
  send logs and API/SMTP relay that automated verification-and-reset mail needs to land in inboxes rather
  than spam folders.
- **Deliverability is the whole job.** A verification mail that lands in spam is a broken sign-up. That
  requires a sender with a warmed reputation plus correct **SPF, DKIM and DMARC** on the suite domain —
  not a mailbox.

**Which service sends the suite's transactional email, cheaply, with EU/Swiss data locality and good
deliverability?**

## Decision drivers

- **Deliverability first** — verification and reset mail must reach the inbox.
- **EU/Swiss data locality** in spirit with
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md); the payload is an
  email address plus a short-lived link, but it is still PII in transit.
- **Free or near-free at family scale.** Real volume is a handful of mails a day, spiking mildly at
  Christmas — nowhere near a paid tier.
- **API *and* SMTP**, DKIM signing, and visible send logs for debugging a "didn't arrive" report.
- **Low switching cost.** The [outbox port](0018-circle-and-sharing-model.md) already abstracts sending,
  so the provider must be a thin adapter behind it, not a commitment baked through the code.
- **Separate from the role mailbox.** The transactional sender and the human `dev@` inbox are different
  concerns and should not be coupled.

## Considered options

1. **Brevo** (EU / France) — 300 transactional emails/day free (~9,000/mo), API + SMTP, DKIM.
2. **Postmark** — best-in-class deliverability; 100 emails/mo free, then ~USD 15/mo.
3. **Infomaniak SMTP / Mail relay** — send through the existing Infomaniak account.
4. **Mailgun (EU region)** / **Scaleway TEM** — EU-hosted transactional APIs.
5. **Self-hosted SMTP** on the [ADR-0036](0036-cloud-provider-and-hosting.md) VPS.

## Decision outcome

**Chosen: option 1 — Brevo's free tier**, with the sender wired behind the existing outbox port and the
role mailbox left on Infomaniak Mail.

- **Volume fits free, comfortably.** 300/day is orders of magnitude above a family suite's real need,
  with headroom for the December spike, at **CHF 0**.
- **EU data locality.** Brevo is a French company hosting in the EU — the residency posture NFR-004 wants
  for PII in transit, short of a Swiss-only sender.
- **It is a proper transactional service** — REST API and SMTP relay, DKIM signing, bounce handling and
  send logs — which is exactly what Infomaniak Mail is *not*.
- **Behind the seam.** The provider is an adapter behind the outbox port
  ([ADR-0018](0018-circle-and-sharing-model.md)); the domain and application layers never name Brevo, so
  swapping to Postmark later is an infrastructure change, not a refactor.
- **The role mailbox stays Infomaniak Mail** (`dev@<brand>`), the ADR-0030 follow-up address — a human
  inbox for store/security notices, distinct from the automated sender.
- **DNS is part of the decision, not an afterthought:** publish **SPF**, provider **DKIM**, and a
  **DMARC** record on the suite domain, so mail authenticates and deliverability holds.

## Consequences

- **Positive:** M1's verification/reset/email-change and M2's invitations become deliverable at zero
  recurring cost with EU data locality and real send logs to debug from.
- **Positive:** no coupling — the app already sends through the outbox port, so this is an adapter plus
  three DNS records.
- **Negative / accepted:** a third-party free tier carries the usual risks — rate caps, occasional
  deliverability wobble, and terms that can change. Mitigated by the seam: **Postmark is the documented
  paid upgrade** (~USD 15/mo) if deliverability ever underperforms, and it is a drop-in behind the port.
- **Negative / accepted:** transactional mail transits an EU (non-Swiss) provider. The payload is an
  address and a short-lived link, not stored account data; accepted as proportionate.
- **Follow-ups:**
  - Create the Brevo account against the project role address (not a personal one), and store the API key
    in the host/CI secret store — **never** in the repo ([ADR-0036](0036-cloud-provider-and-hosting.md)
    secrets follow-up).
  - Add **SPF / DKIM / DMARC** records when the [domain](../process/naming.md) is registered.
  - Point the outbox adapter's *from* address at the suite domain once naming lands (until then it is
    blocked on the same gate as the passkey RP-ID and `RevealBaseUrl`).
  - This closes the **deliverability** criteria carried by M1 and M2 (e.g. FR-017 in the
    [roadmap](../roadmap/roadmap.md)).
- **New risks:** dependence on a free tier's continued terms; guarded by the outbox seam and the named
  paid fallback.

## Pros and cons of the options

### 1. Brevo free tier (chosen)
- 👍 ~9,000 mails/mo free — vastly over-provisioned for family scale; EU-hosted; API + SMTP + DKIM + logs.
- 👍 Thin adapter behind the existing outbox port; Postmark is a clean paid upgrade if needed.
- 👎 Free tier terms and deliverability are a third party's to change; not Swiss-only.

### 2. Postmark
- 👍 Best-in-class deliverability and tooling.
- 👎 Only 100 mails/mo free, then ~USD 15/mo — a recurring cost the free tier avoids; US-centric.

### 3. Infomaniak SMTP / Mail relay
- 👍 No new provider; stays in the Swiss account.
- 👎 It is mailbox hosting, not a transactional service — no per-message DKIM tooling, bounce handling
  or send logs; deliverability for automated mail is not its job.

### 4. Mailgun (EU) / Scaleway TEM
- 👍 EU-hosted transactional APIs with good tooling.
- 👎 Smaller/'paid-sooner' free tiers than Brevo; no advantage here at this volume.

### 5. Self-hosted SMTP on the VPS
- 👍 No third party; full control.
- 👎 Deliverability is a full-time reputation problem (IP warming, blocklists); wrong use of one
  maintainer's time for a free hobby app.

## Links

- Related ADRs: [ADR-0018](0018-circle-and-sharing-model.md) (the outbox port this plugs into),
  [ADR-0036](0036-cloud-provider-and-hosting.md) (hosting, role mailbox, secrets),
  [ADR-0030](0030-mobile-store-publisher-identity.md) (the `dev@` role-address pattern)
- Requirements: [FR-007](../requirements/functional/FR-007-create-suite-account.md),
  [FR-009](../requirements/functional/FR-009-reset-password.md),
  [FR-013](../requirements/functional/FR-013-change-email-address.md),
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)
- Process: [naming](../process/naming.md) (domain gate — from-address and DNS)
- Runbook: [Deployment — Web / API / Database](../deployment/web-api-db.md)
