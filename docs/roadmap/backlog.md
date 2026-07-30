# Backlog

Idea stubs and work items, newest-first. Promote items into
[brainstorming](../brainstorming/index.md) → [use cases](../use-cases/index.md) as they mature.

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
- **Domain & application built (2026-07-29):** `AppSuite.Circles.Contracts` and
  `AppSuite.Circles.Core` exist — the `Circle` aggregate with memberships, invitations and decline
  records inside it, every invariant and cap, and 23 command/query handlers, with **68 unit tests**
  and four architecture fitness functions green (355 tests across 11 projects overall). Four ports
  are declared and unimplemented until the host slice: `ICircleRepository`, `IAccountDirectory`,
  `IInvitationMailer`, `IInvitationRateLimiter`. Two spec corrections were made and recorded in
  [FEAT-003](../features/FEAT-003-m2-circles.md): the "no email field anywhere in Contracts" rule
  became "no DTO describing a **person** carries one", with a two-name exemption list under test
  (an invite request must carry an address, and a pending-invitation list is unusable without one);
  and the invitation mail leaves through an `IInvitationMailer` **port** rather than `Circles.Core`
  referencing `AppSuite.Infrastructure`, which would have pulled MailKit into the domain project.
- **Built and closed (2026-07-30):** the rest of the slice landed — `AppSuite.Circles.Repository`
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
- **Post-MVP (v2 — designed 2026-07-27):** add **central login** in the AppSuite **Main** shell,
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
- **Next:** `UC-SS-007+` use cases (create a group from a circle; link a member; see my assignment
  while logged in; edit my own wishlist) → FRs → the M4 feature spec.

### IDEA-000 — AppSuite shell (Main app-type) · _status: in progress_
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
  two building blocks arc42 ch.5 already planned but that do not exist yet — `AppSuite.Identity`
  (suite-wide, per ADR-0009) and `AppSuite.Infrastructure` (outbox + SMTP sender). arc42 ch.5 §5.3 now
  fixes the four bounded contexts.
- **Test plan written (2026-07-27):** [TP-002](../features/TP-002-m1-identity.md), weighted toward the
  two disproportionate risks — refresh-token rotation (custom code, incl. the racing-refresh case) and
  enumeration resistance (whose failure mode is invisible to ordinary functional tests).
- **Server half built (2026-07-27):** `AppSuite.Identity` and `AppSuite.Infrastructure` exist, and
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
  `IKeyValueStore`; `secretsanta.culture` and `appsuite.secretsanta.groups` are untouched, so no
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
- **Two of M1's three deferred criteria still stand**, and are recorded as deferred in FEAT-002: MAUI
  cross-client passkeys (needs the real domain) and the *offline* language change (needs M3 sync).
  The third — **the sole-circle-owner deletion guard — closed with M2** as `SoleOwnerCircleGuard`,
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
