# Deployment — Launch-readiness assessment

A point-in-time assessment (**2026-09-15**, **updated 2026-09-18** after the stack went live) of whether CircleTools can go live, taken across both
solutions, the full test and architecture suite, the ADR log and the deployment scaffolding. It exists
to answer one question — *can we deploy today?* — and to name exactly what stands between the code and a
first production launch, so the remaining work is tracked rather than rediscovered.

This is a snapshot; the executable runbooks it points at are the milestone-8
[Web / API / Database](web-api-db.md) and [mobile](mobile-apps.md) checklists, and the manual
owner-action items are captured in the [backlog](../roadmap/backlog.md).

## Verdict

**Deployed and live (2026-09-18).** The stack is **serving in production** (owner-confirmed): the two
WASM PWAs and the API run on the Infomaniak VPS behind Caddy/TLS, **account sign-in and sync work
end-to-end**, **transactional email is delivering** (Brevo), and **nightly backups are running**. The
code and architecture were already production-grade at the 2026-09-15 assessment:

- both solutions build with **0 warnings / 0 errors**;
- **762 tests** green, plus **28 enforced architecture fitness tests**;
- **60+ ADRs**, all **Accepted**;
- a live self-hosted Grafana / Loki / Prometheus observability stack
  ([ADR-0051](../adr/0051-production-observability-backend.md)).

The original blockers are cleared: the box is up (§A), the legal pages are reachable (§B), the
one-way-door schema fixes are **applied and committed** (§D), the runtime-correctness items are
**resolved** (§C), and the **restore drill has been run successfully** (2026-09-17, §A). What is left
is **verification and polish, not blockers**: confirm production has run the 2026-09-15 one-way-door
migrations, and get a **light legal review** of the published legal pages. The mobile heads still have
their own residuals (§E).

## Blockers before deploy

### A. Operational provisioning (decided and now executed ✅)

Every step here was decided in an ADR and is **done as of 2026-09-18**; they map to the milestone-8
checklist in [web-api-db.md](web-api-db.md#checklist-milestone-8), now all `[x]`.

| Step | What | Status |
|---|---|---|
| VPS | Infomaniak VPS under a **project-owned account** ([ADR-0036](../adr/0036-cloud-provider-and-hosting.md)) | ✅ live |
| Secrets | vault → VM `.env` + GitHub Actions secrets; `DEPLOY_HOST` activates CD ([ADR-0050](../adr/0050-secrets-management.md)) | ✅ provisioned |
| DNS + mail | Infomaniak DNS A records at the VPS; Brevo + **SPF / DKIM / DMARC** ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md), [ADR-0037](../adr/0037-transactional-email-provider.md)) | ✅ mail delivering |
| TLS | Caddy **HTTP-01** on the VPS, ports 80/443 ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md)) | ✅ HTTPS live |
| Backups | `deploy/backup.sh` (restic + systemd timer); `restore.sh` ([ADR-0036](../adr/0036-cloud-provider-and-hosting.md)) | ✅ running; **restore drill PASSED** (2026-09-17, [backups](backups.md)) |
| First deploy | deploy + **full-flow smoke test** | ✅ sign-in / sync / email verified |

**Mail deliverability — the highest-leverage step — is working:** sign-up, verification, password
reset and invitations send via Brevo with SPF / DKIM / DMARC in place, so the onboarding path is not
silently broken.

**Backups run and the restore is proven.** On a single VPS the box is a single point of failure. The
nightly dump (restic → Swiss Backup) is running, and `deploy/restore.sh` has been **exercised
end-to-end** — the drill **PASSED** (2026-09-17, 33 tables restored across all five schemas; see
[backups](backups.md)) — so recovery is tested, not just written.

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

### C. Runtime correctness on the live stack (resolved ✅)

- **ForwardedHeaders middleware** is in place — configured and `app.UseForwardedHeaders()` runs before
  any scheme-reading middleware (`Main.Service/Program.cs:36-38, 264`), so scheme and client IP are
  correct behind Caddy.
- The **live-stack CSP pin** is confirmed: statically verified and boot-smoke-tested (2026-08-25), and
  now exercised in production — **sign-in and sync make the real cross-origin `connect-src` fetch to
  `api.circletools.ch`** and work, which was the last open confirmation.

### D. Database one-way doors (applied ✅)

These changes are cheap before real data and expensive or impossible after. All five are **applied and
committed** — in commit `76264f5` (2026-09-15), with EF configuration, migrations and model snapshots
in sync:

| Change | From | To | Status |
|---|---|---|---|
| Money precision | unbounded `numeric` | `numeric(10,2)` | ✅ `LinkingDbContext` + migration `…BudgetPrecisionAndMemberLinkUniqueIndex` |
| Share tokens + creator keys | stored in plaintext | hashed ([ADR-0061](../adr/0061-share-token-and-creator-key-at-rest-protection.md)) | ✅ `ShareSecretHash` in `SharedGroupStore` |
| Email hashes | unsalted SHA-256 | keyed **HMAC** ([ADR-0062](../adr/0062-email-address-hashing-keyed-hmac.md)) | ✅ `HmacEmailHasher` (`IEmailHasher`) |
| `member_links` uniqueness | not DB-enforced | partial unique index | ✅ filtered unique index in `LinkingDbContext` + migration |
| Missing indexes | — | on `shared.groups.OwnerAccountId` and `shared.member_claims.AccountId` | ✅ migration `…ProtectShareTokensAtRestAndAccountIndexes` |

**One verification remains:** confirm production has actually **run** the two 2026-09-15 migrations
(`…BudgetPrecisionAndMemberLinkUniqueIndex`, `…ProtectShareTokensAtRestAndAccountIndexes`) — i.e. the
live deploy post-dates them. If so, this door is closed.

### E. Mobile-only residuals (not needed for a web launch)

Not blocking a **web** launch, but required before the mobile heads ship and needing manual owner
action:

- ✅ **Play App Signing SHA-256 filled** (2026-09-18) in both `assetlinks.json` files (SecretSanta +
  Main WebApp), from Play Console → *Protected with Play → App signing*. Still open: the **Apple Team
  ID** in `apple-app-site-association` (`REPLACE_WITH_APPLE_TEAM_ID`), and **redeploy the web front** so
  the updated `.well-known/` files actually serve — then reveal / calendar deep links auto-open in the
  app instead of via the chooser / browser.
- The **iOS head builds on hosted macOS CI** ([ADR-0065](../adr/0065-hosted-macos-ios-builds.md),
  superseding [ADR-0031](../adr/0031-ios-build-host.md)) — no Mac needed. The residual is operational:
  set the iOS signing secrets and trigger the first `release-mobile.yml` build to TestFlight.

> **Update (2026-09-18):** both project developer accounts (Google Play, Apple Developer) are now
> **enabled**. The immediate mobile step is internal-testing installs on our own devices — no public
> review — per the *First milestone* section of the [mobile runbook](mobile-apps.md). The residuals
> above still stand.

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
