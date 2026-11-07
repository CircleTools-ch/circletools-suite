# Deployment — Production-release plan (Secret-Santa 1.0)

Written **2026-10-20**, after the release device test
([2026-10-20 session](../brainstorming/2026-10-20-secret-santa-release-device-test.md)). The web stack has
been live since 2026-09-18 ([launch-readiness](launch-readiness.md)); this plan covers what still stands
between today and a **public 1.0 in both app stores**, and the **order** in which to do it.

## The constraint: the calendar, not the code

Secret Santa is a December product. The Play Store gate for a personal developer account
([mobile runbook, Track A step 9](mobile-apps.md)) is **12 testers opted in for 14 continuous days**,
then a production-access review of **up to 7 days**. Started on 2026-10-20, that is production access
around **mid-to-late November** at the earliest — every week of delay comes straight off the season.

So the work runs as **parallel tracks**, with the long-lead store steps started first and the code work
(updates, review) feeding new builds into the already-running tests.

| Track | Start | Why now |
|---|---|---|
| **1. Play closed test** | immediately, with the current build | 14-day clock + review; new builds can be uploaded to the closed track without resetting the clock, and regular updates show active testing |
| **2. Apple App Store setup** | immediately | no tester gate, but record, declarations, privacy labels and App Review take calendar time |
| **3. Dependency & toolchain updates** | now (branch) | before the review, so the review covers the shipped code |
| **4. Full project review** | after track 3 | code, deployment, database, security, operations |
| **5. Store listings & screenshots** | last | only once the UI is stable, or screenshots are redone |

## Track 1 — Google Play closed test

> **Status 2026-10-28 (Play Console check):** the closed track *Closed testing - Alpha* exists but has
> **no release, no countries and no testers** — 0 testers opted in, so the 14-day clock has **not started**.
> Store listing (EN/DE/FR), store settings and every App-content declaration are filled and verified against
> `store/google-play/`; all of it sits unsent in *Publishing overview* until the first closed-test release
> is sent for review together with it.
>
> **Status 2026-10-29 (Play Console check):** Google **approved** the submission — *Publishing overview*
> shows *Last published on 29 October 2026* with nothing pending. *Closed testing - Alpha* is **Active**:
> release **0.1.131** *Available to selected testers* (released 29 Oct 07:11), 178 countries/regions,
> testers = the Testers Community Google Group `testers-community@googlegroups.com`, feedback address
> `info@circletools.ch`. Join link: `https://play.google.com/apps/testing/ch.circletools.secretsanta`.
> The 14-day clock starts once ≥ 12 of the service's testers have opted in and installed.
>
> **Status 2026-10-30:** Testers Community reports **25 testers**; Play Console ticks *Have at least 12
> testers opted in to your closed test*. The **14-day clock runs from 2026-10-30** — earliest
> production-access application around **2026-11-13**. First cadence build: R1 = **1.0.1**, promoted from
> internal testing to *Closed testing - Alpha* and sent for review on 2026-10-30 (backlog PR-12).

- [x] ~~Recruit **~15 testers** (family and friends, no paid tester services).~~ **Changed 2026-10-29
      (owner decision, see backlog PR-1):** paid closed-testing service — Testers Community **Pro**
      (25 testers). Ask for written feedback — the production-access application asks what testing produced.
- [x] Tester list in Play Console — the service's Google Group is set on the Alpha track.
- [x] Promote the current build to a **closed testing** track — 0.1.131 on Alpha, approved 2026-10-29.
- [x] Confirm the **data-safety form** still matches the app *now* — verified 2026-10-29 against
      `store/google-play/README.md` (backlog PR-1).
- [x] Submit the app on Testers Community with the join link above — done 2026-10-29 (Pro, 25 testers;
      their run is 16 days, Day 0 on submission).
- [ ] Track opt-ins; the 14-day clock starts once the release is approved **and** 12 testers are in. Keep
      ≥ 12 for the whole window. — 12-tester gate met 2026-10-30 (25 testers); keep it up until day 14.
- [ ] Ship each fix build (from tracks 3–4) to the closed track during the window — **cadence decided
      2026-10-29: a small release every 2–3 days**, slices R1…R5 in backlog PR-12.
- [ ] Day 14+: apply for production access; then staged rollout (20 % → 50 % → 100 %).

## Track 2 — Apple App Store

> **Status 2026-10-28 (App Store Connect check):** version 1.0 metadata is complete in DE (primary), EN
> (US/UK/AU/CA) and FR and matches `store/app-store/`; App Information, age rating (4+, incl. the new
> social-media questions), DSA (non-trader, active), pricing (free) and availability (174 regions, not China
> mainland; Mac and Vision Pro off) are set; the privacy label (5 data types) is saved but **awaits the
> owner's *Publish***; TestFlight test information and review details are filled; build `0.1.128` is
> processed. Open: accept the updated **Program License Agreement**, screenshots, the 1.0 version bump
> ([ADR-0047](../adr/0047-versioning-and-release-strategy.md)) so a build matches version 1.0, then submit.

See [mobile runbook, Track B](mobile-apps.md#track-b--apple-app-store) for the step detail.

- [ ] First TestFlight build on our own iPad/iPhone; verify Universal Links and native passkeys on device
      (backlog: open device-verify items).
- [ ] App Store Connect record; **DSA trader status** declaration (mandatory, or the app is removed from the
      EU store).
- [ ] **Privacy labels** — must match actual collection (same inventory as the Play data-safety form).
- [ ] Age rating, export compliance, content rights.
- [ ] **Support URL** (required) — decide what it points to (see "Support" below).
- [ ] **App Review demo account** with a pre-filled group, so reviewers can exercise sign-in and sync.
- [ ] Listing DE/EN/FR: name, subtitle, description, keywords, promotional text — mirror the Play copy.
- [x] Screenshots per device class, incl. **iPad** (required because the app supports iPad) — iPad 13" and
      11" 2026-10-30, iPhone 2026-10-31, each DE/EN/FR in `store/app-store/screenshots/`; uploaded to App
      Store Connect 2026-10-31 (iPhone medium + iPad 13", German, 4× English, French).
- [x] Submit for review — **2026-10-31: version 1.0.1 with build 1.0.1** (launch commit `706dc24`) submitted,
      *Waiting for Review*, manual release. Before that the updated Program License Agreement was accepted
      and the version page renamed 1.0 → 1.0.1 to match the build. TestFlight external testing optional
      (needs a Beta App Review).
- [x] Release — **2026-11-05: approved by App Review (5 days in the queue) and 1.0.1 released manually** in
      174 regions (owner decision). This build predates the 2026-10-31 legal-text and member-removal fixes
      (`f8c4a3e`, LR-1 to LR-8), the 1.0.2 fixes (`be6385d`), "Circle" (`25bd5e4`) and the alphabetical lists
      (`1b80efd`), so the in-app legal pages are outdated until the next version.
- [x] App Store link on the web — 2026-11-05: `AppStoreLinks` in the Secret Santa web app's production config,
      shown on the Groups and join pages (owner decision), never in the native apps. No "Android coming soon"
      (owner decision): the date is not ours and the section only holds working links. Reaches users with the
      next web deploy. **When Play production is live, add the Android entry** (the URL is in the config comment).
      2026-11-06: the link shows **Apple's official black badge** (owner decision), downloaded unmodified from
      Apple's badge service into `CircleTools.Ui.Shared/wwwroot/img/store-badges/` in DE, EN and FR, and served
      from our own site. Apple's rules followed: at least 40 px high (48 px here), clear space of a quarter of its
      height, one per layout, first in a lineup, credit in the imprint (*Trademarks*). Check the credit against
      Apple's Trademark List. With Android, add Google's official badge the same way, including its rules.
- [x] Submit the next App Store version, so the corrected legal texts reach iOS — **2026-11-06: 1.0.10** (build
      1.0.10 from `6736887`, tested quickly on a device by the owner) submitted, *What's New* and promotional text
      in German, French and all four English storefronts, manual release, 7-day phased release on. The "1.0.2" of
      `be6385d` was never built; the number comes from the git height (ADR-0047).
- [ ] When 1.0.10 is approved: *Release This Version*.
- [x] Same build to Play — **2026-11-06: 1.0.10** (versionCode 16777226) promoted from internal testing to
      *Closed testing - Alpha* at 100 %, notes in DE/EN/FR, sent for Google's review (backlog PR-12). Play Console
      showed 7 of the 14 days with 12 testers. Two routine warnings: no deobfuscation file, no native debug symbols.
- [ ] When Google approves it: **publish under *Publishing overview → Changes ready to publish***. Managed
      publishing is on, so an approved release does not go out by itself.

## Track 3 — Dependency & toolchain updates

Snapshot from `dotnet list CircleTools.slnx package --outdated` (2026-10-20):

| Group | Current → latest | Risk |
|---|---|---|
| ASP.NET Core / EF Core / Extensions / Identity / JSInterop | 10.0.10 → **10.0.13** | low — servicing (security fixes); bump together, keep EF Core and Identity on one band |
| Npgsql EF provider | 10.0.0 → 10.0.3 | low; re-check the `Relational` pin comment |
| MAUI Controls / WebView.Maui | 10.0.20 → **10.0.110** | medium — newer MAUI servicing band; device-test Android + iOS after |
| Android / iOS workloads | installed 36.1 / 26.5 | medium — align with the MAUI band; iOS already runs on the `xcode-27` runner (see `release-mobile.yml`) |
| MudBlazor | 9.8.0 → 9.11.0 | medium — UI library minors can shift visuals; browser pass required |
| Plugin.LocalNotification | 12.0.0 → **14.1.2** | **high** — two majors; read the changelog, re-test ADR-0060 scheduling and permission flow on device |
| Microsoft.Extensions.Http.Resilience / ServiceDiscovery | 10.6.0 → 10.11.0 | low |
| OpenTelemetry | 1.15.x → 1.20.x | low–medium; check the Prometheus exporter (beta) still composes |
| Aspire.Hosting.PostgreSQL | 13.4.6 → 13.6.2 | low (dev-time only) |
| Asp.Versioning.Http | 10.0.0 → 10.2.3 | low |
| IdentityModel.JsonWebTokens | 8.19.2 → 8.23.0 | low; token tests cover it |
| MailKit, QRCoder | minor/patch | low |
| Test stack: Test.Sdk 17.14 → **18.11**, Playwright 1.55 → 1.63, Testcontainers 4.6 → 4.16, bUnit, ArchUnitNET | — | medium for Test.Sdk major; test-only |
| Nerdbank.GitVersioning | 3.7.115 → 3.10.94 | low–medium; verify version stamping (ADR-0047) unchanged |
| .NET SDK | `global.json` 10.0.302 (rollForward `latestFeature`), local 10.0.401 | pin to the current feature band for reproducible CI |
| Local tools | `dotnet-ef` 10.0.10, `docfx` 2.78.3 | bump `dotnet-ef` with EF Core |
| Container images (`deploy/compose.yaml`) | Grafana 11.4, Loki 3.3, Prometheus 3.1, node-exporter, cAdvisor; **Postgres 17** | observability images: update; **Postgres: stay on 17** (take minor patches only — a major upgrade is a dump/restore, not a pre-launch task) |
| GitHub Actions | `upload-pages-artifact@v3`, `deploy-pages@v4`, `nbgv@v0.5.2`, … | low |

Order: servicing band first (low risk, security) → MudBlazor → MAUI + workloads → Plugin.LocalNotification →
test stack → images. Build (0 warnings), full test suite, browser pass, device pass after each risky step.

### Status (2026-10-20) — applied in the working tree

Everything in the table above is bumped, plus what the bump surfaced:

- **Packages:** all to latest stable; the test stack included the **bUnit 2** (`BunitContext`, `Render`,
  `AddAuthorization`, async disposal — MudBlazor's services are `IAsyncDisposable`-only) and
  **Testcontainers 4.16** (image passed to the builder constructor) migrations. xUnit stays on **v2**
  (2.9.3, the latest v2); v3 is a separate migration, not needed for release.
- **Security:** `dotnet list package --vulnerable --include-transitive` flagged
  `System.Security.Cryptography.Pkcs` 10.0.0 (via MailKit → MimeKit; GHSA-846f-f764-76mx high,
  GHSA-2fmv-rv2c-59x7 moderate) in the production API — pinned to 10.0.13. Scan now clean.
- **Plugin.LocalNotification 14:** request types moved to `Plugin.LocalNotification.Core.Models`;
  `NotifyTime` is now a `DateTimeOffset` (the scheduler passes the absolute instant instead of a local
  wall-clock time). **Needs a device check** — schedule, permission prompt, delivery, cancel.
- **Aspire 13.6:** new ASPIRE010 notice (CLI bundle) silenced in the AppHost — we keep `dotnet run`.
- **Observability images** (compose + AppHost): Loki 3.7.8, Prometheus 3.15.0, **Grafana 13.2.4** (two
  majors; dashboards use only current panel types; the Grafana DB migrates forward on first start — no way
  back to 11 without restoring the volume), node-exporter 1.12.1, cAdvisor 0.60.6 — **moved registry** to
  `ghcr.io/google/cadvisor` (gcr.io stopped at 0.52). Postgres stays `postgres:17` (floating minor).
- **GitHub Actions:** checkout v7, setup-dotnet v6, upload-artifact v7, upload-pages-artifact v5 /
  deploy-pages v5, docker build-push v7 / login v4, paths-filter v4, upload-testflight-build v5; NBGV
  tool 3.10.94.
- **EF Core:** `has-pending-model-changes` is clean for all five DbContexts — no migration from the bump.
- **Verified:** full solution builds (0 errors; only the documented NU1608 AndroidX skew on the MAUI
  head), all 1,100+ unit/component/integration tests green, both E2E suites green (WebAuthn 2/2,
  Secret-Santa 9/9) against the Aspire 13.6 stack, NBGV still stamps `0.1.<height>+<sha>`. Browser pass
  (create → members → event → draw → share/QR → reveal, EN/DE/FR, console clean) on MudBlazor 9.11 —
  findings PR-8…PR-11 in the [backlog](../roadmap/backlog.md), none introduced by the update.
- **SDK:** `global.json` pinned to **10.0.402** (rollForward `latestFeature`); the Docker build image
  `mcr.microsoft.com/dotnet/sdk:10.0` already ships 10.0.402. Full solution build and all non-E2E test
  projects re-run green on it. Workloads are Visual Studio-managed (android 36.1.118, ios/maccatalyst
  26.5, maui-windows 10.0.110) — update them through the VS installer, not `dotnet workload update`.
- **Still open:** the Android device pass (incl. reminders on
  LocalNotification 14), the first iOS build on the new MAUI band, and a production deploy of the new
  observability images (check Grafana dashboards after the 11 → 13 migration).

## Track 4 — Full project review

- **Known open defects first.** From the 2026-10-20 device test: **R1** (linked givers' *My assignments*
  frozen at draw time) — *gone with ADR-0086: views are derived from the one document on read; re-check on the
  Pixel*, **R5**
  (password readable in the Android accessibility tree), plus the open BTP-3…BTP-9 items in the
  [backlog](../roadmap/backlog.md).
- **Code:** layering/architecture tests, dead code, TODOs, error handling paths, `Ui.Shared` test gaps.
- **Security:** `dotnet list package --vulnerable --include-transitive`, auth/token flows, rate limits,
  CSP/headers, secrets handling, operator access (ADR-0075).
- **Database:** confirm production ran the 2026-09-15 migrations ([launch-readiness §D](launch-readiness.md));
  indexes against real query paths; retention jobs; backup retention vs privacy-policy wording.
- **Deployment & operations:** alerting actually reaches the owner (API down, error rate, email bounces,
  disk, backup failure); a written **rollback** procedure for API, web and DB migrations.
- **December capacity:** **Brevo free tier = 300 emails/day** ([ADR-0037](../adr/0037-transactional-email-provider.md))
  — one busy evening of invitations, verifications and resets can exhaust it and silently break onboarding.
  Decide: upgrade before December, or at least alert on quota. Check VPS headroom and rate-limit tuning
  for legitimate bursts.
- **Mobile crash visibility:** how do we learn about crashes in the wild (Play Console vitals, App Store
  Connect crashes, anything more)?
- **Quality passes:** native-speaker read of **FR**; accessibility basics (contrast, screen reader,
  dynamic type; BTP-7 `<html lang>`).

## Track 5 — Store listings & screenshots

Step-by-step: [store submission guide](store-submission-guide.md) (2026-10-28).

- [x] Update `store/google-play/` copy for the features added since 2026-09-17 (accounts & sync, shared
      groups, live reveal links, own wishlist from the link, reminders, events) — 2026-10-28, plus the
      app-content / data-safety answers in its README.
- [ ] New phone + tablet screenshots in DE/EN/FR from the final build.
- [x] Create the equivalent `store/app-store/` package (copy, keywords, privacy label, age rating, review
      notes, TestFlight texts) — 2026-10-28; iPad screenshots 2026-10-30, iPhone screenshots 2026-10-31.
- [ ] "What's new" text per store and language; a version/release-notes routine (ADR-0047).

## Other go-live items

- **Legal:** the light legal review of `/privacy`, `/imprint`, `/delete-account` still owed
  ([launch-readiness §B](launch-readiness.md)).
- **Support:** a support page/contact (needed for Apple's support URL), a short FAQ, and a feedback path for
  the closed-test testers.
