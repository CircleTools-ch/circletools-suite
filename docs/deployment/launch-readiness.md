# Deployment — Launch-readiness assessment

A point-in-time assessment (**2026-09-15**) of whether CircleTools can go live, taken across both
solutions, the full test and architecture suite, the ADR log and the deployment scaffolding. It exists
to answer one question — *can we deploy today?* — and to name exactly what stands between the code and a
first production launch, so the remaining work is tracked rather than rediscovered.

This is a snapshot; the executable runbooks it points at are the milestone-8
[Web / API / Database](web-api-db.md) and [mobile](mobile-apps.md) checklists, and the manual
owner-action items are captured in the [backlog](../roadmap/backlog.md).

## Verdict

**Not deployable today — but the gap is operational provisioning, legal pages and a few pre-data schema
fixes, not engineering.** The code and architecture are production-grade:

- both solutions build with **0 warnings / 0 errors**;
- **762 tests** green, plus **28 enforced architecture fitness tests**;
- **60 ADRs**, all **Accepted**;
- full deployment scaffolding is built — Docker Compose, Dockerfiles, Caddy reverse proxy, CI **and**
  CD workflows, and a live self-hosted Grafana / Loki / Prometheus observability stack
  ([ADR-0051](../adr/0051-production-observability-backend.md)).

What remains is bringing up the box, publishing the legal content, and closing a small set of
one-way-door database decisions before any real data is written.

## Blockers before deploy

### A. Operational provisioning (all decided, none executed)

Every decision here is already recorded in an ADR; what is missing is execution. These map to the
milestone-8 checklist in [web-api-db.md](web-api-db.md#checklist-milestone-8).

| Step | What | Reference |
|---|---|---|
| VPS | Provision the Infomaniak VPS under a **project-owned account** (not a personal login) | [ADR-0036](../adr/0036-cloud-provider-and-hosting.md) |
| Secrets | Generate every secret into the vault, then derive the VM `.env` and the GitHub Actions secrets; setting `DEPLOY_HOST` self-activates the CD job | [ADR-0050](../adr/0050-secrets-management.md) |
| DNS + mail | Point the **Infomaniak DNS** A records at the VPS, create a Brevo account, and add **SPF / DKIM / DMARC** on the domain ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md) — no Cloudflare) | [ADR-0037](../adr/0037-transactional-email-provider.md) |
| TLS | Bring up TLS — Caddy's default **HTTP-01** challenge on the VPS (no proxy in front); just open ports 80/443 | [ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md) |
| Backups | **Scripted** (`deploy/backup.sh` via restic + systemd timer; `restore.sh`) — owner provisions the Swiss Backup secrets and **runs the restore drill** | [ADR-0036](../adr/0036-cloud-provider-and-hosting.md) |
| First deploy | Deploy and run a **full-flow smoke test** with a clean browser console | — |

**Highest leverage: mail deliverability.** Undeliverable mail silently breaks sign-up, verification,
password reset and invitations — the entire onboarding path — so the Brevo account plus SPF / DKIM /
DMARC records are the single most valuable provisioning step.

**Biggest data-loss risk: backups.** On a single VPS, the box is a single point of failure and **no
backup script exists in `deploy/` today**. The nightly dump and, crucially, a *tested* restore must be
in place before real data lands.

### B. Legal content (written and reachable — pending a light legal review)

Store distribution and Swiss/EU data-protection law both require published legal pages: a **privacy
policy**, an **imprint / about-and-legal** page, and an **account-deletion** page, each in **DE / EN / FR**.

- Governed by [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)
  (revDSG + GDPR) and [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)
  (store distribution).
- **Written and reachable** at `/privacy`, `/imprint` and `/delete-account` in **both** web apps —
  shared, DE/EN/FR-localized content components in `CircleTools.Ui.Shared` behind thin per-app route
  wrappers, plus a footer linking them; verified rendering in the browser in all three languages.
- Written in a warm, first-person voice (a free personal project, not a corporate one). The operator /
  data controller is named as **Adrian Moll (circletools.ch)**, contact **info@circletools.ch**, with
  **no postal address** — a deliberate choice appropriate to a non-commercial project.
- **Still owed before go-live:** a **light legal review** of the wording. Note the address decision holds
  only while the project stays non-commercial and can declare **non-trader** status in the app stores; if
  it is ever monetised or store-classified as a trader, a postal address (or a legal entity such as a
  Swiss Verein) would likely be required.

### C. Runtime correctness to verify on the live stack

- **ForwardedHeaders middleware** was absent from `Program.cs` (needed behind the Caddy reverse
  proxy for correct scheme and client IP) — being fixed.
- The **live-stack CSP pin** is still owed: the policy is statically verified and boot-smoke-tested
  ([web-api-db.md](web-api-db.md#checklist-milestone-8)), but the final confirmation against the real
  cross-origin `connect-src` API fetch only happens on the live stack.

### D. Database one-way doors (fix before production data)

These are changes that are cheap now and expensive or impossible once real data is written, so they are
being applied before first deploy:

| Change | From | To |
|---|---|---|
| Money precision | unbounded `numeric` | `numeric(10,2)` |
| Share tokens + creator keys | stored in plaintext | hashed (ADR pending) |
| Email hashes | unsalted SHA-256 | keyed **HMAC** (ADR pending) |
| `member_links` uniqueness | not DB-enforced | partial unique index |
| Missing indexes | — | on `shared.groups.OwnerAccountId` and `shared.member_claims.AccountId` |

### E. Mobile-only residuals (not needed for a web launch)

Not blocking a **web** launch, but required before the mobile heads ship and needing manual owner
action:

- Replace the placeholder **Play App Signing SHA-256** in `assetlinks.json` (currently
  `REPLACE_WITH_PLAY_APP_SIGNING_SHA256`) with the real fingerprint so reveal / calendar App Links
  auto-open silently instead of via the chooser.
- The **iOS head is not built** ([ADR-0031](../adr/0031-ios-build-host.md)) — it needs a Mac.

## Strengths (project review)

The engineering that is done is done well:

- **Identity token rotation is production-grade** — refresh rotation with reuse detection
  ([ADR-0021](../adr/0021-web-auth-token-model.md)).
- **Circles invitation is privacy-first** — enumeration-safe responses and **dual rate limiting**.
- **Broad, multi-level test suite** — unit, integration and component tests plus real-Chromium **E2E**
  with a **WebAuthn** virtual authenticator.
- **Data minimisation is enforced by a build test** — `PersistedModelReviewTests` fails the build if a
  forbidden personal-data field appears in the EF model
  ([NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md)).

### Database review positives

- `timestamptz` used everywhere for timestamps;
- enums stored as strings, not brittle ordinals;
- **UUIDv7** primary keys;
- length-bounded `varchar` columns;
- isolated migration history per bounded context;
- migrations exercised in CI.

## Non-blocking watch-items

Known trade-offs, recorded so they are not surprises — none blocks launch:

- **Draw uses reject-sampling**, so it can spuriously report `OverConstrained` on tight, small rosters.
- **No on-device SQLite** — standalone storage is a JSON key-value store (web = `localStorage`,
  MAUI = file-per-key), written whole-collection under a single key
  ([ADR-0015](../adr/0015-mvp-client-offline-storage.md)).
- **`Ui.Shared` has thin test coverage** relative to the rest of the suite.
