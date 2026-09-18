# Backlog

Idea stubs and work items, newest-first. Promote items into
[brainstorming](../brainstorming/index.md) → [use cases](../use-cases/index.md) as they mature.

## First-run onboarding pass — Secret-Santa (2026-09-17)

A mobile-first review of the **first session** (empty app → names drawn → links shared), run as a
BA / UX-expert / designer council against the real 390px screens. Root cause it found: the primary
actions are present but **invisible or mislabelled on the first screens** — on a phone the empty-state
card's only visible button was **Sign in** (the optional account), while creating a group was a bare,
unlabelled "+" FAB; and the explainer said "…above" pointing at an inline field that is hidden on
phones. The account upsell was the loudest thing on the screen the app promises works "with no account".

**Done — SMALL items, built & browser-verified at 390px in DE/EN/FR (console clean):**

- **OB-1 · Primary "Create a group" button in the first-run empty card; Sign-in demoted to a text link.**
  `Groups.razor` empty state. New `EmptyStateCreateGroup` string (DE *Gruppe erstellen* / FR *Créer un
  groupe*). Shown only on narrow (the wide inline field already leads there, so no duplicate CTA on
  desktop). The very first tap now does the job, not account signup.
- **OB-2 · Fixed the empty-state explainer copy.** The **English** `EmptyStateExplainer` still said
  "Name a group above to start." (DGH-5's directional-copy fix had missed EN; neutral/DE/FR were already
  width-neutral). All four now read "…Create your first group to get started." (+ DE/FR equivalents).
- **OB-3 · Extended (labelled) FABs.** Groups → *Create group*, group hub → *Add occasion*, Members →
  *Add member*. The bare "+" read identically on every screen (it creates a group / an occasion / a
  member depending where you are); the label now says which, and doubles as the affordance a sighted
  non-screen-reader user was missing.
- **OB-4 · Draw is no longer a dead end.** `Draw.razor` under-two-members state was a bare sentence with
  no way forward; it now carries an **"Add members"** button linking to the Members page (new `GoToMembers`
  string). Verified: the draw page for a 0-member group shows the recovery button.
- **OB-5 · Occasion empty state copy + gating.** `NoEventsYet` dropped the "above"/"oben"/"ci-dessus"
  (same misdirection as OB-2) and now shows only once a group *has* members (the brand-new 0/0 case is
  covered by OB-6 instead of doubling up).
- **OB-6 · First-run hub hint.** On a brand-new group (0 members, 0 occasions) the hub — which leads
  with occasions (ADR-0055) — now shows a one-line **"Start by adding members, then add an occasion to
  draw names."** (new `HubFirstRunHint`), self-hiding as soon as either exists. Bridges the events-first
  IA for a first-timer with copy instead of restructuring.
- **OB-7 · "New version available" snackbar — not a real first-run defect (no change).** The council
  flagged it crowding the first paint; on inspection `UpdatePrompt` (ADR-0048) only fires from the
  service-worker *update* lifecycle, which does **not** trigger on a genuine first install (no prior SW).
  What shows in dev/repeat visits is the update mechanism working as designed. Left untouched (changing
  it risks ADR-0048); the on-screen crowding is a separate cosmetic snackbar-position question, parked.

**BIG — reviewed with the maintainer (2026-09-18) and implemented:**

- **OB-8 · Guided "first group" setup.** ✅ **Built & browser-verified (2026-09-18)** as a **progressive
  getting-started checklist** (the reviewed choice over a modal wizard) — [FEAT-019](../features/FEAT-019-getting-started-checklist.md)
  / [UC-SS-024](../use-cases/secret-santa/UC-SS-024-guided-first-group-setup.md). A non-modal card on the
  group hub sequences *name → add members → add an occasion → draw names*, ticks each off, disables "Draw"
  until an occasion exists, disappears once set up, and is **skippable** (device-local per group). Additive
  over [ADR-0055](../adr/0055-event-navigation-mobile-section-hub.md) (no ADR); supersedes the OB-6 hint.
  **Threshold finding:** "Add members" completes at **three** (not two — a two-person draw is forbidden by
  the no-reciprocal rule), so the checklist never reads done while the draw still fails. New `SecretSanta.Ui/Shared/GettingStartedChecklist.razor`;
  bUnit `GettingStartedChecklistTests` (5) — **Ui.Test 77/77**; full lifecycle verified in a real browser
  at 390px in EN + DE (empty → 3 members → occasion → draw → gone; skip + reload; draw locked until an
  occasion), console clean.
- **OB-9 · Per-screen FAB semantics.** ✅ **Codified (2026-09-18).** OB-3 already gave the FABs visible
  labels; the convention (extended labelled FABs; no dead ends; localize every string) is now written into
  [coding-guidelines §16](../guidelines/coding-guidelines.md) so new screens follow it. No further UI change.
- **OB-10 · "Reveal link" concept primer.** ✅ **Built & browser-verified (2026-09-18).** The Share page
  already reassures the *organizer* ("only the recipient can see who they drew"); added a **recipient-side**
  primer on the reveal page before the reveal tap — "This is your private Secret Santa link — only you can
  see who you got." (`RevealPrimer`, DE/EN/FR + neutral). Verified on the reveal page; RevealPageTests green.

## Mobile store-readiness & native passkeys — Secret-Santa (2026-09-17)

A store-requirements pass (Apple + Google 2026 rules, tablet/iPad priority) plus lifting the
mobile-passkey deferral now that the RP-ID `circletools.ch` is settled. Store-prep details also live in
the [mobile-apps runbook](../deployment/mobile-apps.md) "App preparation" section; the passkey decision
is [ADR-0066](../adr/0066-mobile-native-passkeys.md).

- **MSR-1 · iOS store-config parity (P1).** ✅ **Implemented 2026-09-17.** iOS `AppDelegate` now handles
  `ContinueUserActivity`/`OpenUrl` → `DeepLinkService` (reveal/event Universal Links open the app — was
  Android-only); new `Platforms/iOS/Entitlements.plist` (Associated Domains) wired via csproj
  `<CodesignEntitlements>`; `ITSAppUsesNonExemptEncryption=false`; privacy manifest gained
  `UserDefaults`/`CA92.1` + `NSPrivacyTracking=false`. iOS code doc-reviewed (can't compile on Windows,
  [ADR-0065](../adr/0065-hosted-macos-ios-builds.md)); device-verify on first TestFlight build. Android
  confirmed `targetSdkVersion=36` / `exported=true` in the merged manifest; tablet layout browser-verified
  at 13" iPad portrait+landscape (adaptive, no letterboxing). See GL-10.
- **MSR-2 · Native passkeys on the mobile heads (P1, [ADR-0066](../adr/0066-mobile-native-passkeys.md)).**
  ✅ **Android implemented & compiling; iOS implemented & doc-reviewed (2026-09-17).** `MauiPasskeyCeremony`
  is no longer a stub: **Android** via Credential Manager (`Xamarin.AndroidX.Credentials` 1.6.0.1, passes
  the server's WebAuthn JSON verbatim and returns the standard response JSON) — **builds 0 errors** (27
  NU1608 AndroidX-skew warnings, scoped to a warning via `WarningsNotAsErrors` in the App csproj; the
  PlayServicesAuth companion was dropped for pinning an incompatible older AndroidX graph, so pre-Android-14
  API 28–33 falls back to the FR-011 message — see the csproj note). **iOS** via AuthenticationServices
  (`ASAuthorizationPlatformPublicKeyCredentialProvider`, base64url↔`NSData`, iOS-16 gate) in
  `Platforms/iOS/MauiPasskeyCeremonyIos.cs`, reviewed against the Microsoft.iOS docs (fixed the generic
  `GetCredential<T>()` + `[SupportedOSPlatform("ios16.0")]` blockers) — **device-verify on TestFlight**.
  Seam (`IPasskeyCeremony`) and server unchanged. **Gated until go-live:** the passkey affordance keeps the
  FR-011 "not available here" fallback until the association files carry real ids (MSR-3) and the RP domain
  is live — password sign-in (FR-008) works throughout. Traceability: supersedes the FEAT-002 mobile-passkey
  deferral.
- **MSR-3 · Association files for deep links + passkeys (P2, go-live).** ✅ **Scaffolded 2026-09-17;**
  need real identifiers. Reveal-link **applinks** on `secretsanta.circletools.ch` (`assetlinks.json` +
  `apple-app-site-association`); passkey **`get_login_creds`/`webcredentials`** on the apex
  `circletools.ch` (Main WebApp `wwwroot/.well-known/*`) because the WebAuthn RP-ID is the apex
  (`Auth:Passkey:ServerDomain`). Placeholders: `REPLACE_WITH_PLAY_APP_SIGNING_SHA256` — ✅ **filled
  2026-09-18** (Android SHA-256 from Play App Signing, in both `assetlinks.json`, shared with GL-9) —
  and `REPLACE_WITH_APPLE_TEAM_ID` (iOS, still open). **Owner/CI:** fill the remaining id; **redeploy**
  so the updated files serve; ensure Caddy serves the
  extensionless `apple-app-site-association` as `application/json` (no redirect); enable Associated Domains
  on the App ID + regenerate the profile. Relates to GL-9, GL-10, [ADR-0066](../adr/0066-mobile-native-passkeys.md).
- **MSR-4 · Externally-blocked store gates (tracking only).** Not code: privacy-policy + account-deletion
  URLs (GL-6, hosting), DSA trader declaration (likely *trader* for a Swiss individual → EU), iPad-13"
  screenshots, App Privacy label / Data-safety form (must match the sync data), 16 KB-page-size verify on
  the release AAB (Play, 1 Feb 2027), Play closed-testing 12×14 days. Enumerated in the runbook checklist.

## Play Store submission prep — Secret-Santa (2026-09-17)

Found while preparing the Google Play Console submission (individual enrolment,
[ADR-0030](../adr/0030-mobile-store-publisher-identity.md)) and testing the signed-in app.

- **PSP-1 · Signed-in users have no in-app account-deletion path in the Secret-Santa app (bug, P1 — Play
  blocker).** Found 2026-09-17 by the maintainer testing the live signed-in app: groups can be deleted, but
  there is **no in-app way to delete the account**, despite the `/delete-account` legal page describing an
  "in-app" method. **Root cause (code-confirmed):** the working delete/export UI is `AccountData.razor`
  (route `/account/data`) and lives **only in `CircleTools.Main.Ui`** — the **Secret-Santa app has no
  account-data page at all** (no `Pages/Account/*` under SecretSanta). It is linked **only** from the Main
  app's drawer (`MainShell.razor` → "NavData"); the **shared `AccountMenu`** (top-right avatar, used by both
  apps, [ADR-0034](../adr/0034-shared-account-session-ui-block.md)) links to Profile + Security + Sign out
  only, **never to deletion**. The shared `LegalAccountDeletionContent` still renders an
  `Legal_Delete_InAppHeading/Body` section, so the Secret-Santa `/delete-account` page **promises an in-app
  path that does not exist there**. **Why it blocks submission:** Google Play's account-deletion policy
  requires an app that allows account creation to let users **request deletion from within the app**; the
  Secret-Santa head supports opt-in account/sync ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)),
  so it needs this. Deletion functioning only on the Main origin (a different app a Secret-Santa user may
  never open) does not satisfy it. **Recommended fix (architectural, closes it for both apps at once):**
  move the delete/export page from `Main.Ui` into the shared **`CircleTools.Ui.Account`** building block
  (where `Profile`/`AccountMenu` already live) and add a **"Delete account / My data" entry to the shared
  `AccountMenu`** — then the legal page's "In-app" promise becomes true everywhere. Mind the app-specific
  dependencies the page currently pulls (`AppStrings`, `CircleErrorText`, `AccountApiClient`, `IFileSaver`,
  `ProblemAlert`) when relocating. Backend deletion itself is complete and tested
  ([ADR-0022](../adr/0022-account-deletion-and-data-export.md); `AccountDeletionDispositionTests` drives it
  against the Main origin) — this is a **UI-surface/routing gap in the Secret-Santa head**, not a
  missing feature. Relates to GL-6 (the `/delete-account` content) and
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md).
  ✅ **Implemented, tested & browser-verified — done (2026-09-17).** Moved
  `AccountData.razor` (route `/account/data`) and `CircleErrorText` from `Main.Ui` into the shared
  **`CircleTools.Ui.Account`** block (page → `Pages/`, class → `Api/`, `namespace
  CircleTools.Ui.Account.Api`); migrated its strings into `AccountStrings` (DE/EN/FR + neutral, incl. a new
  app-neutral `DeleteDoneHome` replacing the Main-only "Dashboard" link and an `AccountMenuData` label);
  added a **"Data & account" entry to the shared `AccountMenu`** (top-right avatar) so both apps expose the
  delete/export path the legal page describes. Registered `IFileSaver` in both Secret-Santa heads so Export
  works there too (the privacy page promises it): `BrowserFileSaver` + `wwwroot/js/download.js` (WASM) and a
  new `MauiFileSaver` over the device share sheet
  ([ADR-0028](../adr/0028-browser-platform-adapters-block.md)). Both Secret-Santa heads (WASM + MAUI Android)
  build 0/0; Main.Ui.Test 75/75 and Architecture.Test 28/28 green (the delete/export component + circle-error
  coverage survived the move). **Clean full-solution build 0/0**; full test suite green once run without
  Docker saturation (Main.Service.Test 245/245, SecretSanta.E2E 5/5, SecretSanta.Ui 72/72, all others).
  **DE/EN/FR real-browser walkthrough passed** (fresh Aspire stack, `secretsanta-web`): registered → signed
  in → the **account menu now shows "Data & account"** (was absent) → the page renders in all three locales →
  **Export downloaded** `circletools-export-*.json` (the new `IFileSaver`/`BrowserFileSaver` wiring) →
  password enabled the delete button → deletion succeeded and showed "account is gone" with the new
  **"Back to the app"** link (`DeleteDoneHome`); console clean throughout. Not yet on-device (MAUI Android
  head builds 0/0 but the share-sheet export + menu entry are unverified under a finger — fold into the next
  Pixel pass).
- **PSP-2 · Privacy-policy accuracy (2026-09-17).** Two claims examined during the PSP-1 text audit.
  - **(a) "unconfirmed accounts cleaned up after 30 days" — ✅ already true (audit finding was a false
    negative).** The purge is implemented, tested, and running: `PendingAccountPurge.PurgeAsync` deletes
    `!EmailConfirmed && CreatedAt < now − RetentionOptions.PendingAccountLifetime` (30 days) in
    `Main.Service.Repository/PendingAccountRetention.cs`, `PendingAccountRetentionService` runs it hourly, and
    `AddCircleToolsRetention(...)` wires both at `Main.Service/Program.cs:120`. Covered by
    `PendingAccountPurgeTests` (4 tests, green in Repository.Test 58/58). The original "no job found" note was
    a too-narrow grep (searched `Main.Service`/`Identity`, but the job lives in `Main.Service.Repository` and
    is registered via a named extension). *Design note (not a defect):* the purge is a direct
    `context.Users.RemoveRange`, not the `IAccountDeletionCleanup` cascade — deliberate and safe because an
    unconfirmed account is gated out of circle ownership; if unconfirmed-but-signed-in accounts can ever
    accrue cross-schema (sync/linking) rows, revisit whether the FK cascade covers them.
  - **(b) "no hidden copy kept" vs backups — ✅ fixed (2026-09-17).** The nightly **restic backups** (GL-5)
    retain snapshots up to ~6 months, so deleted data survives in encrypted off-site backups. Reworded
    `Legal_Privacy_RetentionBody` (DE/EN/FR + neutral) to say the account is gone from the *live service* right
    away, with the only lingering trace in the encrypted disaster-recovery backups, which a deleted account
    drops out of within a few months as they roll over. `Legal_Delete_InAppBody`'s "no way back" left as-is
    (it speaks to user-side irreversibility, not backup absence). Ui.Shared builds 0/0. Still worth a glance in
    the **GL-6** legal review. Confirmed-true in the same audit: the "30-day sign-in-token window" (matches
    `RefreshTokenLifetime`), Infomaniak-Geneva hosting, Brevo/EU mail, and HMAC-scrambled invitation emails.

## Go-live — manual owner action (2026-09-15)

Tracked-so-it-is-not-rediscovered work items from the
[launch-readiness assessment](../deployment/launch-readiness.md): the suite is engineering-complete
(0/0 build, 762 tests, 60 Accepted ADRs, full deploy scaffolding) but not yet live. Each item below
**needs the project owner to act** — provisioning under a real account, publishing legal content, or a
manual fingerprint/Mac step — so none can be closed by code alone. Decisions are already recorded; these
are execution items against the milestone-8 [web-api-db](../deployment/web-api-db.md#checklist-milestone-8)
checklist.

- **GL-1 · Provision the Infomaniak VPS under a project-owned account (P1).** VPS Lite M, Geneva,
  Ubuntu LTS; Docker + Compose; not a personal login. [ADR-0036](../adr/0036-cloud-provider-and-hosting.md).
- **GL-2 · Generate secrets and load the two tiers (P1).** Generate into the vault, derive the VM
  `/opt/circletools/.env` and the GitHub Actions secrets; setting `DEPLOY_HOST` self-activates the CD
  job. [ADR-0050](../adr/0050-secrets-management.md).
- **GL-3 · Infomaniak DNS + Brevo account + SPF/DKIM/DMARC (P1, highest leverage).** Point the
  `circletools.ch` A records (`@`, `secretsanta`, `api`, `grafana`) at the VPS in the Infomaniak zone
  ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md) — no Cloudflare), and add the
  Brevo account + **SPF/DKIM/DMARC** records there too. Undeliverable mail silently breaks sign-up /
  verify / reset / invite — the whole onboarding path.
  [ADR-0037](../adr/0037-transactional-email-provider.md).
  ✅ **Verified (2026-09-17).** All four A records (`@`, `secretsanta`, `api`, `grafana`) resolve to the
  VPS (`<server IP>`); Brevo DKIM is delegated (`brevo1/brevo2._domainkey` → `b1/b2.circletools-ch.dkim.brevo.com`);
  DMARC is published (`v=DMARC1; p=none; rua=mailto:rua@dmarc.brevo.com`); SPF is
  `v=spf1 include:spf.infomaniak.ch -all`. End-to-end confirmed by a real account-creation whose
  verification mail was sent successfully — DMARC aligns via **DKIM** (Brevo's model), so the SPF record
  not including Brevo is fine. **Optional later hardening (non-blocking):** add `include:spf.brevo.com`
  for SPF alignment too, and tighten DMARC to `p=quarantine`→`p=reject` after reviewing `rua` reports.
- **GL-4 · Bring up TLS (P1).** ✅ **Resolved by dropping Cloudflare
  ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md), 2026-09-16).** The HTTP-01
  caveat only existed because of Cloudflare's proxy; with DNS staying at Infomaniak and **no proxy in
  front**, Caddy's default **HTTP-01** challenge reaches `:80` directly and issues Let's Encrypt certs for
  all origins on first boot. Reverted the scaffold to stock `caddy:2-alpine` + HTTP-01 (removed the
  `caddy-dns/cloudflare` build, the `acme_dns` directive, and `CLOUDFLARE_API_TOKEN` from `compose.yaml` /
  `.env.example`); dropped Cloudflare from the public **privacy/imprint** strings (DE/EN/FR) and swept the
  docs. **Owner action:** just open ports 80/443 and let the A records (GL-3) propagate before bringing the
  stack up — nothing else. See [server setup §9](../deployment/server-setup.md).
- **GL-5 · Nightly `pg_dump` → Swiss Backup + a tested restore (P1, biggest data-loss risk).**
  ✅ **Scripted (2026-09-16).** `deploy/backup.sh` streams a nightly `pg_dump` into an encrypted,
  deduplicated **restic** repo on Swiss Backup (S3), prunes (7 daily / 5 weekly / 6 monthly) and runs
  `restic check`; `deploy/systemd/circletools-backup.timer` schedules it 03:30 nightly; `deploy/restore.sh`
  does a **safe restore drill** into a scratch DB by default (`--target prod` for a real recovery). restic
  chosen as the tool (encryption + dedup + retention + verify in one binary; recorded in the runbook, no
  ADR — ADR-0036 fixes the policy). Runbook [Backups & restore](../deployment/backups.md); secrets templated
  in `deploy/.env.example`. ✅ **Closed on the VM (2026-09-17).** Swiss Backup S3 access + secrets
  provisioned in `/opt/circletools/.env`; the restic repo was initialised at
  `s3.swiss-backup02.infomaniak.com/circletools-backup/circletools`; a manual `backup.sh` ran green
  (snapshot saved, `restic check` reported *no errors*); the `restore.sh` drill **PASSED** — 33 tables
  restored across all five schemas (circles/linking/public/shared/sync) into a scratch DB and dropped,
  without touching live data; and `circletools-backup.timer` is installed + enabled (nightly ~03:30 UTC,
  confirmed via `systemctl list-timers`). The first dump was ~56 KiB (9 KiB stored after
  compression/dedup), so the 200 GB Swiss Backup quota is effectively unbounded for this workload.
  **Residual (non-blocking, tracked as GL-11):** no backup-failure alerting yet.
  [ADR-0036](../adr/0036-cloud-provider-and-hosting.md).
- **GL-6 · Legal review of the legal pages (P1).** Privacy policy + imprint (about & legal) +
  account-deletion page are **written and reachable** at `/privacy`, `/imprint`, `/delete-account` in
  both web apps, DE/EN/FR, browser-verified, in a warm first-person voice. Operator/controller named as
  Adrian Moll (circletools.ch), contact info@circletools.ch, no postal address (non-commercial choice).
  Remaining: a **light legal review** of the wording before go-live; and if the project is ever monetised
  or store-classified as a **trader**, add a postal address or a legal entity (e.g. a Swiss Verein).
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md),
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md).
- **GL-7 · Pin the CSP on the live stack (P2).** The policy is statically verified and boot-smoke'd;
  the final confirmation against the real cross-origin `connect-src` API fetch only happens live.
  [ADR-0021](../adr/0021-web-auth-token-model.md), [ADR-0040](../adr/0040-ui-component-library-mudblazor.md).
- **GL-8 · Live on-device sign-in / sync test (P2).** Drive account sign-in and sync from a real device
  against the running Main service — still owed since the parity work.
  [ADR-0039](../adr/0039-cross-app-single-sign-on.md).
- **GL-9 · Replace the `assetlinks.json` Play App Signing SHA-256 (P2, mobile).** ✅ **Done 2026-09-18** —
  the real fingerprint (`BB:EF:78:…:F1:EC`) from Play App Signing is in both `assetlinks.json` files, so
  reveal / calendar App Links auto-open silently instead of via the chooser. Residual: **redeploy** the
  web front so the updated `.well-known/assetlinks.json` serves.
  [ADR-0053](../adr/0053-backend-free-first-mobile-release.md).
- **GL-10 · Build the iOS head + wire iOS deep links (P2).** ~~Mac-gated~~ **No longer Mac-gated**
  ([ADR-0065](../adr/0065-hosted-macos-ios-builds.md): iOS builds on hosted macOS CI). ✅ **iOS deep
  links wired in code (2026-09-17):** `Platforms/iOS/AppDelegate.cs` now handles `ContinueUserActivity`
  (Universal Links) + `OpenUrl` and forwards to `DeepLinkService` (parity with Android's `MainActivity`);
  `Platforms/iOS/Entitlements.plist` declares `applinks:secretsanta.circletools.ch` (+
  `webcredentials:circletools.ch` for passkeys, [ADR-0066](../adr/0066-mobile-native-passkeys.md)), wired
  via csproj `<CodesignEntitlements>`; the `apple-app-site-association` files are scaffolded on both
  origins (see GL-9). **Still owner/CI:** the iOS head has not been built/run on a device yet (no local
  Mac — first TestFlight build verifies deep links + passkeys on iPadOS/iOS), and the Associated Domains
  capability must be enabled on the App ID + provisioning profile regenerated.
  [ADR-0053](../adr/0053-backend-free-first-mobile-release.md).
- **GL-11 · Alert on backup failure (P2, non-blocking).** ✅ **Implemented (2026-09-17).**
  `circletools-backup.service` now carries `OnFailure=circletools-backup-failure@%n.service`, a templated unit
  that runs `deploy/notify-failure.sh` — it composes a message (host, time, last 20 journal lines) and sends it
  via **email** (reuses the Brevo SMTP login, `ALERT_EMAIL_TO`) and/or a **webhook** (`ALERT_WEBHOOK_URL` —
  ntfy/healthchecks/chat), always logging to the journal as a fallback; best-effort, always exits 0. See the
  backups runbook [Monitoring & failure alerting](../deployment/backups.md#monitoring--failure-alerting-gl-11).
  **Still owner action on the VM:** install `circletools-backup-failure@.service` (step 4) and set at least one
  `ALERT_*` channel in `.env`. A `restic snapshots` freshness "backup is fresh" signal into Grafana
  ([ADR-0051](../adr/0051-production-observability-backend.md)) remains a nice-to-have.
  [ADR-0036](../adr/0036-cloud-provider-and-hosting.md).
- **GL-12 · Confirm the live web apps actually boot in a real browser (verification, P2).** Raised
  2026-09-17. An initial `WebFetch` of `https://circletools.ch` *appeared* to show the Blazor *"An unhandled
  error has occurred."* bar — but that was a **false positive**: that banner is `display:none` boilerplate
  present in every Blazor WASM `index.html` (identical in the served shell and in-repo), and `WebFetch`
  cannot execute JavaScript, so it never booted the app and just rendered the hidden div's text.
  **Server-side checks are all healthy** (2026-09-17): both `circletools.ch` (Main) and
  `secretsanta.circletools.ch` return 200 with the correct shell + loading spinner, the strict CSP header is
  served by Caddy per [ADR-0021](../adr/0021-web-auth-token-model.md) (incl. `wasm-unsafe-eval`), HSTS +
  security headers present, and the API responds (structured ASP.NET `problem+json`; `/health` path is 404
  only). **What's still unverified:** that the WASM runtime *boots to a rendered UI* — curl proves the shell
  and assets are served, not a clean boot. **Do:** open both apps in a real browser (JS on), confirm they
  render and the console is clean — the CLAUDE.md "verify in a real browser" rule, and owed before store
  submission since the listing needs live `/privacy` `/imprint` `/delete-account` (GL-6,
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)). Not a Play *signup* blocker.
  ✅ **Verified & closed (2026-09-17).** The maintainer opened both `circletools.ch` (Main) and
  `secretsanta.circletools.ch` in a real browser: each shows the normal short WASM loading indicator (the
  `loading-progress` spinner while the .NET runtime + assemblies download) and then renders the app. No error
  bar, no defect — the original signal was purely the `WebFetch` false positive described above. Standing
  reminder unchanged: the substantive pre-submission need is that `/privacy` `/imprint` `/delete-account`
  render correctly in DE/EN/FR (GL-6), which is a separate content check, not a boot problem.

## Transactional email — branded HTML & per-app identity (2026-09-17)

Follow-ups from [FEAT-018](../features/FEAT-018-branded-transactional-emails.md) /
[ADR-0064](../adr/0064-transactional-email-html-and-per-app-branding.md) — branded HTML mail with per-app
co-branding and source-app link origins is built and unit-tested; these are the deferred bits:

- **TEM-1 · White-on-transparent PNG email logo per app (P2).** ✅ **Done (2026-09-17).** White-glyph
  source SVGs + rendered 200×200 transparent PNGs (`email-logo-main.png`, `email-logo-secretsanta.png`) live
  in `CircleTools.Ui.Shared/wwwroot/img/`, served by every host at `_content/CircleTools.Ui.Shared/img/`;
  `Mail:Apps:<app>:LogoUrl` is set in `appsettings.Production.json` to each app's own origin, so the header
  `<img>` now renders (white circle-of-people on navy / white gift on maroon), verified by
  `MailTemplateRendererTests`. Dev keeps the wordmark-only header (no absolute origin).
- **TEM-2 · Explain the reusable CircleTools login when app #2 ships (P3).** With only Secret-Santa live, an
  SSO explainer in mail would confuse more than help; the footer carries only a subtle "CircleTools account"
  seed for now. Add the real "one login across CircleTools apps" explainer when a second app exists — which
  is also when the per-app link origins start earning their keep.
- **TEM-3 · Real-client visual pass + farewell integration test (P2).** ✅ **Mailpit pipeline pass done
  (2026-09-17):** all 12 representative variants were delivered through the real renderer + SMTP into Mailpit
  and verified (per-app colour/sender/origin, CTA + localized fallback, plaintext alternative, DE/EN/FR,
  farewell, HTML-encoding). **Still owed:** a human visual eyeball in the Mailpit UI and one **real external
  client** (Gmail / Outlook / Apple Mail) across DE/EN/FR and both apps, per the CLAUDE.md "verify in a real
  client" rule; and an integration assertion that a farewell mail follows a successful deletion.

## Shared-by-link sync — Secret-Santa (2026-09-14)

Found while re-testing the account-free **link-shared groups** ([FEAT-013](../features/FEAT-013-account-free-link-shared-groups.md),
[ADR-0058](../adr/0058-account-free-link-shared-groups.md)) end-to-end in **two independent real browsers** against
the live Aspire stack — the admin↔admin path the earlier pass missed (it only drove the participant *join* link).

- **SBL-1 · Shared-by-link changes never pull on a plain refresh while signed out (bug, P1).** ✅ **Fixed,
  browser-verified.** A co-organizer who opened the **admin link** in a second browser and added members saw them
  pushed to the server correctly, but the **creator's browser never showed them on refresh** — not even on repeated
  refreshes. Same class as **DGH-6**: a sync path that only fires when signed in, applied to data that is
  account-free. `AutoSyncService.RunSharedCycleAsync` is explicitly built to reconcile shared-by-link groups
  **signed in or out**, but the **app-start trigger** in `InitializeAsync` only called `RequestSync()` when
  `CurrentAccountAsync() != Guid.Empty` — so a signed-out shared-by-link holder's cycle only ran after a *local*
  edit (which then clobbers the un-pulled change via last-write-wins), a reconnect, or (touch-only) pull-to-refresh.
  A plain desktop refresh triggered nothing. **Fix:** `InitializeAsync` now calls `RequestSync()` unconditionally on
  app start — the shared cycle runs first and always, the account cycle self-gates on sign-in, and the whole cycle is
  a cheap no-op when there is nothing to reconcile. **Diagnosis proof:** before the fix, adding any member on the
  creator's device (which *does* trigger `RequestSync`) immediately pulled the co-admin's members on the next refresh,
  isolating the bug to the missing trigger rather than the reconcile. **Browser-verified (two browsers, live stack):**
  B1 creates a shared group → B2 opens the admin link and adds Carol+Dan (server reaches v3) → **B1 pulls both on a
  pure refresh with no local edit** (was empty on every refresh before the fix); console clean.
- **SBL-2 · Section pages didn't refresh in place when a background sync landed (bug, P2).** ✅ **Fixed,
  unit- and two-browser-verified (2026-09-15).** Follow-on found by the maintainer editing the same group in two admin windows: after SBL-1 a
  co-admin's edit *did* sync, but the open page only showed it on the *next* refresh — the refresh that triggered the
  pull rendered the pre-pull state, so it felt like "several refreshes" or "only a force-refresh (Ctrl+Shift+R) works."
  Confirmed it was **not** the HTTP cache (verified: a default-cache fetch and a `no-store` fetch both returned the
  fresh version; the API sends no cache headers but Chrome was not serving stale) — the page simply never re-read the
  store after a background pull. **Fix:** a small `ISyncSignals` seam (just the `SyncCompleted` event, implemented by
  `AutoSyncService`, resolved to the same instance) that the group/event **read** pages — Members, group hub
  (`GroupEditor`), Exclusions, `EventHub`, Draw, Share — subscribe to and reload on, mirroring what the shell already
  does for the drawer. The two **form** pages (`EventEditor` details, `EventWishlists`) were deliberately left out: a
  live reload there would discard the editor's in-progress typing. A page depends only on the notification, so a
  component test supplies a trivial `FakeSyncSignals` instead of the whole sync graph. Covered by a new bUnit
  regression (`MembersPageSyncRefreshTests`: firing `SyncCompleted` swaps the roster in place); **37/37 Ui tests
  green**. **Two-browser re-verify done (2026-09-15, live Aspire stack, two isolated headless-Chrome devices over
  CDP):** device 1 created a shared-by-link group (member *Anna*) and opened Members; device 2 opened the **admin
  link** (group adopted with the *Shared by link* chip, *Anna* pulled) and added *Carol* → pushed. With device 1's
  Members page **open and idle** (Anna, Carol; no *Erin*), device 2 added *Erin*; a background cycle then landed on
  device 1 and **Erin appeared in place — no navigation, no manual refresh** (the `SyncCompleted` reload), the
  add-member field left undisturbed. Console clean throughout. This also re-confirmed **SBL-1** (device 1's fresh
  signed-out load pulled Carol without a hard refresh). Still a known limit (P3, parked): an **idle** open tab with no trigger doesn't pull at all — a co-admin's
  change needs a refresh (or reconnect / pull-to-refresh) to *start* a cycle; a focus/`visibilitychange` or periodic
  pull would make an idle tab live, but that nudges the deliberately-parked continuous-sync decision
  ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md)).
  - **Also observed (expected, note only):** shared reconcile is last-write-wins per aggregate, so a local edit made
    on one device *before* it has pulled a newer server version is silently superseded on the next cycle. Inherent to
    the ADR-0058 whole-aggregate model; flagged so it is a known trade-off, not a surprise.

## Competitor-research candidates — Secret-Santa (2026-09-02)

Promoted from the [DE/EN/FR app-store competitor & review scan](../brainstorming/2026-09-02-secret-santa-competitor-research.md).
The scan's headline is that our **no-account / offline / link-or-QR / non-spoiling-reveal / draw-that-never-repeats**
spine is exactly what reviewers reward and incumbents get wrong — so these are the *on-strategy additions* it
surfaced, not a pivot. Each is a stub; take through the [process](../process/ways-of-working.md) (use case →
requirement → feature spec) if pursued. The QR work already in flight is **MNF-5** in the
[distribution cluster](#mobile-native-feel-pass--secret-santa-2026-09-02); reminders are **MNF-8** below (this scan
raises its motivation).

- **CR-1 · Recurring event carries wish lists + exclusions forward (P1).** Highest-value new candidate. Reviewers
  in all three markets complain that copying last year's event **loses the wish lists** (children's lists must be
  re-typed) and sometimes the exclusions too (DrawNames, Giftster). When creating a new occasion from a group that
  already has a past one, offer to **carry forward each member's wishlist and the exclusion set** (editable, not
  forced). Fits the events-as-first-class model ([ADR-0042](../adr/0042-events-as-first-class-entities.md)) and the
  member-archival/draw-history work ([ADR-0052](../adr/0052-member-archival-and-permanent-draw-history.md)). Design
  q: carry-forward as a copy at creation vs. a live link to the prior occasion.
  **✅ Promoted through the process (2026-09-02):** [UC-SS-014](../use-cases/secret-santa/UC-SS-014-carry-forward-new-occasion.md)
  → [FR-035](../requirements/functional/FR-035-carry-forward-new-occasion.md) →
  [FEAT-010](../features/FEAT-010-carry-forward-new-occasion.md) / [TP-010](../features/TP-010-carry-forward-new-occasion.md),
  new glossary term [Carry-forward](../glossary/carry-forward.md). Scope **sharpened against the model**: exclusions
  already carry (shared at group level, FR-034), so carry-forward copies **wishlists + budget** and seeds the new
  event's **draw-history baseline** with the source draw so the next draw won't repeat last year's pairing — which
  also answers the "same recipient every year" complaint. Copy-at-creation chosen (events stay independent, ADR-0042);
  no new ADR. Distinct from the account-only linked-member self-copy (`CopyFrom`, FR-030). ✅ **Built & browser-verified
  2026-09-02** ([FEAT-010](../features/FEAT-010-carry-forward-new-occasion.md) *Done*): `Group.AddEventFrom`
  + an `Event` baseline draw (separate from the draw history, so a carried occasion reads as undrawn but its
  first draw avoids last time's pairings), `CarryForwardEventDialog` on the group hub, and a "draw anyway"
  escape on the Draw screen for the rare over-constrained roster. Solution builds clean; 198 tests green
  (incl. 8 new); Playwright DE/EN/FR walkthrough clean-console, carried occasion draws.
- **CR-2 · Budget minimum *and* maximum (P2, cheap).** Every surveyed app offers only a **max** price; a recurring
  wish is a **minimum** too (so cheap-out gifts don't undercut the exchange). The event already has budget fields
  ([per-event editor](../features/FEAT-007-events-per-group.md)); add an optional lower bound and show the range on
  the reveal link. Small, closes a universal gap. Watch localization: CHF/EUR formatting per
  [ADR-0056](../adr/0056-decouple-ui-language-from-formatting-region.md).
  **✅ Promoted through the process (2026-09-03):** [UC-SS-015](../use-cases/secret-santa/UC-SS-015-budget-minimum-and-maximum.md)
  → [FR-036](../requirements/functional/FR-036-budget-minimum-and-maximum.md) →
  [FEAT-011](../features/FEAT-011-budget-minimum-and-maximum.md) / [TP-011](../features/TP-011-budget-minimum-and-maximum.md),
  glossary [Budget](../glossary/budget.md) amended to define the range. Design calls (recorded in the FEAT, **no ADR**):
  the existing single amount **is the maximum** and the new field is an optional floor (`0 ≤ min ≤ max`, one currency);
  min-only budgets are not offered. The minimum rides the reveal payload as an **additive optional field** (`bm`)
  **omitted when unset**, so max-only and pre-existing links are byte-for-byte unchanged and the payload version does
  not bump. ✅ **Built & browser-verified 2026-09-03** ([FEAT-011](../features/FEAT-011-budget-minimum-and-maximum.md)
  *Done*): `Budget.MinAmount` + invariant, a **Minimum** field on the event-details editor (old "Amount" relabelled
  **Maximum**), and a shared `BudgetDisplay.Format` rendering "10–30 CHF" (range) or "30 CHF" (bare max) on the reveal +
  my-assignments screens. Solution builds clean; **208 tests green** (incl. 10 new); Playwright DE/EN/FR walkthrough
  clean-console — range shown, max-only unchanged, min-exceeds-max rejected inline.
  **Gap found & closed (2026-09-03):** at ship time the range reached only the account-free **reveal link** — a linked
  member's *my-assignments* rebuilds from a server projection that stored only budget amount+currency, so it showed the
  bare max. **CR-3** carried the minimum through that projection (new `BudgetMinAmount` column + Linking migration,
  needed there anyway for the gift note), so my-assignments now shows the range too. FEAT-011 corrected accordingly.
- **CR-3 · "Do-not-gift" / dietary & lifestyle notes per member (P2).** Distinct from person-*exclusions* (who draws
  whom): a free-text/tagged **"please don't get me…" / allergies / dietary / lifestyle** note the giver sees on the
  reveal. Repeatedly wished-for across markets, done well by nobody. Rides on the existing wishlist plumbing and the
  self-contained reveal payload ([ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md)) — mind the encoded-link
  size budget. Glossary: may want a term (extends [wishlist](../glossary/wishlist.md)).
  **✅ Promoted through the process (2026-09-03):** [UC-SS-016](../use-cases/secret-santa/UC-SS-016-member-gift-note.md)
  → [FR-037](../requirements/functional/FR-037-member-gift-note.md) →
  [FEAT-012](../features/FEAT-012-member-gift-note.md) / [TP-012](../features/TP-012-member-gift-note.md),
  new glossary term [Gift note](../glossary/gift-note.md). **Design call (recorded, no ADR):** modelled **on the
  `Member`** (a stable per-person attribute — an allergy is the same every occasion), *not* per-event like a wishlist,
  so it is set once and shown on every draw; organizer-authored in v1 (linked-member self-authoring parked). Free text
  capped at **500 chars** to protect the encoded link; carried as an **additive optional** reveal-payload field (`gn`)
  omitted when unset (version unchanged). ✅ **Built & browser-verified 2026-09-03**
  ([FEAT-012](../features/FEAT-012-member-gift-note.md) *Done*): `Member.GiftNote` + `Group.SetMemberGiftNote`
  (independent of rename), a gift-note field on the members editor, and a **"Good to know"** line on the reveal +
  my-assignments (after the giftee is revealed). The linked-member path required a **Linking Postgres migration**
  (`AddAssignmentBudgetMinAndGiftNote`) adding `RecipientGiftNote` **and** `BudgetMinAmount` to the assignment
  projection — which **also closed the CR-2 gap** (the budget range now reaches my-assignments, not only the reveal
  link). Solution builds clean; **218 SecretSanta+arch tests green** (incl. 11 new) + a Docker-only Main projection
  test; Playwright DE/EN/FR walkthrough clean-console.
- **CR-4 · Schrottwichteln variant — each person draws 2 recipients (P3, DACH differentiator).** A culturally
  distinct DACH format (each participant is Wichtel for **two** people) that **native apps largely ignore** — only
  web tools (Wichtlify) model it. Sits naturally with the Swiss-first stance. Needs a draw-algorithm variant (the
  fair-draw/no-reciprocal/no-repeat invariants must extend to a 2-recipient assignment) and a reveal that shows both
  giftees. Bigger than CR-1..3; spec as its own feature if pursued. Ubiquitous-language check first — add a
  **Schrottwichteln** glossary entry before naming it in code.

## Drawer & group-hub refinements — Secret-Santa (2026-09-02)

Follow-ups from the maintainer's use, on top of the [chrome/navigation review](#mobile-chrome--navigation-review--secret-santa-2026-09-02).

- **DGH-6 · Deleted groups lingered in the drawer quick-access (bug, P1).** ✅ **Fixed, browser-verified.**
  A group deleted **while signed out** stayed in the drawer's quick-access until the next navigation/sync:
  the after-change trigger (`AutoSyncBehavior` → `RequestSync`) only reaches the drawer via `SyncCompleted`,
  which a signed-out cycle never fires. Added a **`LocalChanged`** event on `AutoSyncService` (raised by
  `RequestSync`, so on every mutation regardless of sign-in); the shell subscribes and re-reads
  quick-access, which filters by `VisibleGroupsService` so a deleted group drops immediately. Browser-verified
  (390px, signed out): create → group in drawer; delete (no navigation) → gone from the list **and** the
  drawer.

- **DGH-1 · Settings pinned to the drawer bottom + bigger nav targets.** ✅ **Built, browser-verified.**
  The drawer is now a full-height flex column: the main nav scrolls in the top region and **Settings sits
  pinned at the very bottom** (empty space between when there are few groups/events), so it reads as an
  app-level control. Drawer nav links get a **≥48px** target and a slightly larger font on narrow
  viewports. Browser-verified (390px): Groups link 48px tall; Settings link top at y≈796 of 844.
- **DGH-2 · Group hub (ADR-0055 amendment).** ✅ **Built, browser-verified; device pass pending.** The
  group landing is now a **hub** matching the event hub ([FEAT-009](../features/FEAT-009-event-section-hub.md),
  [ADR-0055 amendment](../adr/0055-event-navigation-mobile-section-hub.md)): the **occasions list** is the
  primary content, with **Members** and **Exclusions** as `ct-hub-row` rows (icon + count + chevron) below;
  `GroupNav` tabs retired, `GroupBackNav` added to the Members/Exclusions pages. Browser-verified (390px,
  en-US): no tab strip; Members/Exclusions rows with counts; adding an event lists it; row → `/members`
  with a back-to-group link; console clean.
- **DGH-3 · Group rename moved into the header overflow menu.** ✅ **Built, browser-verified.** The
  always-visible group-name text field is gone; rename lives behind the header **⋮ → "Rename"**, opening
  the W4 `TextInputSheet` (extended to pre-fill the current name). **Suggested label: "Rename"
  (Umbenennen)** while the name is the only group setting; promote to **"Group settings"** if more arrive.
  Browser-verified: sheet pre-fills "Familie"; renaming updates the hub title.
- **DGH-4 · Pinned Settings link clipped by the screen corner (P2).** ✅ **Built, browser-verified.** The
  bottom-pinned Settings link sat flush against the phone's rounded corner / gesture bar. Added
  `padding-bottom: calc(env(safe-area-inset-bottom) + 12px)` to `.ct-drawer-bottom` so it clears both.
  Browser-verified (no inset): 12px gap; on device the gesture-bar safe area adds more. **Confirm on the Pixel.**
- **DGH-5 · Create group / add event via a FAB (P1).** ✅ **Built, browser-verified; device pass pending.**
  Same pattern as adding members (FEAT-008 W4): on a phone the inline **create-group** field (Groups) and
  **add-event** field (group hub) are hidden and a **"+" FAB** opens a bottom sheet to enter the name — so
  each screen leads with the *existing* groups/events. The add-event sheet **pre-fills the default name**
  (e.g. "Christmas 2026"). Onboarding copy reworded to drop directional "above/oben/ci-dessus" (now width-
  neutral: "Add a group to get started."). Inline fields stay on wide. Browser-verified (390px, en-US):
  Groups FAB → sheet → group hub; events FAB → **pre-filled** sheet → event hub; inline fields hidden on
  narrow; console clean. **Confirm the FAB feel on the Pixel.**

## Mobile chrome & navigation review — Secret-Santa (2026-09-02)

A pass over app-chrome placement and navigation against mobile conventions, driven by the maintainer's
own use, on top of the [native-feel pass](#mobile-native-feel-pass--secret-santa-2026-09-02) below.

- **MCN-1 · First-run language follows the device (P1).** ✅ **Built 2026-09-02.** The app used to start
  in `de-CH` for everyone with no stored preference — it never looked at the device. Now
  `CultureProvider.InitializeAsync` uses the device/browser language on first run, and `AppCultures.Resolve`
  gained **language-prefix matching** (any `de-*` → de-CH, `fr-*` → fr-CH, `en-*` → en, else de-CH). A
  stored pick still wins; a signed-in account's language still overrides on sync
  ([ADR-0035](../adr/0035-profile-sync-seam.md)); nothing is persisted on first run, so the default keeps
  following the device until the user picks. **Browser-verified** (Playwright): browser `de-DE` → *Gruppen*,
  `en-US` → *Groups*, `fr-FR` → *Groupes*.
- **MCN-2 · Event section hub instead of router-tabs (P1).** ✅ **Built 2026-09-02, browser-verified;
  device pass pending.** [ADR-0055](../adr/0055-event-navigation-mobile-section-hub.md) **Accepted** and
  realised as [FEAT-009](../features/FEAT-009-event-section-hub.md): the event landing is now a **section
  hub** on all widths — a vertical list of Details / Wishlists / Draw / Share, each a tappable card with an
  icon, a one-line **status** ("Not drawn yet", "{x} of {y}", "Drawn"…) and a chevron — replacing the
  horizontally-scrolling tab strip (`EventNav` retired). Details gained its own `/details` route; each
  section page shows a "← Overview" back-link (`EventBackNav`). Refinement during build: the hub is the
  landing on **all** widths (not "hub-narrow / tabs-wide") because the responsive split forced the landing
  route to be two screens by width — see the ADR. Four new strings in all locales; parity green; Ui suite
  **29/29**. **Browser-verified** (Playwright, 390px, en-US): hub is the landing, no tab strip, four rows
  with status, row → route → back-to-Overview → hub, console clean. **Still to confirm on the Pixel:** the
  hub under a real finger in DE. Deferred: whether the **group level** (`GroupNav` tabs) gets a matching hub.
- **MCN-3 · App chrome → a Settings screen (P2).** ✅ **Built 2026-09-02, browser-verified; device pass
  pending.** New `/settings` page (`Pages/Settings.razor`) gathers **Appearance** (System/Light/Dark),
  **Language**, and the build **version** into one screen, composing the existing shared `ThemeState` /
  `CultureSelector` / `AppVersion`. The **theme switch is removed from the app bar** (native-mobile
  placement; declutters the bar on web too) and the drawer's language/version block is replaced with a
  single **Settings** nav link. One screen for web + mobile (no divergence). Two new strings
  (`SettingsTitle`, `SettingsAbout`) in all locales; parity green; Ui suite **29/29**. **Browser-verified**
  (Playwright, 390px): the drawer Settings link opens `/settings`; Appearance + Language dropdowns + a
  version label render; selecting **Dark** flips the app to dark (body background light→dark); app bar has
  no theme toggle; console clean. **Still to confirm on the Pixel** in DE. The signed-in language
  device-note + Profile link moved onto the Settings page unchanged.
- **MCN-4 · Account entry top-right — keep.** The account/avatar menu top-right is the convention
  (kept). Minor refinement to consider: make the **signed-out** state read clearly as "Sign in" rather
  than an ambiguous avatar. No structural change.

## Mobile native-feel pass — Secret-Santa (2026-09-02)

The MAUI head is at web parity and heading for a backend-free v1
([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)); the maintainer's priority is now
**mobile-first — the phone gets the most attention**. An on-device/on-screen review asked how to make
it feel more native. The governing decision — **one neutral Material design across web + both device
heads, with "native feel" earned through motion, touch ergonomics and OS integration rather than
per-platform (Cupertino) chrome** — is recorded as
[ADR-0054](../adr/0054-mobile-native-feel-strategy.md). The near-term implementable subset (levers 1–4)
is specified as [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md); the rest are stubs below,
prioritized. This extends the [R2-6 mobile touch pass](#ux-round-2--secret-santa-usability-2026-09-01)
and consumes the [mobile brainstorm (2026-09-01)](../brainstorming/2026-09-01-secret-santa-mobile.md).

**In [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md) (levers 1–4 — build first):**

- **MNF-1 · Touch-ergonomics completion (P1).** ✅ **Built 2026-09-02.** A shared **mobile density
  baseline** in `circletools.css` (narrow breakpoint) gives every `.mud-icon-button` a ≥48 px hit area
  (input-adornment icons excepted so dense fields don't grow) and every `.mud-menu-item` ≥48 px — so
  every screen, and every new one, inherits a phone-sized target instead of re-solving it. **Browser-verified**
  (Playwright, 390 px): the Groups kebab overflow button measures **48×48**. The transient
  `Size.Small` buttons in the confirm/edit clusters are left for **MNF-4**, which replaces those inline
  clusters with action sheets.
- **MNF-2 · Navigation clean-up (P1).** ✅ **Built 2026-09-02.** `SecretSantaShell` now shows the bottom
  bar **only when it carries more than the single "Groups" tab** (`_showBottomBar`), and the app-bar
  hamburger is hidden on narrow **only when a bottom bar owns the drawer** — so a signed-out / not-linked
  phone gets one menu entry (the hamburger + drawer) and no degenerate one-item bar, while a linked
  member gets the real tab bar and no redundant hamburger. Content bottom-padding (`ct-has-bottomnav`) is
  now conditional too, so there's no phantom strip without a bar. **Browser-verified** (Playwright,
  390 px, signed out): hamburger present, **no bottom bar**, hamburger opens the drawer, console clean.
  The **signed-in linked-member** path (bottom bar shown, hamburger hidden on narrow) needs the live
  Aspire stack + a linked member — deferred to the [TP-008](../features/TP-008-mobile-native-feel-polish.md)
  live-stack / on-device pass.
- **MNF-3a · Motion — haptics (P1).** ✅ **Built 2026-09-02.** New `IHapticFeedback` seam in
  `Ui.Abstractions` with a MAUI impl over `Microsoft.Maui.Devices.HapticFeedback` (`Click`) and a
  **web no-op** (`BrowserHapticFeedback`) — mirrors `IShareService`
  ([ADR-0028](../adr/0028-browser-platform-adapters-block.md)). A light tap fires on **draw success**
  (`Draw`), **giftee reveal** (`Reveal` + `MyAssignments`), and **pin/delete** (`Groups`). Android
  `VIBRATE` permission added to the manifest. Covered by a bUnit test (reveal taps once; the web impl is
  a no-op) — Ui suite **29/29**. **On-device: confirmed on the Pixel 9 Pro** — the buzz fires on **Auslosen (draw)**
  and **delete confirm** (the two offline-reachable points; pin and the signed-in *my-assignments*
  reveal are gated behind sign-in, and the public reveal-link buzz needs an opened `/r` link).
  **Root cause found on device:** the platform haptic silently no-ops unless `HapticFeedback.Perform`
  is marshalled onto the UI thread — a Blazor event handler is not guaranteed to be on the main looper.
  `MauiHapticFeedback` now wraps it in `MainThread.BeginInvokeOnMainThread`; that is what made the buzz
  fire. (A clean build with the temporary diagnostic logging removed is in the working tree; it is
  behaviourally identical to the confirmed build and ships with the next Android deploy.)
- **MNF-3b · Motion — pull-to-refresh (P1).** ✅ **Built & confirmed on the Pixel 2026-09-02.** A shared
  `PullToRefresh` component + `js/pullToRefresh.js` in `Ui.Shared` (works on both
  heads — both render in a web view): a top-of-page pull past ~72px shows a spinner and fires the existing
  `AutoSyncService.RequestSync()` (a no-op signed out; pulled data arrives via the ordinary
  `SyncCompleted` re-render). The gesture only engages at scroll-top while dragging down, so ordinary
  scrolling is never blocked; `overscroll-behavior-y: contain` suppresses the browser's own reload-on-pull.
  **Browser-verified** (Playwright, 390px touch): the indicator reaches full opacity while pulling,
  **triggers past the threshold**, **ignores a below-threshold pull**, console clean. **On-device (Pixel):**
  the maintainer confirmed the gesture feels good and **does not interfere with normal scrolling** — the
  on-device tuning [FEAT-008](../features/FEAT-008-mobile-native-feel-polish.md) flagged is satisfied; no
  threshold/resistance changes needed.
- **MNF-4 · Native interaction patterns (P2).** ✅ **Built 2026-09-02, browser-verified; device pass
  pending.** Two shared bottom-sheet dialogs in `SecretSanta.Ui/Shared` shown via `IDialogService` at
  `DialogPosition.BottomCenter`:
  - **`ConfirmSheet`** replaces the inline `MudAlert` confirmations for the three destructive actions —
    **delete group** (`Groups`), **remove member** (`Members`), **re-draw** (`Draw`) — with a bottom
    sheet carrying a red confirm, instead of a warning box that reflowed the card.
  - **`TextInputSheet` + a FAB** on the **Members** screen: on narrow the inline add-member card is
    hidden and a **`MudFab`** (bottom-right, cleared above the bottom nav / home indicator) opens the
    add-member sheet; the inline card stays on wide.

  **Browser-verified** (Playwright, 390px): the delete sheet and add sheet both open from the bottom, a
  member added via the sheet lands in the roster, the inline add-card is hidden on narrow, console clean.
  **Scope decisions (deliberate):** (1) the **Groups create field is left as-is** — it is the UX-003
  first-run funnel and the onboarding copy in DE/EN/FR literally points "above" at it, so converting it
  to a FAB is its own small UX/copy decision, not this polish pass; a Groups FAB is a candidate follow-up.
  (2) The **kebab overflow menus stay** (they already have 48px targets from W1) — only the destructive
  *confirmations* became sheets, keeping the change focused.
  - **On-device (Pixel) pass — mostly ✅, one bug found & fixed.** Delete/remove/re-draw sheets and the
    add-member sheet all work under a real finger. **Bug (device-only):** the FAB overlapped the last
    member row's kebab, and with too few members to scroll, that member's overflow menu was unreachable.
    **Fixed:** a `.ct-fab-spacer` reserves scrollable room below the list so the FAB always floats over
    empty space and any row can be scrolled clear; the FAB `z-index` was also lowered below MudBlazor's
    popover layer so an open kebab menu is never covered. Browser-verified (Playwright, 6+ members): the
    last row's kebab no longer overlaps the FAB and is clickable. **Confirmed fixed on the Pixel** — the
    bottom member's overflow menu is reachable for both short and long lists.

**Selected earlier, distribution cluster (from the [mobile brainstorm](../brainstorming/2026-09-01-secret-santa-mobile.md)) — build next:**

- **MNF-5 · QR code per reveal link (P2).** ✅ **Built 2026-09-02, browser-verified (DE/EN/FR); device
  pass pending.** Each reveal link on the Share page gained a **QR CODE** button beside Copy/Share that
  opens a centered dialog (`SecretSanta.Ui/Shared/QrDialog.razor` via `IDialogService`) titled with the
  recipient's name, showing a large scannable QR of *their* link plus a one-line hint — so the organizer
  holds up their phone and the recipient scans it, no messaging app needed. The code is drawn by a new
  reusable `QrImage.razor` component over **QRCoder 1.8.0** `PngByteQRCode`: pure managed, no
  System.Drawing and no JS interop, so it renders the same PNG data-URI **offline** in the WASM web head
  and the MAUI Blazor Hybrid webview with **no platform seam** (unlike `IShareService`). ECC level M keeps
  a 2 KB link (the `MaxRecommendedUrlLength` budget) within capacity while short links stay easy to scan;
  the PNG renders `image-rendering: pixelated` on a white ground so it is crisp and scannable in dark
  theme too. **Decision, no ADR:** QRCoder is a small, single-purpose, reversible dependency scoped to
  `SecretSanta.Ui` (kept out of the Main bundle); recorded here and in `Directory.Packages.props` rather
  than as an architecture decision — promote to `Ui.Shared` if a second app needs QR. Three new strings
  (`ShowQr`, `QrHint`, `Close`) in all four resx (de-CH "Schliessen"; FR "vous"); parity green; Ui suite
  **33/33** (added `QrImageTests` + a Share-page QR-button test). **Browser-verified** (Playwright,
  1280px + 390px, DE/EN/FR): create → event → 3 members → draw → Share shows three QR CODE buttons →
  the dialog renders a valid `data:image/png` QR titled per recipient with the localized hint and Close,
  console clean (only the pre-existing `<link rel=preload>` boot warnings). **Still to confirm on the
  Pixel:** the QR under a real camera scan, and the three-button (Copy/QR/Share) row on a phone where the
  Web Share API is present (the headless desktop context hides Share, so only the two-button row was
  captured on narrow). Deferred: a QR-decode assertion (the link → `QrImage.Value` wiring is unit-tested
  and the code renders correctly on screen).
- **MNF-6 · Add-to-calendar for an event (P2).** One tap from the event's date/location/description into
  the phone calendar.
  **✅ Promoted through the process (2026-09-15):** [UC-SS-021](../use-cases/secret-santa/UC-SS-021-add-occasion-to-calendar.md)
  → [FR-044](../requirements/functional/FR-044-add-event-to-device-calendar.md) →
  [FEAT-014](../features/FEAT-014-add-event-to-calendar.md) / [TP-014](../features/TP-014-add-event-to-calendar.md),
  with the calendar platform seam recorded as an
  [ADR-0028 amendment](../adr/0028-browser-platform-adapters-block.md) (no new ADR). **Design calls
  (locked, recorded in the FEAT):** a shared client-side **`.ics` VEVENT** backbone (works on the
  account-free reveal page) delivered per head — **web** opens the `.ics`, **Android** uses
  `Intent.ACTION_INSERT` (reliable; Android has no consistent `text/calendar` handler), **iOS** opens the
  `.ics` (deferred with the iOS head, [ADR-0031](../adr/0031-ios-build-host.md)); an **all-day** entry
  (the date is date-only), **gated on a date being set**; **system-UI only — no silent write, no calendar
  permission** (so no manifest change, unlike haptics' `VIBRATE`); the entry carries name + location +
  notes + **budget range** but **never an assignment** ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md));
  offered to the **organizer** (event Details + hub) **and every recipient** (reveal / my-assignments).
  The entry also carries a **deep link back into the app at the occasion** (in v1, all three storage modes):
  the app's own event route built from the reveal base URL, in the `.ics` `URL` property **and** the
  description, reusing the existing App Link / `DeepLinkService` machinery
  ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)) — carries no secret (opaque GUIDs), and
  behaves exactly like reveal links today re: build-time origin and the Android chooser-until-real-
  `assetlinks.json`-fingerprint. **✅ Built & browser-verified 2026-09-15** ([FEAT-014](../features/FEAT-014-add-event-to-calendar.md)
  *Done*): the `ICalendarService`/`IcsBuilder` seam + per-head impls + the shared button on hub/reveal/
  my-assignments + the `/groups` App Link intent-filter. Solution builds clean (incl. the MAUI Android head
  0/0); Ui 72/72 + Architecture 28/28. Browser walk confirmed the button appears on the hub and reveal page
  once a date is set (absent without one), non-spoiling, console clean. **✅ On-device confirmed on the Pixel 9
  Pro 2026-09-15:** the button on a dated event hub → `ACTION_INSERT` → Google Calendar opened a **pre-filled
  all-day event** (correct title + date, no off-by-one). Residual: App Link **auto-open** still needs the real
  Play-signing `assetlinks.json` fingerprint (shared pre-go-live item with reveal links).
- **MNF-7 · Share all links for an event at once (P2).** Follow-on to the existing share seam.
  **✅ Promoted through the process (2026-09-15):** [UC-SS-022](../use-cases/secret-santa/UC-SS-022-hand-out-reveal-links.md)
  → [FR-045](../requirements/functional/FR-045-guided-hand-out-of-reveal-links.md) →
  [FEAT-015](../features/FEAT-015-guided-hand-out-of-reveal-links.md) /
  [TP-015](../features/TP-015-guided-hand-out-of-reveal-links.md), reusing the `IShareService` seam
  ([ADR-0028](../adr/0028-browser-platform-adapters-block.md), **no new ADR**). **Design calls (locked):**
  "share all" is a **guided hand-out** — the app walks the whole draw, firing the share sheet (or Copy on
  a desktop browser) for **one person's** link at a time and tracking who is done (**"X of Y done ·
  Z left"**), so nobody is forgotten. It **never** composes a payload with more than one reveal link — the
  naive "all links in one message" is a spoiler leak and is forbidden
  ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)). Handed-out progress is
  **device-local, per event, not synced** (via `IKeyValueStore`, like the recent-groups/culture prefs),
  survives an app reopen, and **resets on a re-draw** (the links change). Per-person Share/Copy/QR stay for
  one-offs; value is concentrated in the account-free / shared-by-link modes (linked members see their own
  assignment in-app). **✅ Built & browser-verified 2026-09-15** ([FEAT-015](../features/FEAT-015-guided-hand-out-of-reveal-links.md)
  *Done*): a **Hand out links** button + guided `HandoutDialog` + `HandoutProgressService` (device-local,
  keyed by a reveal-link hash so a re-draw resets). Builds clean; Ui 72/72. Browser walk confirmed
  "Handed out: 0 / 3" → dialog (one-at-a-time Share/Copy/Mark) → "1 / 3" + chip **persist across a full
  reload**, console clean. **Pending the Pixel:** the guided hand-out under a finger — its OS share sheet reuses
  the already-device-proven `IShareService` (reveal-link share confirmed on the Pixel earlier), so only the
  hand-out flow itself is unverified on device (not re-driven this pass; needs a draw set up on the phone).

**Parked — need their own design/ADR before building:**

- **MNF-8 · Local notifications (P1 value).** Draw-reminder before the event, "event is
  tomorrow", empty-wishlist nudge — the strongest reason to *install* rather than bookmark. Needs a
  scheduling + permission design (and, for "remind everyone", a reachable origin).
  **Motivation reinforced (2026-09-02):** the [competitor scan](../brainstorming/2026-09-02-secret-santa-competitor-research.md)
  found reviewers specifically asking to be **nudged to fill their wish list** — the empty-wishlist reminder is a
  named, cross-market wish, not just an install hook.
  **✅ Promoted through the process (2026-09-15):** [UC-SS-023](../use-cases/secret-santa/UC-SS-023-be-reminded-about-my-occasions.md)
  → [FR-046](../requirements/functional/FR-046-device-local-occasion-reminders.md) →
  [FEAT-016](../features/FEAT-016-device-local-occasion-reminders.md) /
  [TP-016](../features/TP-016-device-local-occasion-reminders.md), governed by a new ADR
  [ADR-0060](../adr/0060-local-notification-scheduling-and-permission.md). **Scope sharpened against the
  origin gate:** v1 ships the **device-local** subset — the three reminders scheduled and fired on the
  user's own device (offline, account-free), a new `INotificationScheduler` platform seam
  ([ADR-0028](../adr/0028-browser-platform-adapters-block.md)) over Plugin.LocalNotification on the device
  head with a **web no-op** (phone-first, like haptics), a **global per-kind opt-in** in Settings (off by
  default), OS permission requested **on first enable**, and **no assignment ever in a reminder body**
  ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)). **Cross-member push**
  ("nudge another member" / "remind everyone") **stays parked** on the reveal-origin/hosting decision
  ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)) — a future ADR once an origin exists.
  **✅ Built 2026-09-15; on-device delivery pending the Pixel** ([FEAT-016](../features/FEAT-016-device-local-occasion-reminders.md)
  *Done*): the `INotificationScheduler` seam, the pure `ReminderPlanner`, `NotificationPreferences`, the
  `ReminderScheduler` coordinator, the web no-op (`BrowserNotificationScheduler`) and the MAUI impl over
  **Plugin.LocalNotification 12.0.0**, the Settings **Reminders** section, and `POST_NOTIFICATIONS` +
  `RECEIVE_BOOT_COMPLETED`. Solution builds clean incl. the MAUI Android head (0/0, the plugin restores for
  net10-android); Ui 72/72 + Architecture 28/28. Browser-verified the web-visible part (Settings shows the
  "needs the app" note, no switches on web). **✅ On-device confirmed on the Pixel 9 Pro 2026-09-15:** app
  launches (no crash), the plugin's `ScheduledAlarmReceiver` registers, the three switches render, and enabling
  one fired the Android **`POST_NOTIFICATIONS` prompt** → granting left it on (`granted=true`). Residual (needs
  elapsed time): each kind actually firing at its time, and cancel-on-draw/wishlist-filled/delete.
- **MNF-9 · Biometric app lock (P2, parked).** Face/fingerprint on open — assignments are the one secret
  the app keeps, and a phone left unlocked exposes who drew whom. Opt-in; warrants an
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md) privacy review first.
- **MNF-10 · iOS App Links + edge-swipe-back (P2, gated).** iOS deep links are **not wired at all** today
  (Android-only, [ADR-0053](../adr/0053-backend-free-first-mobile-release.md)); iOS also has no
  edge-swipe-back. Bring the iOS head to the Android head's deep-link parity when iOS becomes a target
  ([ADR-0031](../adr/0031-ios-build-host.md)).

**Explicitly declined** ([ADR-0054](../adr/0054-mobile-native-feel-strategy.md), do not reopen without a
new ADR): iOS Cupertino chrome / a platform-adaptive UI kit; Material You dynamic colour (conflicts with
the neutral chrome, [ADR-0040](../adr/0040-ui-component-library-mudblazor.md)); platform-divergent layouts.

## UX round 2 — Secret-Santa usability (2026-09-01)

A second UX pass on the Secret-Santa app, driven by the organizer's own use. Each item was built and
**verified in a real browser** (Playwright/Chromium, DE + EN, account-free flow), console clean.

- **R2-1/R2-2 · Member history: delete keeps past events; rename propagates (P1).** ✅ **Done —
  [ADR-0052](../adr/0052-member-archival-and-permanent-draw-history.md).** Removing a member used to
  delete them from the roster *and* clear every event's draw (and adding a member cleared past draws
  too), so a completed occasion lost both the person and the result. Now removal **archives** the member
  (kept in the aggregate for name resolution and rename propagation, off the active roster and out of
  future draws), and a roster change **flags** each event's draw stale instead of clearing it — draws
  are permanent history. A rename reaches past occasions because names resolve by id through
  `FindMember`. Reveal links for a kept draw still generate (the archived name resolves), with a "roster
  changed" warning on the draw/share screens; the remove confirmation says the person stays in occasions
  already drawn. Additive `MemberState.IsArchived`, opaque to sync (no server change). **Browser-verified
  end to end:** draw 4 people → remove one → the Share page still lists all 4 links (incl. the removed
  person) under the stale warning → rename another → the kept draw's link shows the new name.
- **R2-3 · A new group starts with no event (P2).** ✅ **Done — [ADR-0042 amendment](../adr/0042-events-as-first-class-entities.md).**
  A new group no longer auto-creates an unnamed event; the group editor opens on an empty occasions list
  with a hint, and the user creates the first occasion. `Group.RemoveEvent` may now go to zero;
  create-from-circle is eventless too. A pre-events snapshot (null `Events`) still migrates; an empty
  list is a real state.
- **R2-4 · Default event name "Christmas {year}" (P2).** ✅ **Done.** The add-event field is pre-filled
  with a localized default — *Weihnachten 2026* / *Christmas 2026* / *Noël 2026* (`DefaultEventNameFormat`
  + the current year) — which the user can accept or edit.
- **R2-5 · Nav quick-access submenu (groups → recent events) (P2).** ✅ **Done.** The drawer now shows,
  under *Groups*, the recently-opened (and still-visible) groups — each an expandable `MudNavGroup`
  listing its most recent occasions (dated newest-first, then undated by last-opened) plus a **Mehr…**
  link into the group — and, when more groups exist than are shown, an **Alle Gruppen** link to the full
  list. Recency is a device-local `RecentAccessService` over the plain `IKeyValueStore` (like the culture
  choice, not synced); `GroupNav`/`EventNav` record opens, and the shell refreshes the section on
  navigation, sign-in change and sync. The current group's submenu auto-expands. Event ordering needed a
  new `EventSummaryDto.Date`. **Browser-verified** (DE): the group and its two occasions appear nested in
  the drawer, current event highlighted.
- **R2-1b · A past occasion's stale draw is not flagged "re-draw needed" (refinement of R2-1/2).** ✅
  **Done.** After removing a member, a *past* occasion (its date has gone by) was still showing
  "re-draw needed", which is noise — a finished exchange is history, not something to re-draw. The read
  model ([ADR-0052](../adr/0052-member-archival-and-permanent-draw-history.md)) now suppresses the stale
  flag once the occasion's date is in the past (undated/upcoming still show it); the query handlers take a
  `TimeProvider`. Applies everywhere the flag surfaces (badges, draw/share notices). Browser-verified: a
  2024 occasion reads "Ausgelost", a 2027 occasion "Neu auslosen nötig".
- **R2-6 · Broader mobile/UX polish (P3, open).** Ongoing: make the app easy to understand and quick to
  reach the common things, on web and especially on phones. Candidates to be gathered as they surface.
  - **Mobile touch/layout pass — ✅ done 2026-09-01** (alongside making the Android head installable on a
    physical phone). From an on-screen inventory of every Secret-Santa screen: primary row-action buttons
    no longer use `Size.Small` (~30px → default, ~44px touch target); crowded per-row button clusters fold
    into an overflow **kebab menu** keeping one primary action visible (`Members` — was up to 5 inline
    buttons; `Groups`; `GroupEditor`); non-wrapping rows gained `flex-wrap` (`Share` card header,
    `Exclusions` list row); the app bar drops the cross-app switcher on narrow viewports
    (`SecretSantaShell`); the budget fields get explicit widths + hidden spinners (`EventEditor`); the
    from-circle person checkboxes lose `Dense` for a bigger hit area. A new **`MoreActions`** string in all
    four resx. Build + Ui tests green. **Still to verify on the Pixel:** the Android top safe-area /
    status-bar behaviour (currently tinted via `colorPrimaryDark`, neutral per ADR-0040 by design) and the
    kebab/tap targets under a real finger.
  - **Installable on a physical Android phone — ✅ done 2026-09-01.** `RevealBaseUrl` in `MauiProgram.cs`
    now points at the dev machine's **LAN address** (`http://192.168.100.122:5081`) so a link shared from
    the phone opens on any device on the home Wi-Fi (run the WebApp with `--urls http://0.0.0.0:5081`).
    USB-deploy runbook added to [device testing](../deployment/device-testing.md). **Known limitation:**
    account sign-in/sync over the LAN needs a trusted cert / real origin — deferred with hosting; the
    offline + share flow is unaffected.
  - **Backend-free v1 reveal path — ✅ implemented 2026-09-01 ([ADR-0053](../adr/0053-backend-free-first-mobile-release.md)).**
    `RevealBaseUrl` (and `ApiBaseUrl`) are now **build-time settings** on the MAUI head (Release defaults to
    the production origins; `release-mobile.yml` passes them), so a store build no longer ships a dev URL.
    **Android App Links** for `secretsanta.circletools.ch/r` open the installed app and render the reveal
    **offline from the baked-in UI** (`MainActivity` intent-filter + `DeepLinkService` + `DeepLinkListener`,
    `LaunchMode=SingleTask`); a recipient without the app falls back to the static page. **Verified on the
    Pixel 9 Pro:** a reveal App Link opened the app and showed "Hallo Katy! … Du bist Wichtel für: Vreni",
    fully offline. `assetlinks.json` ships in the web app with a **placeholder** fingerprint —
    **still to do before go-live:** replace it with the real Play App Signing SHA-256 (see `deploy/README.md`)
    so links auto-open silently instead of via the chooser.
  - More mobile-feature candidates gathered in the
    [mobile brainstorm (2026-09-01)](../brainstorming/2026-09-01-secret-santa-mobile.md): QR codes,
    add-to-calendar, share-all, local notifications, biometric lock.

## UX & platform-parity review — 2026-08-12

A usability pass taken after M4 closed, to answer two questions directly: is the suite built on
**both** web and mobile, and is it **user-friendly**? The offline Secret-Santa journey was driven in a
**real browser** (headless Chromium via Playwright, DE/EN/FR: create group → budget/event → members +
wishlists → exclusion → draw → share → open a reveal link), per the `CLAUDE.md` "verify UI in a real
browser" rule. **The core offline flow is polished and correct** — all three languages render cleanly,
spoiler protection is thoughtful, the reveal page separates `Event: 12/24/2026, Zürich` correctly (the
old separator defect is gone), empty wishlists read "No wishlist provided", and the whole run produced
**zero console errors/warnings**. The findings below are the gaps that pass alongside that.

**Scope caveat:** only the **account-free** flow was browser-audited. The **signed-in v2 flows**
(sign-in, sync, claim, circles, linking) were **not** driven live — that needs the Main service +
Postgres + Mailpit up — so the signed-in items below are from a **code + docs review** and still want a
live two-account pass to confirm.

- **UX-001 · Mobile is a milestone behind — decide its scope (P1).** ✅ **Resolved 2026-08-13 —
  decision: bring the MAUI head to web parity.** The `CircleTools.SecretSanta.App` head now composes the
  same shared feature stack the browser WebApp does, with only the platform seams swapped for their
  device implementations (ADR-0025/0028): it references `CircleTools.ApiClient` and `CircleTools.Ui.Account`,
  and `MauiProgram.cs` registers the two keyed account `HttpClient`s (plain handler, no
  browser-credentials handler), the `M3` sync engine (`AddSecretSantaSyncEngine`), the `ISyncApi` /
  `ILinkingApi` / `ICircleDirectoryApi` ports over the authorized client, `AddAccountUi` under
  `secretsanta.refreshToken`, and `AddSecretSantaUi`. The device seams are `FileKeyValueStore` (already
  present) for storage and a new `MauiPasskeyCeremony`. The root `Routes` component now wraps
  `CascadingAuthenticationState` + `CultureBoundary` and, before the first page renders, runs the two
  startup steps the WebApp runs before `RunAsync` — `ICultureProvider.InitializeAsync` and
  `AccountSession.RestoreAsync` — which is where the culture/session init the old comment said "belongs
  with adding the picker" now lives. The `NavMenu` gained the account-aware section (sign-in / signed-in
  identity, Sync now, Claim, My links/assignments/wishlists, sign out) **and the language picker**
  (`CultureSelector`), so mobile is DE/EN/FR, not `de-CH`-only. **Verified:** both target frameworks
  build clean (`net10.0-windows`, `net10.0-android`, 0/0), and the Windows head was run natively — it
  boots with no composition/startup error and renders the parity nav (the **Anmelden** link and the
  **Deutsch** language picker) over the offline flow.
  - **What is still deferred, with reasons (all gated on the brand/domain, not on this work):**
    **cross-client passkeys** — the WebAuthn RP-ID is the registrable suite domain (FR-010/011), so
    `MauiPasskeyCeremony` reports "unsupported" and the account pages already show the right message;
    password sign-in works fully. **The production API origin** — `MauiProgram.ApiBaseUrl` is a dev
    default (`10.0.2.2:7080` for the Android emulator's host-loopback); the literal origin lands with
    the domain. The **`ApplicationId`** is now set to the final `ch.circletools.secretsanta` (2026-08-13,
    brand chosen); the **icon/splash** are still template art, to be replaced before first submission.
    There is still **no `Main` mobile head** — out of scope for the Secret-Santa app's parity.
  - Live sign-in/sync from a device against a running Main service was **not** driven this pass (it
    needs Main + Postgres + Mailpit reachable from the emulator/device); the account UI is the same
    shared `Ui.Account` components already browser-verified end-to-end on web across M1–M4.
- **UX-002 · Account-only actions are offered to account-free users (P1, browser-verified).** ✅
  **Fixed 2026-08-13.** On the members page **while signed out**, every member showed a **"Link…"**
  button; clicking it opened an email field + "Send" that calls the authorized server API — a dead end
  for someone with no account. `Members.razor` rendered the link/unlink controls with no `AuthorizeView`.
  The link/unlink cluster is now wrapped in `<AuthorizeView>` so it appears only when signed in; Edit
  and Remove (local, offline actions) stay for everyone. Verified in a real browser (signed out: no Link
  button, Edit/Remove present, console clean).
- **UX-003 · First-run / empty states are bare (P2, browser-verified).** ✅ **Fixed 2026-09-01.**
  Signed out, `/` was a title, an input, and the literal line "No groups yet." — no explanation of what
  the app does, no cue to sign in for cross-device sync. The empty Groups state is now an
  `AuthorizeView`-split onboarding block (`Groups.razor`): **signed out**, a gift-icon card with a
  headline (per app name — *Wichteln* / *Secret Santa* / *Père Noël secret*), a one-paragraph explainer
  of the whole flow that nudges to the create field above, a divider, and the **account sell at the
  point of choice** (sync across devices, see your own assignment, own your wishlist) with a **Sign in**
  button to `/account/login`; **signed in**, a shorter hint pointing at the create field and the
  from-circle shortcut. Five new strings (`EmptyState*`) added to all three cultures + neutral (the dead
  `NoGroups` key it replaces was removed); DE `du` / FR `vous` per ADR-0032; no counts, so no
  singular-form concern. Parity test green, project builds clean. **Verified in a real browser**
  (Playwright/Chromium, signed-out first-run in DE/EN/FR): the card renders correctly in all three
  languages, the create field stays the primary action above, and the change adds no console
  errors/warnings.
- **UX-004 · The share screen dumps raw encoded URLs (P2, browser-verified).** ✅ **Partly fixed
  2026-08-13.** Each person's reveal link was printed verbatim as a ~197-character base64 blob. The
  "Copy" button is the right primary action, but the raw string is noise and can alarm users. The raw
  URL is now collapsed behind a native `<details>` disclosure ("Show link" / "Link anzeigen" /
  "Afficher le lien"), so Copy is the clear primary action and the URL stays available for anyone who
  wants to inspect or paste it. Verified in a real browser (collapsed by default, expands on click,
  console clean).
  - **Native share sheet — ✅ done 2026-09-01.** Each link now has a **Share** button beside Copy that
    opens the OS share sheet (WhatsApp / Messages / e-mail), carrying the reveal link. It is a new
    platform seam, `IShareService` ([ADR-0028](../adr/0028-browser-platform-adapters-block.md) amendment):
    a browser impl over the Web Share API (`SecretSanta.WebApp/Interop/BrowserShareService.cs` +
    `wwwroot/js/share.js`) and a device impl over MAUI's `Share` (`App/Platform/MauiShareService.cs`).
    The button self-hides where the platform has no share sheet (most desktop browsers), so Copy stays
    the fallback. Covered by `SharePageTests` (button gates on support; click hands the link to the
    seam). WebApp + Android head build clean; Ui tests 28/28. **On-device (Pixel) confirmation pending**
    the USB-deploy pass.
  - **Still open (deferred):** a **QR code** per person (see the
    [mobile brainstorm](../brainstorming/2026-09-01-secret-santa-mobile.md)).
- **UX-005 · Signed-in navigation shows dead ends and a reverting language picker (P2).** ✅ **Fixed
  2026-09-01** (the **Claim** sub-finding was already closed earlier by ADR-0044, which retired the
  `/claim` screen for the silent `AutoSyncService` + `SignInReconciler` prompt). Two changes to
  `SecretSantaShell.razor`, both verified live:
  - **Contextual personal nav.** *My links / My assignments / My wishlists* were shown to **every**
    signed-in user, inert unless they were a linked member. They are now gated: **My links** appears
    only when a member-link request is actually waiting (`ILinkingApi.GetPendingAsync` > 0), and **My
    assignments / My wishlists** only for a linked member (`GetMyMembershipsAsync` > 0). The gate is
    **server-authoritative on purpose** — a member of *someone else's* group has no local group document
    to read `Member.UserId` from, so a local check would wrongly hide the screens from exactly the people
    they are for. The two gates recompute when the account changes and after each background sync (so
    they appear on their own once a create/self-link or an accepted link lands), and stay hidden when the
    server is unreachable, reappearing on the next sync. The mobile bottom bar gates its two personal
    tabs the same way.
  - **Language picker — warn (not unify).** The nav `CultureSelector` is device-local; for a signed-in
    user the account language wins and reverts the pick on the next reconcile
    ([ADR-0035](../adr/0035-profile-sync-seam.md)). Chosen fix: when signed in, a caption under the
    picker says so and links to the **Profile** page (the durable, synced path). Kept the shared
    `Ui.Shared/CultureSelector` device-local rather than wiring it to `IProfileWriter`, so the shared
    component's behavior is unchanged across apps; persisting-from-nav ("unify") is a recorded follow-up
    (would add an optional persistence callback to `CultureSelector`). Two new `NavLanguage*` strings in
    all three cultures + neutral.
  - **Verified against the live Aspire stack** (Playwright/Chromium, real register→verify→sign-in via
    the API + Mailpit): signed-in-but-not-linked hides all three personal links and shows the device
    note; after creating a group (FR-032 self-link) *My assignments* + *My wishlists* appear on their own
    via the post-create sync while *My links* stays hidden; the device note reads correctly in **DE / EN
    / FR** (the account language winning over the local pick was observed directly — a live demonstration
    of the very revert the note explains). Console clean; 8/8 assertions green.
  - **Follow-up (optional):** a bUnit regression test for the two nav gates (the shell needs MudBlazor
    providers + fakes for `ILinkingApi`/`AutoSyncService`/`ISender`, so it was left as a follow-up rather
    than built here); and the "unify" language-picker option if device-local-plus-warn proves too subtle
    in use.
- **UX-006 · Sync is manual (P2).** ✅ **Resolved by ADR-0044 (2026-08-17).** The manual "Sync now"
  button is gone; `AutoSyncService` runs a silent, debounced sync on four triggers — app start (restored
  session), reconnect, post-edit, and the sign-in reconciler — so device B converges without anyone
  pressing anything, and the shell shows only a passive last-outcome status line. The parked
  continuous-sync decision ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md)) was **not**
  reopened. A pure *pull-on-tab-focus* refresh for a long-idle open tab is the only sliver left, and it
  is marginal; parked with the continuous-sync decision.
- **UX-007 · Group editor has three separate Save buttons and one shared status line (P3).** ✅
  **Obsolete after ADR-0042 (events as first-class).** The editor was restructured to **Name + Events**;
  budget and date/location moved into the **per-event** editor, so the "three saves under one ambiguous
  status line" arrangement no longer exists. `GroupEditor.razor` now has one name save plus per-event
  add/rename/remove, each with its own inline control. Nothing to do.

These are review findings, not yet promoted; the P1/P2 items are the candidates to turn into use
cases/requirements (or ADRs, for the mobile-scope decision) if taken forward.

## Ideas

### IDEA-002 — Circle (shared people across the suite) · _status: built & closed (M2, 2026-07-30) — M4 consumes it_
- **Problem:** every app re-asks who your people are. A family or friend-group has no lasting
  existence in the suite — each Secret-Santa group is typed in from scratch, and nobody but the
  organizer can contribute.
- **Target users:** families and friend-groups who use more than one app, or the same app repeatedly.
- **Value:** one standing set of people, reusable across apps and years. A Secret-Santa group can be
  created **from** a circle instead of retyped; linked people see their own assignment on their own
  device and keep their own [wishlist](../glossary/wishlist.md).
- **Scope (built):** create a circle and label it; roles (owner/admin/member);
  invitations by email that never leak whether an address has an account; managed members without an
  account (children); a user in several circles.
- **Naming:** the canonical code name is **`Circle`**; the UI label is chosen per circle and defaults
  to "Family". `Group` remains the Secret-Santa term — see [Circle](../glossary/circle.md).
- **Traceability:** [brainstorming v2](../brainstorming/2026-07-24-accounts-families-sync.md)
  (decisions D2, D5, D6, D7) → glossary [Circle](../glossary/circle.md),
  [Circle membership](../glossary/circle-membership.md), [Invitation](../glossary/invitation.md),
  [Managed member](../glossary/managed-member.md).
- **Architecture decided (2026-07-27):** [ADR-0018](../adr/0018-circle-and-sharing-model.md) — the
  `Circle` aggregate, roles, enumeration-resistant invitations, managed members, and the
  snapshot-at-draw rule; [ADR-0019](../adr/0019-member-user-linking.md) for member linking.
- **Use cases & requirements written (2026-07-29):**
  [UC-MAIN-008..014](../use-cases/index.md) — create a circle · invite a person · accept/decline ·
  managed members · roles, removal & ownership transfer · leave · delete — and
  [FR-016..022](../requirements/index.md) plus
  [NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md) (circle data
  authorization). Four points ADR-0018 left to this milestone are now decided in the specs, with the
  reasoning attached: **transfer of ownership is immediate, not an offer** (a pending transfer is the
  ownerless limbo the model forbids); **role changes are owner-only** and an admin may not remove
  another admin (or one admin can manufacture a majority); **membership is granted only to a verified
  owner of the invited address**, which closes the forwarded-invitation hole without a second secret;
  and **declining must be possible without an account**, because otherwise "declined" in the inviter's
  list would itself prove the address has one. Two M1 loose ends were closed on the way —
  [FR-015](../requirements/functional/FR-015-delete-account.md)'s sole-owner guard now has
  [FR-022](../requirements/functional/FR-022-delete-circle.md) behind it, and managed members are
  confirmed to be circle data that survives their creator.
- **Architecture follow-up closed (2026-07-29):** [arc42 ch. 5](../arc42/05-building-block-view.md)
  now describes the **Circles** context — `Circles.Contracts` / `Circles.Core` / `Circles.Repository`,
  hosted by the Main API host, with its own Postgres schema and no foreign key to Identity. It sits in
  `BuildingBlocks/` for the same reason Identity does (every app reuses it) but gets the full
  Core/Repository split, because unlike Identity it has invariants that must hold transactionally.
  [ch. 8 §8.10](../arc42/08-crosscutting-concepts.md) gained the authorization and invitation
  mechanics. This closes the last follow-up ADR-0018 recorded; **no new ADR was needed** — the
  packaging follows M1's precedent rather than deciding anything new.
- **Feature spec & test plan written (2026-07-29):** [FEAT-003](../features/FEAT-003-m2-circles.md) and
  [TP-003](../features/TP-003-m2-circles.md). The spec fixes every number the
  requirements deferred to it — **14-day invitations** (a resend replaces the live link rather than
  adding one), rate limits keyed on the **inviter**, the **recipient address** (the only one that
  protects the third party) and the **circle**, a **7-day silent** cooldown after a decline, and caps
  of 100 people per circle / 20 owned circles — and answers the four product questions: no acceptance
  or removal notifications (M2 has no notification channel and this is not the place to invent one),
  managed members *do* appear in a member's export because it mirrors in-app visibility, and circle
  deletion asks the owner to **type the circle's name** rather than their password, because the threat
  here is the misclick, not a stolen session. Two design points are worth knowing before implementing:
  memberships, invitations and decline records live **inside the `Circle` aggregate** (every invariant
  is circle-scoped), and there is **exactly one accept path** for both invitation branches, so the
  "verified address must match" rule lives in one place.
- **Domain & application built (2026-07-29):** `CircleTools.Circles.Contracts` and
  `CircleTools.Circles.Core` exist — the `Circle` aggregate with memberships, invitations and decline
  records inside it, every invariant and cap, and 23 command/query handlers, with **68 unit tests**
  and four architecture fitness functions green (355 tests across 11 projects overall). Four ports
  are declared and unimplemented until the host slice: `ICircleRepository`, `IAccountDirectory`,
  `IInvitationMailer`, `IInvitationRateLimiter`. Two spec corrections were made and recorded in
  [FEAT-003](../features/FEAT-003-m2-circles.md): the "no email field anywhere in Contracts" rule
  became "no DTO describing a **person** carries one", with a two-name exemption list under test
  (an invite request must carry an address, and a pending-invitation list is unusable without one);
  and the invitation mail leaves through an `IInvitationMailer` **port** rather than `Circles.Core`
  referencing `CircleTools.Infrastructure`, which would have pulled MailKit into the domain project.
- **Built and closed (2026-07-30):** the rest of the slice landed — `CircleTools.Circles.Repository`
  (the `circles` schema and its migration, `xmin` concurrency marked on the circle row so the token
  covers child changes, and `IInvitationRateLimiter` over rolling-window `invitation_attempts` rows),
  `/api/v1/circles` + `/api/v1/invitations` behind `CircleRoleFilter`, `OutboxInvitationMailer`, both
  account-deletion seams (`SoleOwnerCircleGuard` for M1's `IAccountDeletionGuard`, and the new
  `IAccountDeletionCleanup`), the DE/EN/FR invitation mail, and four `Main.Ui` pages. **537 tests
  green across 12 projects.** Two further spec corrections are recorded in
  [FEAT-003](../features/FEAT-003-m2-circles.md): `IInvitationRateLimiter` belongs to
  `Circles.Repository`, not the host (its counters are `circles` tables), and the in-app invitation
  path needed routes of its own, because `CircleRoleFilter` answers `404` to exactly the people those
  routes exist for.
- **What closing it found, and the lesson:** the four checks below were missing while everything
  around them looked finished, and a green suite said nothing about any of them.
  - the NFR-006 endpoint-guard fitness function — `CircleRoleRequirement` had been built as endpoint
    *metadata* specifically so a test could read it back, with a comment saying so, and no test did;
  - the "role matrix" was three spot checks, not 14 routes × 5 caller kinds;
  - revoking an invitation had no functional test, only its route filter;
  - **two acceptance criteria were unmet in the product**: FR-021's leave confirmation never said the
    account and the user's own Secret-Santa data survive, and FR-022's delete confirmation never
    named the circle, counted who loses access, or named the managed members. Both were found by
    reading the criteria against the screen rather than against the tests.

  All four were fixed at close. The transferable lesson for **M3**: a test-plan row is not evidence,
  and a fitness function that is designed but never written leaves its design comment behind as false
  reassurance.
- **Carried, with reasons:** deliverability of the invitation (FR-017) waits on the
  transactional-email provider — invitations make that decision onboarding-critical, not merely
  account-critical; FR-019's and FR-022's Secret-Santa halves wait on **M4**, when something first
  copies a circle into a group. The two confirmation screens changed at close still want a look in a
  real browser.

### IDEA-001 — Secret-Santa app (first app) · _status: MVP defined_
- **Problem:** Organizing a Secret-Santa (Wichteln) by hand is fiddly — drawing names fairly,
  honoring "who can't draw whom" (e.g. partners), and doing it across a group on different phones.
- **Target users:** families and friend-groups, Swiss-first.
- **Value:** a delightful, private, offline-capable way to run a gift exchange with no account or backend.
- **MVP scope (defined):** create & configure a group (name, budget, event details) → add members
  with wishlists → define exclusions → run a fair draw (honors exclusions, no reciprocal pairs, no
  repeat of the previous draw via draw history, graceful failure) → generate one **self-contained
  encoded reveal link** per member (recipient + wishlist + budget + event) → each member opens their
  link and the page decodes it fully client-side. Fully offline & account-free; privacy (the link is
  the secret); multilingual DE/EN/FR (`de-CH` default). Out of scope: accounts, sync, server-stored
  assignments, notifications, multiple simultaneous organizers.
- **Traceability:** [brainstorming](../brainstorming/2026-07-24-secret-santa-scope.md) →
  [use cases UC-SS-001..006](../use-cases/index.md) → [requirements FR-001..006, NFR-001..003](../requirements/index.md).
- **Post-MVP (v2 — designed 2026-07-27):** add **central login** in the CircleTools **Main** shell,
  **App↔backend sync**, and the ability to **link a Secret-Santa `Member` to a
  [User](../glossary/user.md) account** — so members receive their assignment on their own device,
  keep their own wishlist, and identity is shared across apps. Scope and the eight pivotal decisions
  are settled in [brainstorming v2](../brainstorming/2026-07-24-accounts-families-sync.md);
  sliced as **M1 Identity · M2 Circle · M3 Sync · M4 Linking**.
- **Privacy change to be aware of (D1):** in v2, a signed-in member's assignment **is stored
  server-side** and protected by authorization rather than by absence. The account-free flow is
  unchanged and still stores nothing.
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) has been amended, and
  [ADR-0020](../adr/0020-server-side-assignment-privacy.md) partly supersedes
  [ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md).
- **Architecture decided (2026-07-27):** [ADR-0016](../adr/0016-sync-and-conflict-resolution.md)…
  [ADR-0021](../adr/0021-web-auth-token-model.md) cover sync, the account claim, the circle model,
  Member↔User linking, assignment privacy and the web token model.
- **M3 specified (2026-07-30):** the sync and claim slice has its use cases and requirements —
  [UC-SS-007](../use-cases/secret-santa/UC-SS-007-claim-local-groups.md) take my existing groups into
  my account · [UC-SS-008](../use-cases/secret-santa/UC-SS-008-remove-group-from-account.md) take one
  back out · [UC-MAIN-015](../use-cases/main/UC-MAIN-015-sync-across-devices.md) ·
  [UC-MAIN-016](../use-cases/main/UC-MAIN-016-work-offline-and-reconcile.md) → FR-023..026 and
  [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md). The
  privacy consequence is worth stating where the Secret-Santa idea lives: **a claimed group carries
  its draw**, so M3 — not M4 — is when assignments first reach the server, and the claim screen has to
  say so before the upload rather than in a settings page afterwards.
- **M3 unblocked & designed (2026-08-03):** the one open decision that blocked the build is closed —
  [ADR-0033](../adr/0033-which-aggregates-sync.md) narrows [ADR-0016](../adr/0016-sync-and-conflict-resolution.md)
  so the Secret-Santa `Group` and profile preferences sync but the `Circle` does not (server-authoritative,
  online-only), and it parks an offline circle *read* cache as a separate, later decision. The slice now
  has its feature spec and test plan: [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md) (*Draft*) fixes
  the numbers the requirements deferred — the `POST /api/v1/sync` delta shape, the per-account cursor,
  tombstone/un-claim-marker retention below the slowest device's cursor, claim idempotency via
  client-minted ids, and profile preferences as a per-account document — and
  [TP-004](../features/TP-004-m3-sync-and-claim.md) (*Draft*) centres on a two-client harness for
  NFR-008's two silent-failure properties (convergence, data safety) and on proving the LWW collision
  hook actually emits telemetry rather than being a stub. The same deployment ADRs
  gate *shipping* but not *building* ([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)).
- **M3 built & closed (2026-08-12):** the whole slice is built and
  [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md) is *Done* / [TP-004](../features/TP-004-m3-sync-and-claim.md)
  *Reviewed*; FR-023..026 and NFR-008 are `Implemented`, and **FR-012's carried offline-language
  criterion closed with them**. The verification `CLAUDE.md` insists on — a **two-device, DE/EN/FR
  browser walkthrough against a live Main service** — was completed and is green (Playwright over real
  Chrome, two isolated contexts as two devices, 37 assertions): convergence, data safety, LWW with a
  reported collision, delete-without-resurrection, un-claim-as-marker, the FR-012 cross-device language
  change, the claim-list singular count in all three languages, and the offline circle notice, all
  confirmed on screen with a clean console. **No product defects were found.** Two behaviour questions
  were recorded, not fixed: first sign-in surfaces the claim screen via a nav link rather than an
  automatic prompt; and the **signed-in nav language picker is local-only** and is reverted to the
  profile preference on the next reconcile (the Profile page is the synced path). What is carried, not
  closed: the same-as-M1 **CI pipeline** ("CI green" → milestone 7), and a handful of automated tests
  the reconciliation records (`ClaimFlowTests`/`ClaimIdempotencyTests`, `SyncAuthTests`, the perf gate,
  account-deletion cascade, bUnit page renders).
- **M4 built and closed (2026-08-12):** the linking slice is built, verified, and browser-walked —
  [FEAT-005](../features/FEAT-005-m4-linking.md) is *Done* / [TP-005](../features/TP-005-m4-linking.md)
  *Reviewed*, FR-027..030 *Implemented*. Backend: the `Member` domain change (`UserId?` +
  `OriginCirclePersonId?`, and a link that does **not** invalidate a completed draw), create-from-circle
  composition with the duplicate-account guard, the new **`linking` Postgres schema** (member-links,
  the ADR-0020 assignment projection, self-wishlists, resolutions) with its migration, and the whole
  authorized surface on the Main host — reusing the M2 invitation machinery for the member-link, and
  the deny-not-filter rule realised as **403 for a stranger vs 404 for a known member with no draw**
  (unlink *closes* the member-link row rather than deleting it, so a former member degrades gracefully).
  The account-deletion cascade is a second `IAccountDeletionCleanup`. Client: the four Blazor flows
  (create-from-circle picker, organizer link/unlink on the members page, the anti-spoiler my-assignment
  view, the my-wishlist editor with the reveal-link caveat), the resolution applier and assignment
  publisher wired into the `SyncCoordinator`, and 60 DE/EN/FR strings. **Verified by automated tests:**
  17 two-account integration tests on real Postgres, 28 architecture fitness (incl. Core ⊥ Circles),
  16 Ui component/unit, localization parity, and the M3 suite still green (221) with no regression.
  **One decision recorded:** the circle read for the picker exposes an account-backed member's opaque
  account id to fellow circle members (never an address) so the group can link them — the circle's own
  trust boundary (ADR-0018); NFR-005's address-account oracle is untouched.
  - **The browser walkthrough was run and is green** (2026-08-12): two accounts against a live Main
    service (Postgres + Mailpit + the WASM app), Playwright over real Chromium, console clean in all
    three languages — create-from-circle (the *"3 people"* count checked as "3 Personen" / "3 personnes",
    no `"1 Personen"` bug), linked badges on the members page, draw + sync, the linked member's own
    assignment behind the anti-spoiler step, and a wishlist edit that **persisted across reload**.
  - **It found a real defect a green suite had missed** — create-from-circle set `Member.UserId`
    locally but never registered the **server-side** member-link, so a person linked that way could see
    their assignment but **could not own their wishlist**. Fixed by `POST /api/v1/member-links/circle`
    (the host **verifies shared-circle membership** before recording the link) plus the client call from
    `CreateFromCircleService`; now covered by `LinkingCircleTests`. The lesson for the next milestone,
    written down: **every test set up its links through the accept flow**, so the one path that did not
    was the one that broke — and only the browser showed it. One recorded decision came with the fix:
    `CirclePersonResponse` exposes an **opaque account id** (never an address) to fellow circle members
    so a group made from the circle can link them (ADR-0018 trust boundary; NFR-005 untouched).
- **Next:** M4 is the last v2 milestone; the carried cross-cutting items remain (the milestone-7 **CI
  pipeline** for "CI green", and *shipping* still waits on the transactional-email and hosting decisions,
  now sharpened again by the member-link mail — none gate the build).

### IDEA-000 — CircleTools shell (Main app-type) · _status: in progress_
- **Problem:** apps need shared branding, one login, shared config, and a dashboard.
- **Role:** hosts **central login / accounts**, shared config, and the **sync backbone** that
  connects the Secret-Santa app (and future apps) to the backend — including linking in-app "persons"
  (e.g. Secret-Santa `Member`s) to [User](../glossary/user.md) accounts.
- **Decided (2026-07-27):** identity stack per
  [ADR-0009](../adr/0009-authentication-and-passkeys.md) (ASP.NET Core Identity + passkeys), with
  **bearer tokens in the WASM client** (D8 — no BFF, so
  [ADR-0005](../adr/0005-web-hosting-render-model.md) stays intact); local-first
  [Sync](../glossary/sync.md) with per-aggregate last-write-wins; anonymous→account claim by
  **per-group choice** on first login (D4).
- **Traceability:** [brainstorming v2](../brainstorming/2026-07-24-accounts-families-sync.md) →
  glossary [User](../glossary/user.md), [Sync](../glossary/sync.md) →
  ADRs [0016](../adr/0016-sync-and-conflict-resolution.md),
  [0017](../adr/0017-anonymous-to-account-claim.md), [0021](../adr/0021-web-auth-token-model.md).
- **Use cases written (2026-07-27):** [UC-MAIN-001..007](../use-cases/index.md) cover the **M1 —
  Identity** slice — create an account, sign in with email+password, register a passkey, sign in with
  a passkey, profile & preferred language, delete account & export data, reset a forgotten password.
- **Open questions resolved (2026-07-27):** unverified accounts may sign in but get nothing
  account-scoped (purged after 30 days); password policy is NIST-style (12+ chars, no composition
  rules, breach-list check); sessions are 15-min access / 30-day rotating refresh, persistent, with no
  "remember me" tick; passkey **RP-ID is the registrable suite domain** so one passkey spans all app
  subdomains; display names are not unique; avatar deferred to M2. Deletion & export became
  [ADR-0022](../adr/0022-account-deletion-and-data-export.md) — immediate hard delete, export mirrors
  in-app visibility.
- **Requirements written (2026-07-27):** [FR-007..FR-015](../requirements/index.md) plus
  [NFR-004](../requirements/non-functional/NFR-004-data-protection-and-retention.md) (data protection
  & retention) and [NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md)
  (account-enumeration resistance). Glossary gained [Passkey](../glossary/passkey.md).
- **Feature spec written (2026-07-27):** [FEAT-002](../features/FEAT-002-m1-identity.md). It creates
  two building blocks arc42 ch.5 already planned but that do not exist yet — `CircleTools.Identity`
  (suite-wide, per ADR-0009) and `CircleTools.Infrastructure` (outbox + SMTP sender). arc42 ch.5 §5.3 now
  fixes the four bounded contexts.
- **Test plan written (2026-07-27):** [TP-002](../features/TP-002-m1-identity.md), weighted toward the
  two disproportionate risks — refresh-token rotation (custom code, incl. the racing-refresh case) and
  enumeration resistance (whose failure mode is invisible to ordinary functional tests).
- **Server half built (2026-07-27):** `CircleTools.Identity` and `CircleTools.Infrastructure` exist, and
  the whole `/api/v1/account` surface with them — register, verify, login, refresh, logout,
  forgot/reset, profile, change-email, passkeys, export, delete — against real Postgres and real
  Mailpit.
- **Client half built (2026-07-28):** the Main Blazor client, plus the four UI building blocks arc42
  ch.5 planned — `Ui.Abstractions`, `Localization`, `ApiClient`, `Ui.Shared`
  ([ADR-0025](../adr/0025-shared-ui-building-blocks.md)). Three decisions that were being made
  silently in code became ADRs: styling ([0024](../adr/0024-ui-styling-and-theming.md)), the shared
  blocks ([0025](../adr/0025-shared-ui-building-blocks.md)), and where the refresh token is kept
  ([0026](../adr/0026-refresh-token-persistence.md), extending ADR-0021). FEAT-002 and TP-002 moved
  to *In progress*.
- **Secret-Santa migrated onto the shared blocks (2026-07-28):** ADR-0025's recorded follow-up is
  closed. Secret-Santa lost its own `AppCultures`, its own `CultureSelector` and its own
  `IKeyValueStore`; `secretsanta.culture` and `circletools.secretsanta.groups` are untouched, so no
  user loses a language or a group. Two amendments came out of it —
  [ADR-0028](../adr/0028-browser-platform-adapters-block.md) (a shared `Ui.Web` block for the browser
  adapters, now that there are two web hosts) and
  [ADR-0029](../adr/0029-storage-write-failure-contract.md) (`SetAsync` reports success, so a lost
  preference and a lost group stop being the same event). 281 tests green; full journey re-driven in
  a browser in DE/EN/FR.
- **M1 closed in the docs (2026-07-29):** [FEAT-002](../features/FEAT-002-m1-identity.md) is *Done*
  and [TP-002](../features/TP-002-m1-identity.md) *Reviewed*. The requirement and use-case registers
  were advanced with them — `Implemented` where every criterion is met, `Approved` where one is
  knowingly still open (FR-010/011/012/015, NFR-004), so the split marks where the slice stops
  instead of reading as neglect. The two exit criteria that could not be met are **carried to
  milestone 7**, not ticked; see the carry-overs below.
- **Carried to milestone 7 (from M1):**
  - **Automate the WebAuthn ceremony.** Every passkey test in the suite is negative or an
    authorization check; none creates a credential, because an in-process `WebApplicationFactory`
    has no authenticator. The ceremony works — it was driven by hand through a CDP virtual
    authenticator — but nothing re-checks it, and **NFR-004's "no private key, no biometric data"
    has no stored credential to inspect** until it is automated. Needs a browser-driving test host.
  - **A CI pipeline.** There is none, so the Definition of Done's "CI green" cannot be satisfied by
    any milestone until one exists. The CI/CD choice is still an open item in the
    [ADR register](../adr/index.md#open-decisions-backlog) and needs an ADR before the pipeline.
- **M2 written, built and closed (2026-07-29 → 2026-07-30):** the circle use cases and requirements,
  arc42 ch. 5's Circles context, [FEAT-003](../features/FEAT-003-m2-circles.md) (*Done*) and
  [TP-003](../features/TP-003-m2-circles.md) (*Reviewed*) — see `IDEA-002` above for what they decided,
  what was built, and what closing the milestone turned up.
- **M3 written (2026-07-30):** the sync and claim requirements —
  [FR-023](../requirements/functional/FR-023-sync-across-devices.md) sync across devices ·
  [FR-024](../requirements/functional/FR-024-offline-editing-and-reconciliation.md) offline editing
  and reconciliation · [FR-025](../requirements/functional/FR-025-claim-local-groups.md) claim ·
  [FR-026](../requirements/functional/FR-026-remove-group-from-account.md) un-claim ·
  [NFR-008](../requirements/non-functional/NFR-008-sync-convergence-and-data-safety.md) convergence &
  data safety — plus glossary entries for [Claim](../glossary/claim.md) (whose naming hazard is
  recorded: ASP.NET Identity already has a `Claim`) and [Tombstone](../glossary/tombstone.md) (which
  is **not** a soft delete). **The one decision that blocked the build is now closed** (2026-08-03):
  the requirements exclude circle data from sync, narrowing
  [ADR-0016](../adr/0016-sync-and-conflict-resolution.md) — last-write-wins on data four admins share
  would discard other people's edits, and an offline circle write cannot make the server-side
  authorization decision NFR-006 requires — and that narrowing is recorded as
  [ADR-0033](../adr/0033-which-aggregates-sync.md). The M3 feature spec and test plan
  ([FEAT-004](../features/FEAT-004-m3-sync-and-claim.md) *Done* / [TP-004](../features/TP-004-m3-sync-and-claim.md)
  *Reviewed*) were written, built and closed 2026-08-12 — see the `IDEA-001` M3 note above for the
  build and the browser walkthrough. FR-023 also carried **FR-012**'s offline language change, now
  closed.
- **Of M1's three deferred criteria, one still stands**, recorded as deferred in FEAT-002: MAUI
  cross-client passkeys (needs the real domain). The *offline* language change **closed with M3**
  (FR-012, via FR-023/024 — verified cross-device in the M3 browser walkthrough). The remaining one —
  **the sole-circle-owner deletion guard — closed with M2** as `SoleOwnerCircleGuard`,
  verified end to end through real account deletion. It blocks on **every** circle the account owns
  rather than only the ones nobody else could own: deleting the owner's own membership would otherwise
  leave a circle with no owner, a state the domain does not define.
- **Blocked on deployment ADRs — M1 cannot ship without them:**
  - **Transactional email provider.** FR-007 (verification), FR-009 (reset) and FR-013 (email change)
    all depend on delivery. The ADR register currently justifies deferring this behind M2 invitations;
    it is in fact an **M1** dependency.
  - **Hosting / suite domain.** Needed for the passkey RP-ID (FR-010, FR-011), the CSP directive set
    (ADR-0021), and the NFR-004 residency target. The *strategy* for each is decided — only the
    literal domain is missing. See [naming](../process/naming.md).
- **Store distribution scoped (2026-07-29):** what it actually takes to put the MAUI heads in the two
  stores, worked out and written down ahead of milestone 8, because two parts of it have long lead
  times and one is irreversible.
  - **Decided:** publisher identity — enrol as a private individual on **project-owned** accounts,
    never the personal ones ([ADR-0030](../adr/0030-mobile-store-publisher-identity.md)); and the
    **iOS build host** — an own Apple-silicon Mac ([ADR-0031](../adr/0031-ios-build-host.md)).
  - **The finding that reorders the work:** iOS cannot be built on Windows at all, and the Intel
    MacBook Pro is at best a one-release bridge — .NET 10 needs Xcode 26 → macOS 26, which only the
    16-inch 2019 and four-port 13-inch 2020 models run, and macOS 27 drops Intel entirely. Recorded
    as risk **R6**.
  - **The other long pole is Google's, not ours:** a new personal Play account must run closed
    testing with **12 opted-in testers for 14 continuous days** before it may even apply for
    production access. Android is ≈4–6 weeks of mostly waiting; iOS is ≈1–2 weeks once a Mac exists.
    So the Play account and the tester list should be started first, well before code is ready.
  - **Obligations stated** as [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)
    (annual SDK / target-API floors, DSA trader status, account-deletion URL, privacy declarations,
    DE/EN/FR listings) and the procedure as the
    [mobile-apps runbook](../deployment/mobile-apps.md); what can be tested on today's hardware is in
    [device testing](../deployment/device-testing.md).
  - **Still blocked on the brand:** submission, not account setup. The bundle id is permanent once
    released and RP-IDs do not migrate, which promotes [naming](../process/naming.md) from a cosmetic
    milestone to a hard gate (risks **R8**, debt **D7**).

## Later / parked
- Additional apps (to be brainstormed).
- **Invite by link or short code** — parked; revisit if email delivery proves to be the friction
  point (see [Invitation](../glossary/invitation.md)).
- **Per-member envelope encryption of assignments** — the P3 option from D1, parked as future
  hardening if the operator-as-trusted-party assumption stops being acceptable.
