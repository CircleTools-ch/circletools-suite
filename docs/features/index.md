# Feature Specs & Test Plans

A **feature spec** (`FEAT-NNN`) turns approved use cases and requirements into an implementable
design. Each feature has a paired **test plan** (`TP-NNN`).

- New feature spec → copy [`_template-feature-spec.md`](_template-feature-spec.md).
- New test plan → copy [`_template-test-plan.md`](_template-test-plan.md).

## Register

| ID | Title | Bounded context | Status |
|----|-------|-----------------|--------|
| [FEAT-001](FEAT-001-secret-santa-mvp.md) | Secret-Santa MVP (offline, local-first) | SecretSanta | Done |
| [TP-001](TP-001-secret-santa-mvp.md) | Test plan — Secret-Santa MVP | SecretSanta | Reviewed |
| [FEAT-002](FEAT-002-m1-identity.md) | M1 — Identity in the Main shell | Identity | Done |
| [TP-002](TP-002-m1-identity.md) | Test plan — M1 Identity | Identity | Reviewed |
| [FEAT-003](FEAT-003-m2-circles.md) | M2 — Circles in the Main shell | Circles | Done |
| [TP-003](TP-003-m2-circles.md) | Test plan — M2 Circles | Circles | Reviewed |
| [FEAT-004](FEAT-004-m3-sync-and-claim.md) | M3 — Sync & Claim | Platform | Done |
| [TP-004](TP-004-m3-sync-and-claim.md) | Test plan — M3 Sync & Claim | Platform | Reviewed |
| [FEAT-005](FEAT-005-m4-linking.md) | M4 — Linking | SecretSanta | Superseded by FEAT-027 |
| [TP-005](TP-005-m4-linking.md) | Test plan — M4 Linking | SecretSanta | Reviewed |
| [FEAT-006](FEAT-006-m5-cross-app-sso.md) | M5 — Cross-app single sign-on | Platform | Done |
| [TP-006](TP-006-m5-cross-app-sso.md) | Test plan — M5 Cross-app SSO | Platform | Reviewed |
| [FEAT-007](FEAT-007-events-per-group.md) | Events (occasions) as first-class entities within a group | SecretSanta | Done |
| [TP-007](TP-007-events-per-group.md) | Test plan — Events per group | SecretSanta | Reviewed |
| [FEAT-008](FEAT-008-mobile-native-feel-polish.md) | Mobile native-feel polish pass (ergonomics, navigation, motion, interaction patterns) | SecretSanta | In progress |
| [TP-008](TP-008-mobile-native-feel-polish.md) | Test plan — Mobile native-feel polish | SecretSanta | Draft |
| [FEAT-009](FEAT-009-event-section-hub.md) | Event section hub (mobile-native event navigation) | SecretSanta | In progress |
| [TP-009](TP-009-event-section-hub.md) | Test plan — Event section hub | SecretSanta | Draft |
| [FEAT-010](FEAT-010-carry-forward-new-occasion.md) | Carry forward a new occasion from a previous event | SecretSanta | Done |
| [TP-010](TP-010-carry-forward-new-occasion.md) | Test plan — Carry forward a new occasion | SecretSanta | Reviewed |
| [FEAT-011](FEAT-011-budget-minimum-and-maximum.md) | Gift budget minimum as well as maximum | SecretSanta | Done |
| [TP-011](TP-011-budget-minimum-and-maximum.md) | Test plan — Budget minimum and maximum | SecretSanta | Reviewed |
| [FEAT-012](FEAT-012-member-gift-note.md) | Member gift note (allergies / dietary / do-not-gift) | SecretSanta | Done |
| [TP-012](TP-012-member-gift-note.md) | Test plan — Member gift note | SecretSanta | Reviewed |
| [FEAT-013](FEAT-013-account-free-link-shared-groups.md) | Account-free, link-shared groups (third storage tier) | SecretSanta | Superseded by FEAT-027 |
| [TP-013](TP-013-account-free-link-shared-groups.md) | Test plan — Account-free, link-shared groups | SecretSanta | Approved |
| [FEAT-013 Implementation Plan](FEAT-013-implementation-plan.md) | Build plan — Account-free, link-shared groups (phased) | SecretSanta | Complete |
| [FEAT-014](FEAT-014-add-event-to-calendar.md) | Add an event to the device calendar (one-tap, pre-filled) | SecretSanta | Done |
| [TP-014](TP-014-add-event-to-calendar.md) | Test plan — Add an event to the device calendar | SecretSanta | Reviewed |
| [FEAT-015](FEAT-015-guided-hand-out-of-reveal-links.md) | Guided hand-out of reveal links (one per recipient, tracks progress) | SecretSanta | Done |
| [TP-015](TP-015-guided-hand-out-of-reveal-links.md) | Test plan — Guided hand-out of reveal links | SecretSanta | Reviewed |
| [FEAT-016](FEAT-016-device-local-occasion-reminders.md) | Device-local occasion reminders (opt-in local notifications) | SecretSanta | Done |
| [TP-016](TP-016-device-local-occasion-reminders.md) | Test plan — Device-local occasion reminders | SecretSanta | Reviewed |
| [FEAT-017](FEAT-017-local-data-transparency-and-backup.md) | Local data transparency, durability & backup (web) | SecretSanta | Done |
| [TP-017](TP-017-local-data-transparency-and-backup.md) | Test plan — Local data transparency & backup | SecretSanta | Reviewed |
| [FEAT-018](FEAT-018-branded-transactional-emails.md) | Branded HTML transactional emails (per-app co-branding, source-app link origin, farewell mail) | Main | In progress |
| [FEAT-019](FEAT-019-getting-started-checklist.md) | Getting-started checklist (first-group onboarding: guided empty→drawn sequence on the hub, self-dismissing, skippable) | SecretSanta | Done |
| [FEAT-020](FEAT-020-live-reveal-links-for-backend-backed-groups.md) | Live reveal links for backend-backed groups (client-published opaque per-giver slice served by reveal token; reveal page shows the current wishlist/event online, falls back to the embedded snapshot offline; local-only unchanged; eventual organizer-published freshness — ADR-0068) | SecretSanta | Superseded by FEAT-027 |
| [TP-018](TP-018-branded-transactional-emails.md) | Test plan — Branded transactional emails | Main | Draft |
| [FEAT-021](FEAT-021-storage-default-onboarding-and-list-indicator.md) | Storage default, first-run onboarding & group-list storage indicator (device-local default seeds every create path incl. the phone FAB; one-time skippable onboarding; per-group storage icon — ADR-0071, FR-048) | SecretSanta | In progress |
| [TP-021](TP-021-storage-default-onboarding-and-list-indicator.md) | Test plan — Storage default, onboarding & list indicator | SecretSanta | Draft |
| [FEAT-022](FEAT-022-business-metrics-dashboard.md) | Business-metrics dashboard (PII-free counts of accounts/groups/events/draws over time on the existing Grafana stack — ADR-0051) | Platform | In progress |
| [TP-022](TP-022-business-metrics-dashboard.md) | Test plan — Business-metrics dashboard | Platform | Draft |
| [FEAT-023](FEAT-023-session-scope-and-change-password.md) | Session scope and change password (sign out ends this device only; "Sign out on all devices"; change password ends the other sessions — ADR-0079) | Identity | In progress |
| [TP-023](TP-023-session-scope-and-change-password.md) | Test plan — Session scope and change password | Identity | Draft |
| [FEAT-024](FEAT-024-participants-in-shared-groups.md) | Participants in shared groups (own pairing only, own entry editable, leave group, organizer-only linking publish — ADR-0080) | SecretSanta + Main | Superseded by FEAT-027 |
| [TP-024](TP-024-participants-in-shared-groups.md) | Test plan — Participants in shared groups | SecretSanta + Main | Approved |
| [FEAT-025](FEAT-025-storage-model-and-onboarding.md) | Storage model & onboarding (owned shared groups, create-time storage choice, named import prompt — ADR-0081) | SecretSanta + Main | Done |
| [TP-025](TP-025-storage-model-and-onboarding.md) | Test plan — Storage model & onboarding | SecretSanta + Main | Approved |
| [FEAT-026](FEAT-026-own-entry-from-the-reveal-link.md) | Own wishlist and gift note from the reveal link (live-link groups; organizer's copy read-only — ADR-0084) | SecretSanta + Main | Done |
| [TP-026](TP-026-own-entry-from-the-reveal-link.md) | Test plan — Own entry from the reveal link | SecretSanta + Main | Reviewed |
| [FEAT-027](FEAT-027-one-way-to-be-in-a-group.md) | One way to be in a group — online group store, participants for every route, derived views (ADR-0086) | SecretSanta + Main | In progress |
| [TP-027](TP-027-one-way-to-be-in-a-group.md) | Test plan — One way to be in a group | SecretSanta + Main | Draft |

**M1 is closed, not shipped.** FEAT-002 is `Done` in the sense the process means it — the slice is
built, tested and documented. It cannot reach production until the transactional-email provider and
the hosting/suite-domain decisions land; both are open items in the
[ADR backlog](../adr/index.md#open-decisions-backlog). Two verification criteria were carried to
milestone 7 rather than ticked — see [the backlog](../roadmap/backlog.md) and
[arc42 ch. 11](../arc42/11-risks-and-technical-debt.md).

**M2 is closed on the same terms, and is blocked on one of the same two decisions.** FEAT-003 is
`Done`: the `Circles` context is built, the role matrix and the two NFR-006 fitness functions are
green, and FR-015's sole-owner guard — the criterion M1 carried — is closed. Shipping still waits on
the **transactional-email provider**, which invitations sharpen from account-critical to
onboarding-critical: an invitation that does not arrive is a person who never joins. Three criteria are
carried rather than ticked, two of them to M4 — see the
[requirements register](../requirements/index.md#reading-the-statuses).

The spec is also where the seven circle requirements' deliberately open numbers were decided
(invitation lifetime, the three rate limits, the decline cooldown, the caps) and where four product
questions they left are answered; the reasoning for each sits in the spec's *numbers* table, and the
corrections implementation forced on it are marked in place rather than rewritten away.

**What closing M2 cost, as a warning for M3.** Reconciling TP-003 against the code found four things
missing that a green suite had nothing to say about: the NFR-006 fitness function whose mechanism was
built but never read back, a "role matrix" that was three spot checks, two acceptance criteria unmet in
the **product** (both destructive confirmations), and an invitation-revoke path with no functional test.
[TP-003's closing notes](TP-003-m2-circles.md#what-the-reconciliation-found-2026-07-30) record how each
stayed invisible; the short version is that a test-plan row is not evidence.

**M3 is built and closed (2026-08-12).** [FEAT-004](FEAT-004-m3-sync-and-claim.md) is `Done` and
[TP-004](TP-004-m3-sync-and-claim.md) `Reviewed`: the sync-and-claim slice is built to the fixed design
(the wire format, the cursor, tombstone/marker retention, claim idempotency, and the `Group`-only sync
catalogue from [ADR-0033](../adr/0033-which-aggregates-sync.md)), the two-client harness is green, and
the verification `CLAUDE.md` insists on — a **two-device, DE/EN/FR browser walkthrough against a live
Main service** — was completed: convergence (members, wishlists, exclusions and the draw reach the
second device; the unclaimed group does not), data safety (no account action drops a local group), LWW
(one survivor **and** a reported collision on a genuine conflict, none on replays), delete without
resurrection, un-claim as a marker not a tombstone, FR-012's cross-device language change, and the
claim-list **singular count** in all three languages. TP-004 was deliberately weighted toward NFR-008's
two silent-failure properties — convergence and data safety — and centred on the two-client harness,
because M2's close showed that is exactly where a green suite says nothing.

Closing M3 turned up no product defects; two behaviour questions were noted for a later decision (first
sign-in surfaces the claim screen via a nav link rather than an automatic prompt; the signed-in nav
language picker is local-only and is reverted to the profile preference on the next reconcile). As with
M1 and M2, **"CI green" is carried to milestone 7** — no pipeline exists yet — and *shipping* still
waits on the transactional-email and hosting/residency decisions, though neither gates the build
([ADR-0023](../adr/0023-local-dev-infrastructure-in-apphost.md)).

**M4 is built and closed (2026-08-12).** [FEAT-005](FEAT-005-m4-linking.md) is `Done` and its paired
[TP-005](TP-005-m4-linking.md) `Reviewed`: the linking slice — create a group from a circle, link and
unlink a member with consent, see my own assignment while signed in, edit my own wishlist — realises
FR-027..030 under the already-accepted M4 ADRs ([ADR-0018](../adr/0018-circle-and-sharing-model.md),
[ADR-0019](../adr/0019-member-user-linking.md), [ADR-0020](../adr/0020-server-side-assignment-privacy.md)),
so Phase 4 was a no-op here. M4 is the **first cross-account milestone**: everything through M3 was
single-account (a user's own devices), whereas linking is the first time a *second* account — a linked
member — reads and writes one slice of an organizer-owned group. The spec fixed the load-bearing new
piece for that: a **structured, per-caller-authorized surface** beside M3's opaque single-account sync,
the surface ADR-0020 sanctioned, realised as **403 for a stranger vs 404 for a known member with no
draw**. TP-005 was deliberately weighted toward **assignment authorization** — a cross-member read must
be **denied, not filtered**, and a non-organizer response capped at one — for the same reason M3's plan
centred on convergence and M2's on enumeration resistance: it is the sharpest silent-failure surface in
the suite, where one wrong rule leaks the exact secret the app exists to keep. The slice also closed the
carried Secret-Santa halves of **FR-019** and **FR-022** (the first thing to copy a circle into a group),
and settled the numbers the requirements deferred (reuse the M2 invitation machinery for the member-link
request; self-link auto-accepts; one user per group; per-group wishlists with a copy-previous offer;
snapshot-at-create timing) in its *numbers* table.

**Closing M4 turned up one product defect a green suite had missed** — create-from-circle set
`Member.UserId` locally but never registered the **server-side** member-link, so a person linked that way
could see their assignment but could not own their wishlist. It was found only by the two-account,
DE/EN/FR browser walkthrough `CLAUDE.md` requires, and fixed with `POST /api/v1/member-links/circle` (the
host verifies shared-circle membership before recording the link), now covered by `LinkingCircleTests`.
The transferable lesson, recorded: every test set up its links through the accept flow, so the one path
that did not was the one that broke — and only the browser showed it.
