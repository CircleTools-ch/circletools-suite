# Architecture Decision Records (ADRs)

Every significant architectural or technological decision is recorded here as an ADR
(MADR-style; see [ADR-0001](0001-record-architecture-decisions.md)). Copy
[`_template-adr.md`](_template-adr.md), number it sequentially (`NNNN-kebab-title.md`), and add
a row below. Implement only once an ADR is **Accepted**.

## Register

| # | Title | Status | Date |
|---|-------|--------|------|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted | 2026-07-24 |
| [0002](0002-use-arc42-c4-docfx.md) | Document with arc42 + C4 + DocFX | Accepted | 2026-07-24 |
| [0003](0003-modular-monolith-with-aspire.md) | Start as a modular monolith orchestrated by Aspire | Accepted | 2026-07-24 |
| [0004](0004-plantuml-prerender.md) | Pre-render PlantUML diagrams to SVG | Accepted | 2026-07-24 |
| [0005](0005-web-hosting-render-model.md) | Web hosting & render model (standalone WASM PWA) | Accepted | 2026-07-24 |
| [0006](0006-application-layer-and-mediation.md) | Application layer & mediation (hand-rolled dispatcher) | Accepted | 2026-07-24 |
| [0007](0007-persistence-approach.md) | Persistence approach (EF Core for Postgres + SQLite) | Accepted | 2026-07-24 |
| [0008](0008-entity-id-strategy.md) | Entity ID strategy (client-generated GUID v7) | Accepted | 2026-07-24 |
| [0009](0009-authentication-and-passkeys.md) | Authentication & passkeys (Identity + built-in passkeys) | Accepted | 2026-07-24 |
| [0010](0010-api-style-and-versioning.md) | API style & versioning (Minimal APIs) | Accepted | 2026-07-24 |
| [0011](0011-localization-format-and-culture.md) | Localization format & culture (RESX, `de-CH`) | Accepted | 2026-07-24 |
| [0012](0012-observability-and-telemetry.md) | Observability & client telemetry | Accepted | 2026-07-24 |
| [0013](0013-wasm-api-dev-wiring.md) | Standalone WASM ↔ API wiring (manual config + CORS) | Accepted | 2026-07-24 |
| [0014](0014-reveal-link-encoding-and-privacy.md) | Secret-Santa reveal-link encoding & privacy | Accepted | 2026-07-24 |
| [0015](0015-mvp-client-offline-storage.md) | MVP client offline storage (localStorage / file) | Accepted | 2026-07-24 |
| [0016](0016-sync-and-conflict-resolution.md) | Local↔server sync & conflict resolution (aggregate LWW) | Accepted | 2026-07-27 |
| [0017](0017-anonymous-to-account-claim.md) | Anonymous → account claim (choose per group) | Superseded by [0086](0086-one-way-to-be-in-a-group.md) | 2026-07-27 |
| [0018](0018-circle-and-sharing-model.md) | Circle & sharing model (canonical `Circle`, user-chosen label) | Accepted | 2026-07-27 |
| [0019](0019-member-user-linking.md) | Member↔User linking (optional `UserId`) | Superseded by [0086](0086-one-way-to-be-in-a-group.md) | 2026-07-27 |
| [0020](0020-server-side-assignment-privacy.md) | Server-side assignment storage & privacy (plaintext + authz) | Accepted | 2026-07-27 |
| [0021](0021-web-auth-token-model.md) | Web auth token model (bearer in WASM); amendment 2026-10-25 (Proposed): offline is not signed out — the session's account is remembered as display state, a waiting session is renewed on every sync trigger | Accepted | 2026-07-27 |
| [0022](0022-account-deletion-and-data-export.md) | Account deletion & data export (immediate delete, export mirrors visibility) | Accepted | 2026-07-27 |
| [0023](0023-local-dev-infrastructure-in-apphost.md) | Local dev infrastructure in the AppHost (containers behind provider-agnostic seams) | Accepted | 2026-07-27 |
| [0024](0024-ui-styling-and-theming.md) | UI styling & theming (Bootstrap 5 + thin suite theme) | Superseded by [0040](0040-ui-component-library-mudblazor.md) | 2026-07-28 |
| [0025](0025-shared-ui-building-blocks.md) | Shared UI building blocks (Ui.Abstractions, Localization, ApiClient, Ui.Shared) | Accepted | 2026-07-28 |
| [0026](0026-refresh-token-persistence.md) | Refresh-token persistence in the browser (session by default, durable on request) | Accepted | 2026-07-28 |
| [0027](0027-breached-password-check.md) | Breached-password check (bundled ≥12-character list) | Accepted | 2026-07-28 |
| [0028](0028-browser-platform-adapters-block.md) | Browser platform adapters as a shared block (`Ui.Web`) | Accepted | 2026-07-28 |
| [0029](0029-storage-write-failure-contract.md) | What an `IKeyValueStore` write does when storage refuses it | Accepted | 2026-07-28 |
| [0030](0030-mobile-store-publisher-identity.md) | Store publisher identity and account ownership (individual, project-owned accounts) | Accepted | 2026-07-29 |
| [0031](0031-ios-build-host.md) | iOS build host (own Apple-silicon Mac) | Superseded by [0065](0065-hosted-macos-ios-builds.md) | 2026-07-29 |
| [0032](0032-ui-tone-of-voice-and-gender-neutral-terms.md) | UI tone of voice & gender-neutral role terms | Accepted | 2026-07-30 |
| [0033](0033-which-aggregates-sync.md) | Which aggregates sync (narrows ADR-0016 — Group + profile; Circle excluded) | Accepted | 2026-08-03 |
| [0034](0034-shared-account-session-ui-block.md) | Shared account/session UI building block (`Ui.Account`) | Accepted | 2026-08-11 |
| [0035](0035-profile-sync-seam.md) | Profile-sync seam (host-supplied profile writer; offline edits flushed to `/profile`) | Accepted | 2026-08-12 |
| [0036](0036-cloud-provider-and-hosting.md) | Cloud provider & hosting topology (Infomaniak VPS + Docker Compose; self-managed Postgres; Swiss Backup) — **Cloudflare edge dropped by [ADR-0063](0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md)** | Accepted | 2026-08-13 |
| [0037](0037-transactional-email-provider.md) | Transactional email provider (Brevo free tier behind the outbox port) | Accepted | 2026-08-13 |
| [0038](0038-ci-cd-pipeline.md) | CI/CD pipeline (GitHub Actions; CI gate now, CD to the VPS deferred to M8) | Accepted | 2026-08-13 |
| [0039](0039-cross-app-single-sign-on.md) | Cross-app single sign-on (redirect-based authorization-code hand-off; Main origin as authority) | Accepted | 2026-08-13 |
| [0040](0040-ui-component-library-mudblazor.md) | UI component library: MudBlazor (Material), supersedes ADR-0024 (neutral in-app palette, theme switch, native-feel nav) | Accepted | 2026-08-13 |
| [0041](0041-sync-secret-santa-ui-preferences.md) | Sync Secret-Santa UI preferences as an opaque sync-store kind (extends ADR-0033 — adds pinned groups) | Accepted | 2026-08-14 |
| [0042](0042-events-as-first-class-entities.md) | Events as first-class entities within a Secret-Santa group (shared roster; per-event draw, wishlists, budget; linking store keyed by event) | Accepted | 2026-08-14 |
| [0043](0043-invitee-chosen-managed-member-promotion.md) | Let the invited person choose which offline profile they join as (self-select gated to the authenticated, address-matched recipient; admin targeting stays authoritative) | Accepted | 2026-08-14 |
| [0044](0044-sign-in-reconciliation-and-account-scoped-visibility.md) | Sign-in reconciliation of offline data (Import / Discard / Cancel) and account-scoped visibility; silent auto-sync on sign-in, start, change and reconnect. *Amended 2026-10-15:* the prompt is a sheet that must be answered (no answer ≠ keep local); sync also on resume and every 2 min in the foreground | Accepted | 2026-08-14 |
| [0045](0045-browser-driving-e2e-test-host.md) | Browser-driving E2E test host: Playwright .NET + a CDP virtual authenticator against the AppHost-launched stack, in a dedicated gating CI job (automates the WebAuthn ceremony, arc42 D4) | Accepted | 2026-08-18 |
| [0046](0046-suite-packaging-and-app-composition.md) | Suite packaging & app composition (federated suite: per-app installs/origins with permanent `ch.circletools.<app>` bundle ids and `<app>.circletools.ch` subdomains, apex RP-ID; Main is a hub, not a container; unified by shared account + SSO + Circles) | Accepted | 2026-08-19 |
| [0047](0047-versioning-and-release-strategy.md) | Versioning & release strategy (Nerdbank.GitVersioning from a committed `version.json`; hybrid triggers — merge to `main` deploys the server, a `v*` tag cuts a mobile release; one suite version now, splittable per app; Android `versionCode` computed by nbgv from the version) | Accepted | 2026-08-21 |
| [0048](0048-web-app-update-delivery.md) | Web app update delivery (keep the offline PWA; a shared MudBlazor "Reload" prompt switches to a newly deployed version on the user's terms; all responses revalidate — corrected by ADR-0049; native heads update via the store) | Accepted | 2026-08-21 |
| [0049](0049-disable-wasm-fingerprinting-for-csp.md) | Disable WASM asset fingerprinting for strict-CSP compatibility (the inline import map fingerprinting emits is blocked by the inline-free CSP and stops boot; `OverrideHtmlAssetPlaceholders=false` on both heads; corrects ADR-0048's immutable cache header) | Accepted | 2026-08-21 |
| [0050](0050-secrets-management.md) | Secrets management (two-tier: GitHub Actions encrypted secrets for CI/deploy + git-ignored `deploy/.env` on the host for runtime; authoritative copy in a password manager for recovery/rotation; no plaintext in the repo; SOPS+age recorded as the trigger-gated upgrade path) | Accepted | 2026-08-25 |
| [0051](0051-production-observability-backend.md) | Production observability backend (self-hosted **Loki + Prometheus + Grafana** on the VPS: Prometheus scrapes the API's internal `/metrics`, the API ships OTLP logs to Loki, Grafana is the persistent admin view at `grafana.circletools.ch` behind its own login; Prometheus/Loki never published; telemetry stays on-box; logs+metrics only — Tempo/alerting/SaaS recorded as upgrades; closes ADR-0012's backend follow-up) | Accepted | 2026-08-25 |
| [0052](0052-member-archival-and-permanent-draw-history.md) | Member archival & permanent draw history (removing a member **archives** it — kept for name resolution and rename propagation, off the active roster; a roster change **flags** each event's draw stale instead of clearing it, and reveal links are withheld while stale; additive `MemberState.IsArchived`, opaque to sync; amends ADR-0042) | Accepted | 2026-09-01 |
| [0053](0053-backend-free-first-mobile-release.md) | Backend-free first mobile release (v1 to Play works with **no API**; reveal links delivered by the live static site at `secretsanta.circletools.ch` **plus Android App Links** so an installed app opens them offline from its baked UI; `RevealBaseUrl` becomes a build-time setting baked into store builds; accounts stay optional, API only on login; App-Link auto-verify waits on Play signing) | Accepted | 2026-09-01 |
| [0054](0054-mobile-native-feel-strategy.md) | Mobile native-feel strategy (**extends ADR-0040**: keep one neutral Material design across web + both device heads; earn "native feel" through **motion** (haptics, pull-to-refresh, transitions), **touch ergonomics**, and **OS integration** (share, notifications, calendar, biometrics, deep links) — **not** iOS Cupertino chrome, Material You, or platform-divergent layouts; near-term levers 1–4 specified as FEAT-008) | Accepted | 2026-09-02 |
| [0055](0055-event-navigation-mobile-section-hub.md) | Event-level navigation: a **section hub** (vertical list of Details/Wishlists/Draw/Share with per-section status + chevron) becomes the event landing on **all widths**, retiring the clumsy horizontally-scrolling router-tab strip; Details gains its own route, section pages get a back-to-overview link; one shared component, one screen per route; amends ADR-0042 (presentation only, domain unchanged) | Accepted | 2026-09-02 |
| [0056](0056-decouple-ui-language-from-formatting-region.md) | Decouple UI language from formatting region (**refines ADR-0011**: UI strings follow the chosen language via `CurrentUICulture`; date/number/currency formatting follows the **device/OS region** via `CurrentCulture`, captured at startup and independent of the language — so a Swiss user reading English sees Swiss dates; on the web the region is the browser locale) | Accepted | 2026-09-02 |
| [0057](0057-app-store-naming-and-localization.md) | App store naming & listing localization (**one universal, stable visible name per app** — "Secret Santa", English, identical in all three store locales and on-device; per-market discoverability lives in the localized **listing title / keywords** (`Secret Santa – Wichteln`, `Père Noël secret`), **not** in the name; store names are not derived from domains, so **no per-language or per-name domains** — the single `<app>.circletools.ch` origin serves every locale; standing rule for every suite app; complements ADR-0046) | Accepted | 2026-09-02 |
| [0058](0058-account-free-link-shared-groups.md) | Account-free, backend-stored, link-shared groups (a **third storage tier** between local-only and account-synced: the group lives in Postgres keyed by an **unguessable share token**, no account; an **admin link** (shared capability, account-free only, disable/regenerate, retired when an account claims admin) + a **join link** (participant; claim existing/new person × account or device-only); **trust-based + first-claim-locks** assignment privacy with **admin release** as the sole recovery; **release-admin** (My account → Shared by link) so ownership isn't a trap; mode chosen at group creation, default local; per-person crypto = future hardening; **amends NFR-002, glossary/sync, arc42 §8**, narrows ADR-0053, **extends ADR-0022 deletion flow** (hand off as shared by link / keep on device / delete)) | Accepted | 2026-09-13 |
| [0059](0059-brand-mark-ring-and-central-glyph.md) | Brand marks — a **ring enclosing a central glyph** (replaces the *ring of five glyphs*, which was illegible at favicon size): a real circular border with one bold white glyph inside — CircleTools = overlapping **trio of people** in a **deep navy-blue `#0a2c6b`** ring on navy; Secret-Santa = the **white wrapped present** in a **maroon `#4a0f18`** ring on burgundy; both rings **recessed tone-on-tone**; palette unchanged; per-app rule = swap glyph + ring colour; all SVG/PNG/MAUI assets regenerated; **updates naming.md Visual identity**, extends the ADR-0024 brand-mark amendment | Accepted | 2026-09-15 |
| [0060](0060-local-notification-scheduling-and-permission.md) | Device-local notification scheduling, permission & platform seam (MNF-8: opt-in **device-local reminders** — draw-not-done / event-tomorrow / empty-own-wishlist — via a new **`INotificationScheduler`** platform seam over **Plugin.LocalNotification** on the device head with a **web no-op** (phone-first, like haptics); a shared scheduler computes the set from **local occasion data** and (re)schedules/cancels on date-change / draw / wishlist-filled / delete / app-start with **stable per-(occasion,kind) ids**; **global per-kind opt-in, off by default**; OS permission requested **on first enable**; a reminder body **never** carries an assignment (NFR-002); **cross-member push parked** on the origin/hosting gate; follows ADR-0028, respects ADR-0053, iOS deferred ADR-0031) | Accepted | 2026-09-15 |
| [0063](0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md) | **Drop Cloudflare** — Infomaniak DNS + Caddy TLS via HTTP-01 (supersedes the Cloudflare edge / DNS-01 / CDN-DDoS-WAF parts of ADR-0036; whole request path stays in Switzerland, one provider, stock Caddy + default HTTP-01, no plugin/token; gives up edge CDN/DDoS/WAF + origin-IP hiding as proportionate for a free seasonal app; public privacy/imprint strings updated) | Accepted | 2026-09-16 |
| [0061](0061-share-token-and-creator-key-at-rest-protection.md) | Share-token & creator-key **at-rest protection** (ADR-0058 stored share tokens as the plaintext PK and the creator key in clear, so a DB dump yielded working links + the recovery key): store **`SHA-256(token)`** as the deterministic lookup PK **plus** an **`AES-256-GCM(token)`** copy so the **resume-share** path can still return the original link (a plain hash can't); **hash the creator key** (only ever compared); **fail-closed** on a missing key; new secret `SharedGroups:Security:TokenEncryptionKey` under ADR-0050; DeviceHandle left as-is (not a verified credential); pre-data one-way-door fix | Accepted | 2026-09-15 |
| [0062](0062-email-address-hashing-keyed-hmac.md) | Email address hashing → **keyed HMAC-SHA256** (unsalted SHA-256 was dictionary-reversible over the small address space): a new **`IEmailHasher`** port computes `HMAC(key, TRIM+UPPER(email))` at the **application boundary** and passes the hash **into the pure domain** as a value (like the invitation secret handle); deterministic equality preserved (invitation match, decline cooldown, rate-limit key); **no schema change**; **fail-closed** on a missing key; new secret `Circles:Security:EmailHashKey` under ADR-0050; pending-invitation plaintext address unchanged (needed to send); pre-data one-way-door fix | Accepted | 2026-09-15 |
| [0064](0064-transactional-email-html-and-per-app-branding.md) | **Branded HTML transactional email with per-app identity** (plaintext + `multipart/alternative` HTML with a CTA button and a fallback link; each mail is **co-branded to its source app** — name, brand colour, sender, and **links that return to the source app's origin** so the page renders in that app's shell; a `sourceApp` rides the outbox payload; resolves the open RESX-vs-own-mechanism question with a shared `MailLayout` in Infrastructure; adds a link-less **account-deleted farewell** mail (FR-047); user text HTML-encoded; SSO explainer + per-app PNG logo deferred) | Accepted | 2026-09-17 |
| [0065](0065-hosted-macos-ios-builds.md) | **iOS builds on GitHub-hosted macOS runners** (**supersedes [ADR-0031](0031-ios-build-host.md)** — no Mac bought): iOS moved onto the launch critical path (TestFlight is how the family's iPhones test, the fastest route to Play's 20-tester threshold), so the iOS release leg runs on a `macos-*` runner in `release-mobile.yml` on the same `v*` tag as Android — Xcode select → MAUI workload → signed `.ipa` + TestFlight upload when secrets exist, compile-check otherwise; **~CHF 0** at this release cadence (built only on tags/dispatch, kept out of the per-push gate) vs a CHF 650–800 Mac; signing keys in GitHub secrets ([ADR-0050](0050-secrets-management.md)); **amends** [ADR-0038](0038-ci-cd-pipeline.md) (iOS leg no longer a self-hosted Mac) and [ADR-0053](0053-backend-free-first-mobile-release.md) (iOS no longer deferred to v2+); ADR-0031's cost/durability case **preserved as the upgrade trigger** for buying a Mac later; follow-up: iOS Universal Links (AASA) for in-app reveal links) | Accepted | 2026-09-17 |
| [0066](0066-mobile-native-passkeys.md) | **Native passkeys on the mobile heads** (the mobile-passkey deferral from FEAT-002 is lifted now that the RP-ID `circletools.ch` is settled): implement `MauiPasskeyCeremony` behind the existing `IPasskeyCeremony` seam — **Android** via Credential Manager (`androidx.credentials`, consumes/produces the standard WebAuthn JSON the seam already speaks) verifiable on the Pixel now; **iOS** via AuthenticationServices (`ASAuthorizationPlatformPublicKeyCredentialProvider`, base64url↔`NSData` translation) written now, device-verified later (no local iOS loop, [ADR-0065](0065-hosted-macos-ios-builds.md)); no server/seam change (RP-ID stays `circletools.ch`); association files carry the passkey relations (`assetlinks.json` `get_login_creds`; new `apple-app-site-association` `webcredentials`+`applinks`) filled with real ids at go-live; the affordance stays behind the FR-011 fallback until those are live) | Accepted | 2026-09-17 |
| [0067](0067-android-backup-posture.md) | **Android backup posture — disable OS auto-backup** (`android:allowBackup="false"`): Android Auto Backup would silently copy on-device personal data (member names, assignments) to the user's Google Drive by default, against the local-first, data-minimised posture (NFR-004); the app's existing **export + opt-in sync** ([ADR-0015](0015-mvp-client-offline-storage.md)/[ADR-0016](0016-sync-and-conflict-resolution.md)) are the user-chosen backup path instead — consistent across Android + iOS, no native `BackupAgent`; trade-off = no automatic Google restore on reinstall/new device (user migrates via export/sync); iOS iCloud counterpart left as a separate open call | Accepted | 2026-09-18 |
| [0068](0068-live-reveal-links-for-backend-backed-groups.md) | **Live reveal links for backend-backed groups** (extends [ADR-0014](0014-reveal-link-encoding-and-privacy.md)): a reveal link is a self-contained offline snapshot, so an already-sent link shows a stale wishlist/event after an edit; now that [ADR-0058](0058-account-free-link-shared-groups.md) gives shared-by-link and account groups a backend, mint a **per-giver reveal token** carried alongside the offline `d=` snapshot — the reveal page fetches a fresh per-giver slice (recipient, current wishlist, gift note, budget, event details) online and falls back to the snapshot offline; per-giver privacy preserved (server returns only that giver's slice, never the draw, NFR-002), local-only groups unchanged; token encrypted at rest ([ADR-0061](0061-share-token-and-creator-key-at-rest-protection.md)) and dies with the server copy on `MakeLocal`; the local-only staleness case is handled instead by a "links changed since hand-out" notice (FR-045) | Accepted | 2026-09-19 |
| [0069](0069-passkey-origin-allow-list-for-native-heads.md) | **Passkey origin allow-list for the native heads** (corrects the "no server change" assumption in [ADR-0066](0066-mobile-native-passkeys.md)): ASP.NET Core Identity's default passkey origin check only accepts a ceremony whose `clientDataJSON.origin` equals the request's `Origin` HTTP header — which browsers send but the native heads (OS passkey APIs + plain `HttpClient`) do not, so every native attestation was rejected with "invalid origin". Override `IdentityPasskeyOptions.ValidateOrigin` with a config-driven allow-list (`Auth:Passkey:Origins`) that validates the origin directly: the web origins (apex + app subdomains) plus the native authenticator origins (iOS `https://circletools.ch`; Android `android:apk-key-hash:<base64url-SHA256 of the Play signing cert>`); left off when unconfigured so the dev/browser default is unchanged. Android also needs its Digital-Asset-Links file live + matching the installed signing cert (client-side, separate) | Accepted | 2026-10-01 |
| [0070](0070-always-on-bottom-navigation-on-phones.md) | **Bottom navigation in the native app (phone + iPad portrait)** (amends the signed-out "no bottom bar" lever in [ADR-0054](0054-mobile-native-feel-strategy.md)): the bottom bar was shown only for a signed-in linked member, so the primary nav changed shape on sign-in and signed-out first-run lost the one-handed bar. Drop the auth gate and show the bar on narrow viewports **in the native app** (phone, iPad portrait), signed in or out — Groups + "More" at minimum, personal tabs in between when linked (two `flex:1` tabs read balanced, so ADR-0054's no-degenerate-bar concern is honoured). Disabled on the **web** (a fixed bar collides with the browser's own bottom chrome → drawer + hamburger) and on wide native (iPad **landscape** → persistent drawer); gated on `!OperatingSystem.IsBrowser()` + the Md width breakpoint, `_showBottomBar` removed | Accepted | 2026-10-01 |
| [0071](0071-default-storage-mode-and-first-run-onboarding.md) | **Default storage mode for new groups + first-run onboarding** (refines the fixed "On this device pre-selected" rule of [FR-038](../requirements/functional/FR-038-per-group-storage-mode.md)/[ADR-0058](0058-account-free-link-shared-groups.md)): users couldn't tell where a group's data lives, had to re-pick storage per group, and the phone "+" quick-create ignored the choice (always local). Add a **device-local** default storage mode that seeds every create path (picker + phone FAB), a one-time skippable onboarding dialog explaining the three modes + per-group changeability, and a per-group storage icon in the list (mode read from the existing sync read model, no domain change); default starts "On this device" (local-first preserved); "My account" default + signed out ⇒ falls back to local and auto-claims after sign-in; rejected account-synced default (signed-out users need a default too) | Accepted | 2026-10-01 |
| [0072](0072-reflection-json-on-native-heads.md) | **Reflection-based JSON on the native heads (iOS full-trim)** — iOS Release is fully trimmed + AOT, which disables reflection-based `System.Text.Json` (`JsonSerializerIsReflectionEnabledByDefault=false`); client paths still using reflection (`RecentAccessService`'s `List<Guid>`, `ApiResponseReader`/`AccountApiClient` DTOs) threw `InvalidOperationException` only on iOS, breaking create/edit/open and account reads while Android (partial trim) + web (WASM) worked. Re-enable it for the MAUI app with `JsonSerializerIsReflectionEnabledByDefault=true` (one line, comprehensive, matches Android/web); source-generating those paths so full trimming can return is the tracked follow-up | Accepted | 2026-10-02 |
| [0073](0073-storage-mode-is-authoritative.md) | **Per-group storage mode is authoritative** (refines [FR-038](../requirements/functional/FR-038-per-group-storage-mode.md)/[ADR-0058](0058-account-free-link-shared-groups.md); amends the auto-claim of [ADR-0044](0044-sign-in-reconciliation-and-account-scoped-visibility.md)): create recorded both "On this device" and "My account" as `Mode=OnThisDevice`, and auto-claim then claimed every `OnThisDevice` group into the account, so signed-in the two modes collapsed to "account". Make `Mode` the intent: `ClaimAsync` sets `Mode=Account`; new `MarkForAccountAsync` records account intent on create (offline-safe); **auto-claim claims only `Mode==Account`** so "On this device" stays local while signed in; create wires the "My account" choice. Follow-up: one compact create affordance (icon + Change) replacing the inline picker + silent phone default. *Amended 2026-10-15:* "My account → On this device" is a move — the other devices drop their copy (one with unsynced edits keeps it as a local copy and says so) | Accepted | 2026-10-02 |
| [0074](0074-aot-safe-mediator-pipeline-over-value-types.md) | **AOT-safe mediator pipeline over value-type results** (fixes [ADR-0006](0006-application-layer-and-mediation.md)'s dispatcher on the iOS full-AOT head; same wall as [ADR-0072](0072-reflection-json-on-native-heads.md)) — the CQRS dispatcher resolved its pipeline behaviors (`LoggingBehavior`/`AutoSyncBehavior`) closed over the command's **result type**, so a value-type result (`Guid` for create/add; the internal `Unit` struct for every void edit/rename/delete) made DI materialise `IPipelineBehavior<…,Guid>`/`<…,Unit>` at runtime, which Mono full-AOT cannot do (`InvalidOperationException: AotCannotCreateGenericValueType`) — create/add/edit were dead on iPad while Android + web worked. Run the pipeline over a **boxed `object` response** so the behaviors are only ever closed over reference types (shared generics); box the handler result out, unbox once at the end. No public-contract/behavior/registration change; covers `Guid`, `Unit`, and any future value-type result at once | Accepted | 2026-10-07 |
| [0075](0075-operator-read-only-database-access.md) | **Operator read-only access to the production database** — the Postgres container publishes no port and the only credential is the app's read-write one, while [ADR-0020](0020-server-side-assignment-privacy.md) makes raw access expose plaintext pairings + PII. Grant a dedicated **`circletools_readonly`** `SELECT`-only role reached over an **SSH tunnel** to the internal container (no public DB port — [ADR-0036](0036-cloud-provider-and-hosting.md) preserved), password off-repo ([ADR-0050](0050-secrets-management.md)); route routine "how much data" questions to the PII-free [FEAT-022](../features/FEAT-022-business-metrics-dashboard.md) Grafana board and reserve raw access for real debugging; rejected a public web DB UI (attack surface) and reusing the app role (write-capable); masked read-only views recorded as the mechanism for any future less-trusted grantee | Accepted | 2026-10-13 |
| [0076](0076-docs-site-to-private-github-pages.md) | **Publish the DocFX site to private GitHub Pages** — CI builds and link-checks the docs site but discards it; there is no live wiki. The repo is private and the docs hold internal security/architecture detail ([ADR-0020](0020-server-side-assignment-privacy.md)/[ADR-0050](0050-secrets-management.md)/[ADR-0075](0075-operator-read-only-database-access.md)), so public Pages would leak them. Add a `docs-pages.yml` workflow that builds the site (same `docfx` build) and deploys to GitHub Pages with **private** access control; the deploy job is **gated on a `DOCS_PAGES_ENABLED` repo variable** (like deploy.yml) so the default state is *no site*, never a public one. Rejected public Pages (exposure); artifact-only kept as the gated-off fallback; VPS self-host (Caddy+auth) is the fallback if the plan lacks private Pages | Accepted | 2026-10-13 |
| [0077](0077-retract-linking-projection-on-group-delete.md) | **Deleting a group retracts its server-side linking projection; device-only groups do not self-link** — a deleted group stayed on My assignments / My wishlists because the linking store had no delete path, and the automatic self-link on create published even "On this device" groups (contradicting [ADR-0073](0073-storage-mode-is-authoritative.md)). Add an organizer-scoped, idempotent `DELETE /groups/{id}/linking`, driven by a queued `SyncState.Retractions` sent on the next signed-in cycle (offline-safe, like tombstones); stop the automatic self-link for device-only groups. Rejected tombstone-only (misses account-free groups) and a synchronous call (breaks offline delete) | Superseded by [0086](0086-one-way-to-be-in-a-group.md) | 2026-10-14 |
| [0078](0078-my-account-groups-record-their-account.md) | **Every "My account" group records which account it belongs to** — a group marked My account but not yet claimed, or a shared-by-link group whose admin was claimed into an account, had `Mode = Account` but no `AccountScope`, so it showed while signed out and to other accounts, was offered in the import prompt, and an admin-claimed one was auto-claimed into the account store a second time. Add additive `PendingClaimAccount` / `AdminAccount` record fields; visibility, the import prompt and the auto-claim use the recorded owner; legacy records resolve on the next signed-in cycle. Rejected treating `Mode` alone as account-bound (can't tell accounts apart) and overloading `AccountScope` (wrong server path). *Amended 2026-10-15:* (1) deleting reaches the online copy (owner delete); (2) other devices follow the delete (404 vs 403); (3) the two kinds are told apart by `AdminAccount`/`AccountScope` everywhere, a pull keeps local facts, the owner gets the join link, a same-account join holder becomes the owner | Superseded by [0086](0086-one-way-to-be-in-a-group.md) | 2026-10-15 |
| [0079](0079-sign-out-this-device-and-change-password.md) | **Sign out ends this device's session; "sign out everywhere" and password change are explicit** — `POST /logout` revoked every session, so signing out on one device silently signed out all others. Logout now revokes only the presented refresh chain (no body = old revoke-all, for old apps); new `POST /logout-all` (Security → "Sign out on all devices"); new `POST /change-password` (current password, policy, breached check) revokes the other sessions and mails a notice; a refused refresh signs the client out with a notice. Rejected keeping revoke-all and asking on every sign-out | Accepted | 2026-10-15 |
| [0080](0080-participant-access-to-shared-groups.md) | **A participant sees only their own pairing, edits only their own entry, and never publishes** — a join-token read returned the whole group (every pairing, all wishlists), a participant could turn the group into a local copy with everyone's reveal links, could not edit their own wishlist, and published the linking projection (500s). The server now trims a join read to the proven member's pairing and their own/recipient's wishlist and gift note; a server-minted participant key (or the account bearer) proves the member; a narrow `PUT /shared/{token}/me` writes the own entry, re-applied over stale organizer pushes; only the organizer may publish the linking projection (403 otherwise). Rejected client-side filtering, organizer-published participant views, whole-body participant pushes | Accepted | 2026-10-16 |
| [0081](0081-signed-in-sharing-stays-in-the-account.md) | **A signed-in organizer's shared group stays in their account** — a My-account group could not be shared, and sharing a group by link moved it out of the account (gone from the organizer's other devices). Signed in, sharing now shares and claims admin in one step (owned, synced to all their devices, join link kept); My account offers "Invite people"; stop sharing returns it to the account store; the owner can release a joined person. Signed out, Shared by link is unchanged. Rejected explaining the old model better and a fourth storage mode | Accepted | 2026-10-16 |
| [0082](0082-short-reveal-links-for-online-groups.md) | **Short reveal links for online groups** — a group with published live slices shares `/r#for=<name>&rt=<token>` (about 90 characters) instead of the long snapshot link, once its slices are on the server; the reveal page fetches the slice and keeps it on the device for offline reopening. The token stays in the fragment (no logs, no preview). Long links remain for local-only groups and whenever publishing failed. Rejected keeping long links and path-based codes | Accepted | 2026-10-16 |
| [0083](0083-links-in-wishlist-items.md) | **Links in wishlist items are recognised, not entered in a special way** — the first http(s) address anywhere in a wish is shown to the giver as a tappable "Open link (site)"; nothing to type, no format change, so old lists, links, server copies and older clients keep working. A typed "·" convention with prices was tried and dropped as confusing; rejected structured items | Accepted | 2026-10-16 |
| [0084](0084-own-entry-from-the-reveal-link.md) | **The reveal link lets its holder keep their own wishlist and gift note (live-link groups)** — the reveal page told every recipient to send their wishes to the organizer. For Shared-by-link and My-account·shared groups the reveal token now also reads and writes its own member's gift note and that event's wishlist (`GET`/`PUT /shared/reveal/{rt}/me`), stored as an ADR-0080 participant entry; the organizer's copy turns read-only; the server learns only the giver of a token, never the pairing; the giver sees the change after the organizer's device syncs. Not for linked members or snapshot-link groups. Rejected last-write-wins, live links for private account groups, and a read-only link | Accepted | 2026-10-17 |
| [0085](0085-one-place-for-a-members-own-entry.md) | **One place for what a member keeps themselves** (R16, 2026-10-22 database review): a member's own wishlist/gift note lives in two tables — `shared.member_entries` (joined participants, reveal-link holders; merged into the group document) and `linking.member_wishlists` (linked members; never merged, read only for account-linked givers) — so a linked member's wishlist misses most givers and is lost on unlink. Proposed: one member-entry table keyed by member for every route, one owner per member, merged by the server into whichever store holds the document (shared store as today; the sync store via a registered body hook, re-applied on every push); *On this device* groups, which have no server copy, apply entries on the organizer's device; existing rows migrate without re-entry. Rejected: the organizer's device relaying server data, unifying only shared groups | Superseded by [0086](0086-one-way-to-be-in-a-group.md) | 2026-10-22 |
| [0086](0086-one-way-to-be-in-a-group.md) | **One way to be in a group: two places, one group store, participants, derived views** — three server stores (`sync`, `shared`, `linking`), two access models for people with an account (linked member vs participant), four user-facing kinds and published copies (assignments, event registry, wishlist snapshots, reveal slices) caused R16, unlink data loss, two owners per member, stale reveal pages and S1/S2. Groups live *on this device* or *online* (one Secret-Santa group store, join link as a setting); access as role assignments checked by permission (owner and participant now, more admins later as a new role); one owner (account or organizer link); every route in (join link, email invitation, circle, own member) creates the same participant; one member-entry table merged into the document; every view derived on read (participant view, account overview, reveal page); a Secret-Santa server module reads the document through `SecretSanta.Core`'s `GroupState` with a schema version; the sync store keeps per-account documents only; no migration (test data dropped). Supersedes ADR-0019, 0020 (storage), 0077, 0078, 0085 | Accepted | 2026-10-22 |
| [0087](0087-production-data-is-kept.md) | **Production data is kept from 2026-10-25**: migrations never drop or delete data (expand, then contract; enforced by `NonDestructiveMigrationTests` with a closed list of the four migrations from before real data); the group document changes additively; devices set aside what they can't read instead of discarding it; a backup and restore drill before every release that migrates | Accepted | 2026-10-25 |
| [0088](0088-mail-capacity-alerting.md) | **Alert before Brevo's 300/day cap**: two outbox-derived gauges (mails sent in a rolling 24 h, undeliverable mails) on the business meter; Grafana alert rules provisioned from git warn at 200 and 270 and on new delivery failures, by email to `ALERT_EMAIL_TO`; first Grafana alerting (ADR-0051 upgrade path) | Accepted | 2026-10-28 |
| [0089](0089-circle-as-a-product-name-in-every-language.md) | **"Circle" as a product name in every language**: the UI says Circle / Circles in DE, FR and EN (capitalised, local grammar around it) instead of "Kreis" / "cercle"; the circle's own label is unchanged | Accepted | 2026-10-31 |
| [0090](0090-backend-stays-compatible-with-released-apps.md) | **The backend stays compatible with every released app**: API contract, documents and behaviour keep working for every app version shipped through a store (no minimum-version gate yet); a stale push must never delete data for good; every server change is checked against the oldest released app | Accepted | 2026-10-31 |
| [0091](0091-ntfy-push-alerts-and-watchdog.md) | **Push alerts through a self-hosted ntfy, and a watchdog outside the server**: ntfy on the VPS (deny-all, users/tokens from `.env`, shared with the owner's private senders) as the alert channel independent of Brevo; Grafana's `owner` contact point sends every alert by email and push; an always-firing watchdog rule pings healthchecks.io, which alerts when the VPS or its alerting stops | Accepted | 2026-10-31 |

### Recently closed (iOS build path reversed — 2026-09-17)

Raised when iOS moved onto the **launch** critical path: most of the family uses iPhones, TestFlight is
the fastest way to onboard them (and to reach Google Play's ~20-tester threshold), but the project owns
no Mac and buying one just to unblock a family test is a real cash outlay for a free app.

- ✅ **iOS build path** → [ADR-0065](0065-hosted-macos-ios-builds.md) (**supersedes
  [ADR-0031](0031-ios-build-host.md)**). Build, sign and upload iOS on **GitHub-hosted macOS runners** in
  `release-mobile.yml`, on the same `v*` tag as Android — ~CHF 0 at this release cadence (tags/dispatch
  only, kept out of the per-push gate) instead of a CHF 650–800 Mac. **Amends**
  [ADR-0038](0038-ci-cd-pipeline.md) (the iOS leg is no longer a self-hosted Mac runner) and
  [ADR-0053](0053-backend-free-first-mobile-release.md) (iOS is no longer deferred to v2+). ADR-0031's
  cost/durability reasoning is **kept as the upgrade trigger** for owning a Mac later. **Follow-ups:**
  define the iOS signing secrets ([ADR-0050](0050-secrets-management.md)); add iOS **Universal Links**
  (an `apple-app-site-association` file + Associated Domains) so installed iPhones open reveal links in
  the app, as ADR-0053 already does for Android.

## Open decisions backlog

The Secret-Santa MVP shipped, and the **v2 domain decisions that were deferred behind it are now
recorded** as ADR-0016…0021, resolved by the
[accounts, families & sync brainstorming session](../brainstorming/2026-07-24-accounts-families-sync.md).
What remains open genuinely depends on the deployment phase or on evidence we do not yet have.

### Recently closed (v2 — 2026-07-27)
- ✅ **Sync & conflict resolution** → [ADR-0016](0016-sync-and-conflict-resolution.md). Includes the
  web-client offline storage sub-decision: `localStorage` per
  [ADR-0015](0015-mvp-client-offline-storage.md) **stays**, extended with sync metadata.
- ✅ **Anonymous → account claim** → [ADR-0017](0017-anonymous-to-account-claim.md) (choose per group).
- ✅ **Circle & sharing model across users** → [ADR-0018](0018-circle-and-sharing-model.md).
- ✅ **Member↔User linking** → [ADR-0019](0019-member-user-linking.md) (optional `UserId`).
- ✅ **Server-side assignment storage & privacy** → [ADR-0020](0020-server-side-assignment-privacy.md);
  **partly supersedes** [ADR-0014](0014-reveal-link-encoding-and-privacy.md) and amends
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md).
- ✅ **Web auth token model** → [ADR-0021](0021-web-auth-token-model.md) (bearer in WASM), closing the
  follow-up deferred in [ADR-0009](0009-authentication-and-passkeys.md).
- ✅ **Account deletion & data export** → [ADR-0022](0022-account-deletion-and-data-export.md)
  (immediate hard delete; the export reuses the ADR-0020 authorization boundary). Raised by the M1
  identity use cases, and legally due now that v2 stores PII. **Follow-up: NFR-004 (data protection &
  retention) does not exist yet** — first task of the M1 requirements step.

### Recently closed (M1 client — 2026-07-28)

Raised by building the Main app's Blazor client. The first two were being decided silently in code —
the template's Bootstrap, and a storage choice with no record; the third is a gap ADR-0021 left open
on purpose until there was a client to be concrete about.

- ✅ **UI styling & theming** → [ADR-0024](0024-ui-styling-and-theming.md) (Bootstrap 5 + a thin suite
  theme; brand deliberately absent, per [naming](../process/naming.md)).
- ✅ **Shared UI building blocks** → [ADR-0025](0025-shared-ui-building-blocks.md), making
  [arc42 ch.5](../arc42/05-building-block-view.md)'s planned blocks real. Its follow-up —
  Secret-Santa's own culture implementation — **is now closed** (see below).
- ✅ **Refresh-token persistence in the browser** →
  [ADR-0026](0026-refresh-token-persistence.md), extending
  [ADR-0021](0021-web-auth-token-model.md) with the session/durable split behind "stay signed in".

### Recently closed (Secret-Santa migration — 2026-07-28)

Raised by carrying out ADR-0025's recorded follow-up: migrating Secret-Santa onto the shared blocks.
Both decisions exist because the follow-up turned out to touch more than Secret-Santa — the second
web host made a Main-only adapter shared, and the second kind of caller made the storage contract
insufficient.

- ✅ **Where browser platform adapters live** → [ADR-0028](0028-browser-platform-adapters-block.md)
  (a new `CircleTools.Ui.Web` block), **amending** ADR-0025's "a host supplies the platform" rule.
  **Follow-up: the remaining three adapters** (`BrowserFileSaver`, `BrowserPasskeyCeremony`,
  `BrowserCredentialsHandler`) stay in `Main.WebApp` until a second host needs them.
- ✅ **Storage write-failure contract** → [ADR-0029](0029-storage-write-failure-contract.md)
  (`SetAsync` reports success), so a lost preference and a lost group stop being the same event.
  **Follow-up: the Secret-Santa UI does not yet present the failure** — the exception reaches
  Blazor's error UI, as a failed write already did.

### Recently closed (distribution — 2026-07-29)

Raised by working out what it actually takes to put the MAUI heads in the two stores. Both are
deployment decisions, but neither waits on the cloud provider — they depend on identity and on
hardware, and both have long lead times, so deciding them early is what keeps them off the critical
path.

- ✅ **Store publisher identity & account ownership** →
  [ADR-0030](0030-mobile-store-publisher-identity.md) (enrol as a private individual, on accounts
  created for the project). **Follow-up: point both accounts at a suite-domain role address** once
  [naming](../process/naming.md) lands.
- ✅ **iOS build host** → [ADR-0031](0031-ios-build-host.md) (an own Apple-silicon Mac; the Intel
  MacBook Pro is at best a one-release bridge). Feeds the still-open **CI/CD** item below by making a
  self-hosted macOS runner possible. **Superseded 2026-09-17 by [ADR-0065](0065-hosted-macos-ios-builds.md)**
  — iOS now builds on GitHub-hosted macOS runners (see the 2026-09-17 entry below); ADR-0031's analysis
  survives as the upgrade trigger for buying a Mac later.

Both are governed by [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md),
which states the recurring platform obligations that outlive any single release.

### Recently closed (M2 circles — 2026-07-30)

Raised by driving M2's circle screens in a real browser. The Circles strings had been written
informally while M1's account strings were formal, and the sole-owner refusal put both registers in
one red panel — a defect no test could see.

- ✅ **UI tone of voice & gender-neutral role terms** →
  [ADR-0032](0032-ui-tone-of-voice-and-gender-neutral-terms.md) (German `du`, French `vous`, colon
  form for German agent nouns). The convention is recorded in the
  [localization guidelines](../guidelines/localization-guidelines.md) so new strings follow it
  without rediscovering the ADR.

### Recently closed (M3 requirements — 2026-08-03)

Raised by writing the M3 sync requirements, and closed before the M3 build begins.

- ✅ **Which aggregates sync** → [ADR-0033](0033-which-aggregates-sync.md). ADR-0016 named both the
  Secret-Santa [Group](../glossary/group.md) and the [Circle](../glossary/circle.md) as sync units;
  [FR-023](../requirements/functional/FR-023-sync-across-devices.md) **excludes the circle**, because M2
  built circles server-authoritative and online-only, last-write-wins would silently discard *other
  people's* edits on data several admins share, and an offline circle write cannot take the server-side
  authorization decision [NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md)
  requires on every mutation. ADR-0033 **narrows** ADR-0016 (which stays Accepted) rather than leaving a
  divergence, and records the one axis that could bring circles back later: an offline **read cache** is
  a separate decision from offline **writes**, and is parked below.

### Recently closed (M3 build — 2026-08-12)

Raised while building the M3 profile-sync slice, and closed before it is implemented.

- ✅ **Profile-sync seam** → [ADR-0035](0035-profile-sync-seam.md). The client sync engine lives only
  in the Secret-Santa app, but the profile screen lives in the shared
  [account block](0034-shared-account-session-ui-block.md), and the authoritative profile (which drives
  the JWT locale and the Main app) lives on the Main service's `/account/profile` endpoint — not the
  opaque sync store. A host-supplied `IProfileWriter` port lets the Main app keep its direct online
  write while the Secret-Santa app queues an offline edit locally and `PUT`-flushes it to `/profile` on
  the next sync trigger — closing [FR-012](../requirements/functional/FR-012-manage-profile.md)'s offline
  language change and [FR-023](../requirements/functional/FR-023-sync-across-devices.md)'s profile sync
  with the Identity profile as the single source of truth.

### Recently closed (deployment — 2026-08-13)

Raised as the "depends on deployment" items below and closed by pricing the concrete Swiss options.
Both record the *decision* now; both have an **implementation prerequisite on the [brand & domain](../process/naming.md)**
gate (the final origin fixes the passkey RP-ID, CSP and mail from-address), exactly as
[ADR-0030](0030-mobile-store-publisher-identity.md)/[ADR-0031](0031-ios-build-host.md) are Accepted
though execution waits on the brand and a Mac.

- ✅ **Cloud provider & hosting target** → [ADR-0036](0036-cloud-provider-and-hosting.md) (a single
  Infomaniak VPS + Docker Compose; self-managed Postgres with nightly dumps to Swiss Backup; ~CHF 12–14/mo)
  — the **Cloudflare edge was later dropped**, [ADR-0063](0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md)
  (Infomaniak DNS + Caddy HTTP-01). Swiss data residency for data at rest; Kubernetes and hyperscalers
  rejected as disproportionate to a seasonal single-service load.
- ✅ **Transactional email provider** → [ADR-0037](0037-transactional-email-provider.md) (Brevo free
  tier, EU-hosted, behind the existing invitation outbox port; Postmark named as the paid fallback).
  Closes M1's verification/reset/email-change and M2's invitation deliverability.

### Recently closed (CI/CD — 2026-08-13)

Raised as the "depends on deployment" CI/CD item below, and closed now that the code lives on GitHub
(org `CircleTools-ch`, repo `circletools-suite`) and a PR-based workflow needs an enforceable gate.

- ✅ **CI/CD pipeline** → [ADR-0038](0038-ci-cd-pipeline.md) (GitHub Actions). The **CI gate is built
  now** — `build-test` (non-MAUI solution + full test run), `docs` (link-checked DocFX), and
  `build-maui` (Android + Windows heads; iOS deferred to a macOS runner per
  [ADR-0031](0031-ios-build-host.md)) — satisfying the Definition of Done's "CI green" and closing
  [arc42 ch. 11](../arc42/11-risks-and-technical-debt.md) D5. **CD is documented but deferred to
  milestone 8**: it needs the [ADR-0036](0036-cloud-provider-and-hosting.md) VPS to exist (SSH →
  `compose pull && up`), and carries ADR-0036's still-open **secrets management** follow-up.

### Recently closed (Secret-Santa boot outage — 2026-08-21)

The first deploy after [ADR-0048](0048-web-app-update-delivery.md) took Secret-Santa down (fresh
sessions hung on the loading splash); Main was fine.

- ✅ **Disable WASM fingerprinting for CSP** → [ADR-0049](0049-disable-wasm-fingerprinting-for-csp.md).
  Root cause: the two WASM heads had drifted — Main disabled framework-asset fingerprinting
  (`OverrideHtmlAssetPlaceholders=false`) because the inline **import map** it emits is blocked by the
  strict inline-free CSP ([ADR-0021](0021-web-auth-token-model.md)) and stops the runtime booting;
  Secret-Santa still had it on. Fixed by bringing Secret-Santa in line (no import map, stable asset
  names). This also **corrects** [ADR-0048](0048-web-app-update-delivery.md)'s `immutable` cache header,
  which would serve stale files once names are stable — all responses now revalidate.

### Recently closed (web app update delivery — 2026-08-21)

Raised the moment CI/CD ([ADR-0047](0047-versioning-and-release-strategy.md)) shipped its first deploy:
a returning user kept seeing the **old** version because the stock offline service worker parks a new
build in the "waiting" state until every tab closes, and never says so.

- ✅ **Web app update delivery** → [ADR-0048](0048-web-app-update-delivery.md). Keep the **offline
  PWA** (local-first is a product promise, [ADR-0015](0015-mvp-client-offline-storage.md)) and add a
  shared, localized **MudBlazor "Reload" prompt**: the service worker gains a `SKIP_WAITING` handler,
  `js/app.js` bridges the worker's update lifecycle to .NET, and `UpdatePrompt` (in `Ui.Shared`, mounted
  in both shells, inert on MAUI) offers a one-tap switch — **on the user's terms**, so a background
  deploy never interrupts a draw or a half-filled form. Adds edge cache headers (fingerprinted
  `_framework` immutable, control files revalidated). Native heads update through the store, unchanged.

### Recently closed (versioning & release — 2026-08-21)

Raised as [ADR-0038](0038-ci-cd-pipeline.md)'s two deferred CD follow-ups — *how versions are assigned*
and *what triggers a deploy* — now due as milestone 8 begins.

- ✅ **Versioning & release strategy** → [ADR-0047](0047-versioning-and-release-strategy.md).
  **Nerdbank.GitVersioning** from a committed `version.json` (source of truth in the repo, rule 9)
  stamps every assembly with `0.1.<git-height>` — automatic and monotonic, no hand-edits. **Triggers are
  hybrid**: merge to `main` builds versioned images and (once the VPS exists) deploys the server
  continuously, while a **`v*` tag** cuts a deliberate mobile store release — because a server we own and
  a store-reviewed app have different release mechanics. **One suite version now**, stamped everywhere for
  provenance, with a no-rewrite path to per-app `version.json` when cadences diverge; nbgv computes the
  Android `versionCode` from the version, which stays monotonic across minor bumps (corrected 2026-11-06). The
  image-build/push runs now; the SSH rollout **self-activates** when `DEPLOY_HOST` is set. **Follow-ups:
  bump to `1.0` at launch; wire the store-upload leg once signing secrets exist**
  ([ADR-0030](0030-mobile-store-publisher-identity.md)); record **secrets management** at M8.

### Recently closed (packaging & composition — 2026-08-19)

Raised as the "depends on deployment" packaging item below, and closed before the first store
submission — because two of its outputs (a released bundle id, a passkey RP-ID) are permanent.

- ✅ **CircleTools packaging & app composition** → [ADR-0046](0046-suite-packaging-and-app-composition.md).
  A **federated suite**, not a container: each app is its own installable (permanent
  `ch.circletools.<app>` bundle id, one store listing) and its own web origin
  (`<app>.circletools.ch`, apex `circletools.ch` as the passkey RP-ID); the Main app is the suite
  **hub** (account, dashboard, Circles, SSO authority), not a host that embeds other apps. This
  ratifies the shape already built — two origins ([ADR-0005](0005-web-hosting-render-model.md)),
  cross-origin SSO ([ADR-0039](0039-cross-app-single-sign-on.md)) that presupposes separate origins,
  and a per-app MAUI head — and reserves the identifier namespace so the next app inherits the
  decision instead of reopening it. The accepted cost is N apps = N store listings / review cycles.

### Depends on deployment (genuinely open)

> [ADR-0023](0023-local-dev-infrastructure-in-apphost.md) lets **implementation** proceed without
> these, via containerised dev infrastructure behind provider-agnostic seams. It buys time; it does
> **not** close them.

_None — the packaging & composition item that stood here is closed as
[ADR-0046](0046-suite-packaging-and-app-composition.md) (see above)._

### Parked — revisit on a concrete trigger
- **Per-member envelope encryption of assignments** (option P3 in
  [ADR-0020](0020-server-side-assignment-privacy.md)) — if operator-as-trusted-party stops being
  acceptable.
- **IndexedDB / SQLite-in-OPFS client storage** — if data volume or partial-query needs outgrow
  load-all/save-all ([ADR-0016](0016-sync-and-conflict-resolution.md)).
- **Invite by link or short code** — if email delivery proves to be the friction point.
- **Conflict-resolution UI** and **continuous background sync** — if the escalation hook in ADR-0016
  shows collisions actually occur.
- **Offline *read cache* for circles** — [ADR-0033](0033-which-aggregates-sync.md) excludes circle
  data from sync; a read-only cache so circle screens work while briefly offline is a *separate*
  decision (no conflict, no deferred authorization). Revisit if users report circle screens are
  unusable on flaky connections. It does **not** reopen offline circle *writes*, which stay excluded.
