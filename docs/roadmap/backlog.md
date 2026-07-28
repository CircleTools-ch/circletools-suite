# Backlog

Idea stubs and work items, newest-first. Promote items into
[brainstorming](../brainstorming/index.md) → [use cases](../use-cases/index.md) as they mature.

## Ideas

### IDEA-002 — Circle (shared people across the suite) · _status: designed_
- **Problem:** every app re-asks who your people are. A family or friend-group has no lasting
  existence in the suite — each Secret-Santa group is typed in from scratch, and nobody but the
  organizer can contribute.
- **Target users:** families and friend-groups who use more than one app, or the same app repeatedly.
- **Value:** one standing set of people, reusable across apps and years. A Secret-Santa group can be
  created **from** a circle instead of retyped; linked people see their own assignment on their own
  device and keep their own [wishlist](../glossary/wishlist.md).
- **Scope (designed, not built):** create a circle and label it; roles (owner/admin/member);
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
- **Next:** the `UC-MAIN-007+` use cases (create a circle, invite a new person, invite an existing
  account, accept/decline, leave/remove) → FRs → the M2 feature spec. Ships as **M2**, after the
  M1 identity slice whose use cases are now written.

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
- **Next:** the browser walkthrough `CLAUDE.md` requires — the full journey in DE/EN/FR with a clean
  console and zero CSP violations, and the first end-to-end run of the WebAuthn ceremonies, which no
  automated test covers. Then the Secret-Santa culture migration onto the shared blocks (ADR-0025's
  recorded follow-up).
- **Three acceptance criteria cannot close in M1** and are recorded as deferred in FEAT-002: MAUI
  cross-client passkeys (needs the real domain), the *offline* language change (needs M3 sync), and
  the sole-circle-owner deletion guard (needs M2 circles).
- **Blocked on deployment ADRs — M1 cannot ship without them:**
  - **Transactional email provider.** FR-007 (verification), FR-009 (reset) and FR-013 (email change)
    all depend on delivery. The ADR register currently justifies deferring this behind M2 invitations;
    it is in fact an **M1** dependency.
  - **Hosting / suite domain.** Needed for the passkey RP-ID (FR-010, FR-011), the CSP directive set
    (ADR-0021), and the NFR-004 residency target. The *strategy* for each is decided — only the
    literal domain is missing. See [naming](../process/naming.md).

## Later / parked
- Additional apps (to be brainstormed).
- **Invite by link or short code** — parked; revisit if email delivery proves to be the friction
  point (see [Invitation](../glossary/invitation.md)).
- **Per-member envelope encryption of assignments** — the P3 option from D1, parked as future
  hardening if the operator-as-trusted-party assumption stops being acceptable.
