# Backlog

Idea stubs and work items, newest-first. Promote items into
[brainstorming](../brainstorming/index.md) → [use cases](../use-cases/index.md) as they mature.

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
