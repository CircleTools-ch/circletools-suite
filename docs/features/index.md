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
| [FEAT-005](FEAT-005-m4-linking.md) | M4 — Linking | SecretSanta | Done |
| [TP-005](TP-005-m4-linking.md) | Test plan — M4 Linking | SecretSanta | Reviewed |
| [FEAT-006](FEAT-006-m5-cross-app-sso.md) | M5 — Cross-app single sign-on | Platform | In progress |
| [TP-006](TP-006-m5-cross-app-sso.md) | Test plan — M5 Cross-app SSO | Platform | Draft |

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
