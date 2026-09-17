# Requirements

Functional requirements (**FR**), non-functional requirements (**NFR**), and user stories
(**US**) for CircleTools and its apps. Requirements are testable, traceable statements derived
from [use cases](../use-cases/index.md) and [brainstorming](../brainstorming/index.md).

- New functional requirement → copy [`_template-requirement.md`](_template-requirement.md) into `functional/` (`FR-NNN`).
- New non-functional requirement → copy the same template into `non-functional/` (`NFR-NNN`).
- New user story → copy [`_template-user-story.md`](_template-user-story.md) (`US-NNN`).

## Register

| ID | Title | Type | Priority | Status |
|----|-------|------|----------|--------|
| [FR-001](functional/FR-001-create-secret-santa-group.md) | Create and configure a Secret-Santa group offline | Functional | Must | Implemented |
| [FR-002](functional/FR-002-manage-members-and-wishlists.md) | Manage members and their wishlists | Functional | Must | Implemented |
| [FR-003](functional/FR-003-define-exclusions.md) | Define exclusions between members | Functional | Must | Implemented |
| [FR-004](functional/FR-004-run-fair-draw.md) | Run a fair draw with rules, history, and graceful failure | Functional | Must | Implemented |
| [FR-005](functional/FR-005-generate-reveal-links.md) | Generate self-contained reveal links | Functional | Must | Implemented |
| [FR-006](functional/FR-006-reveal-page-decode.md) | Reveal page decodes a link client-side | Functional | Must | Implemented |
| [NFR-001](non-functional/NFR-001-local-first-offline.md) | Local-first and fully offline (no account or backend) | Non-functional | Must | Implemented |
| [NFR-002](non-functional/NFR-002-assignment-privacy.md) | Assignment privacy — the link is the secret | Non-functional | Must | Implemented |
| [NFR-003](non-functional/NFR-003-multilingual-de-en-fr.md) | Multilingual DE/EN/FR with de-CH default | Non-functional | Must | Implemented |
| [FR-007](functional/FR-007-create-suite-account.md) | Create a suite account with email verification | Functional | Must | Implemented |
| [FR-008](functional/FR-008-sign-in-with-password.md) | Sign in with email and password | Functional | Must | Implemented |
| [FR-009](functional/FR-009-reset-password.md) | Reset a forgotten password | Functional | Must | Implemented |
| [FR-010](functional/FR-010-manage-passkeys.md) | Register and remove passkeys | Functional | Should | Approved |
| [FR-011](functional/FR-011-sign-in-with-passkey.md) | Sign in with a passkey | Functional | Should | Approved |
| [FR-012](functional/FR-012-manage-profile.md) | Manage profile and preferred language | Functional | Must | Implemented |
| [FR-013](functional/FR-013-change-email-address.md) | Change the account email address | Functional | Should | Implemented |
| [FR-014](functional/FR-014-export-personal-data.md) | Export personal data | Functional | Must | Implemented |
| [FR-015](functional/FR-015-delete-account.md) | Delete the account | Functional | Must | Implemented |
| [NFR-004](non-functional/NFR-004-data-protection-and-retention.md) | Data protection & retention | Non-functional | Must | Approved |
| [NFR-005](non-functional/NFR-005-account-enumeration-resistance.md) | Account-enumeration resistance | Non-functional | Must | Implemented |
| [FR-016](functional/FR-016-create-circle.md) | Create a circle with a user-chosen label | Functional | Must | Implemented |
| [FR-017](functional/FR-017-invite-to-circle.md) | Invite a person to a circle by email | Functional | Must | Approved |
| [FR-018](functional/FR-018-respond-to-circle-invitation.md) | Accept or decline a circle invitation | Functional | Must | Implemented |
| [FR-019](functional/FR-019-manage-managed-members.md) | Add and maintain managed members | Functional | Must | Implemented |
| [FR-020](functional/FR-020-manage-circle-roles-and-membership.md) | Manage circle roles, membership and ownership | Functional | Must | Implemented |
| [FR-021](functional/FR-021-leave-circle.md) | Leave a circle | Functional | Must | Implemented |
| [FR-022](functional/FR-022-delete-circle.md) | Delete a circle | Functional | Must | Implemented |
| [NFR-006](non-functional/NFR-006-circle-authorization.md) | Circle data authorization | Non-functional | Must | Implemented |
| [NFR-007](non-functional/NFR-007-store-distributability.md) | Store distributability & platform compliance | Non-functional | Must | Approved |
| [FR-023](functional/FR-023-sync-across-devices.md) | Sync account-scoped data across a user's devices | Functional | Must | Implemented |
| [FR-024](functional/FR-024-offline-editing-and-reconciliation.md) | Offline editing and reconciliation for signed-in users | Functional | Must | Implemented |
| [FR-025](functional/FR-025-claim-local-groups.md) | Claim local groups into the account | Functional | Must | Implemented |
| [FR-026](functional/FR-026-remove-group-from-account.md) | Remove a group from the account | Functional | Should | Implemented |
| [NFR-008](non-functional/NFR-008-sync-convergence-and-data-safety.md) | Sync convergence & data safety | Non-functional | Must | Implemented |
| [FR-027](functional/FR-027-create-group-from-circle.md) | Create a group from a circle | Functional | Must | Implemented |
| [FR-028](functional/FR-028-link-member-to-account.md) | Link a member to an account | Functional | Must | Implemented |
| [FR-029](functional/FR-029-see-my-assignment.md) | See my assignment while signed in | Functional | Must | Implemented |
| [FR-030](functional/FR-030-edit-my-wishlist.md) | Edit my own wishlist | Functional | Should | Implemented |
| [FR-031](functional/FR-031-single-sign-on-across-apps.md) | Start a session from a signed-in sibling app (cross-app SSO) | Functional | Should | Implemented |
| [FR-032](functional/FR-032-add-organizer-as-member.md) | Add the organizer as the first member on group creation | Functional | Should | Implemented |
| [FR-033](functional/FR-033-bookmark-groups.md) | Bookmark groups for quick access (synced across devices) | Functional | Could | Implemented |
| [FR-034](functional/FR-034-multiple-events-per-group.md) | Multiple events (occasions) per group, sharing one roster | Functional | Should | Implemented |
| [FR-035](functional/FR-035-carry-forward-new-occasion.md) | Carry forward wishlists, budget and prior draw when creating an event | Functional | Should | Implemented |
| [FR-036](functional/FR-036-budget-minimum-and-maximum.md) | Gift budget supports an optional minimum as well as a maximum | Functional | Could | Implemented |
| [FR-037](functional/FR-037-member-gift-note.md) | A member has a stable gift note (allergies / dietary / do-not-gift) shown to their giver | Functional | Could | Implemented |
| [FR-038](functional/FR-038-per-group-storage-mode.md) | A group has one of three storage modes, chosen at creation and switchable | Functional | Must | Approved |
| [FR-039](functional/FR-039-shared-by-link-storage-and-sync.md) | A shared-by-link group is stored on the server by share token and synced to link-holders | Functional | Must | Approved |
| [FR-040](functional/FR-040-admin-link-capability.md) | An admin link grants a shared, account-free, revocable admin capability | Functional | Must | Approved |
| [FR-041](functional/FR-041-join-link-and-person-claim.md) | A join link lets a person join a shared group and claim which member they are | Functional | Must | Approved |
| [FR-042](functional/FR-042-release-a-claimed-person.md) | An admin can release a claimed person so they can be claimed again | Functional | Should | Approved |
| [FR-043](functional/FR-043-release-ownership-and-deletion-disposition.md) | Release a group to shared-by-link, and dispose of owned groups on account deletion | Functional | Must | Approved |
| [NFR-009](non-functional/NFR-009-account-free-write-abuse-resistance.md) | Account-free backend writes are abuse-resistant | Non-functional | Must | Approved |
| [NFR-010](non-functional/NFR-010-shared-group-retention-lifecycle.md) | Shared-by-link groups have a defined retention & deletion lifecycle | Non-functional | Must | Approved |
| [FR-044](functional/FR-044-add-event-to-device-calendar.md) | Add an event to the device calendar (pre-filled, all-day, system-UI) | Functional | Could | Implemented |
| [FR-045](functional/FR-045-guided-hand-out-of-reveal-links.md) | Guided hand-out of reveal links (one per recipient, tracks who's done) | Functional | Could | Implemented |
| [FR-046](functional/FR-046-device-local-occasion-reminders.md) | Device-local occasion reminders (draw due / tomorrow / empty wishlist; opt-in, off by default) | Functional | Could | Implemented |
| [FR-047](functional/FR-047-account-deletion-farewell-email.md) | Account-deletion farewell email (one link-less confirmation after successful deletion, DE/EN/FR, source-app branded) | Functional | Should | Implemented |
| [NFR-011](non-functional/NFR-011-local-data-durability-and-portability.md) | Local data is durable and portable on the web (persistence request + backup/restore) | Non-functional | Should | Implemented |

_Add a row per requirement._

## Slices

- **FR-001..006, NFR-001..003** — the Secret-Santa MVP (shipped,
  [FEAT-001](../features/FEAT-001-secret-santa-mvp.md)).
- **FR-007..015, NFR-004..005** — **M1 — Identity in Main**, derived from
  [UC-MAIN-001..007](../use-cases/index.md), built and closed as
  [FEAT-002](../features/FEAT-002-m1-identity.md).
- **FR-016..022, NFR-006** — **M2 — Circles**, derived from
  [UC-MAIN-008..014](../use-cases/index.md). Built and closed as
  [FEAT-003](../features/FEAT-003-m2-circles.md). It also closed **FR-015**, the criterion M1 carried:
  the sole-circle-owner guard finally has circles to guard.
- **FR-023..026, NFR-008** — **M3 — Sync & claim**, derived from
  [UC-MAIN-015..016 and UC-SS-007..008](../use-cases/index.md). Specified 2026-07-30, **built and
  verified 2026-08-12** as [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md) — including the
  two-device, DE/EN/FR browser walkthrough `CLAUDE.md` requires (convergence, data safety, LWW with a
  reported collision, delete-without-resurrection, un-claim-as-marker, and the claim-list singular
  count all confirmed on screen). FR-023 also closes **FR-012**'s carried criterion (changing the
  language offline, pushed on the next sync — confirmed cross-device). The open point before building —
  FR-023 **excludes circle data from sync** — was settled as
  [ADR-0033](../adr/0033-which-aggregates-sync.md), narrowing
  [ADR-0016](../adr/0016-sync-and-conflict-resolution.md).
- **FR-027..030** — **M4 — Linking**, derived from
  [UC-SS-009..012](../use-cases/index.md). This is the slice where the M1–M3 foundations turn into
  product value: create a group from a circle (FR-027), link a member to an account (FR-028), see your
  own assignment in-app (FR-029, the amended NFR-002 in action), and own your wishlist (FR-030).
  FR-027 also **closes the carried Secret-Santa halves of FR-019 and FR-022** — it is the first thing
  that copies a circle into a group, which is exactly the "copy" those two criteria were waiting on.
  M4 needs **no new NFR**: assignment privacy is already governed by the amended
  [NFR-002](non-functional/NFR-002-assignment-privacy.md), and the link request's
  enumeration resistance by [NFR-005](non-functional/NFR-005-account-enumeration-resistance.md). Built
  and closed as [FEAT-005](../features/FEAT-005-m4-linking.md), verified by
  [TP-005](../features/TP-005-m4-linking.md).
- **NFR-008** is the M3 counterpart to NFR-005 and NFR-006: convergence and data safety are one
  property that every syncing aggregate must hold, and both of its failure modes — a device that
  quietly stops converging, an edit that disappears — are silent. Stating it once gives it one test
  approach instead of four requirements each asserting a corner of it.
- **NFR-005** is cross-cutting: it also governs M2 invitations, where the rule originated
  ([ADR-0018](../adr/0018-circle-and-sharing-model.md)) — see
  [FR-017](functional/FR-017-invite-to-circle.md) and
  [FR-018](functional/FR-018-respond-to-circle-invitation.md).
- **NFR-006** is the M2 counterpart: M1's protected data belonged to the caller, while circle data
  belongs to *other people*, so authorization becomes a role matrix that one NFR states once instead
  of seven requirements repeating.
- **NFR-007** belongs to no slice. It states what the App Store and Google Play demand in return for
  distribution, and unlike every other requirement here it is never finally satisfied — both stores
  raise their floor annually. It is written down because the failure mode is silent: nothing breaks
  when a deadline passes, the app simply becomes unshippable the next time an update is needed. Due
  at [milestone 8](../roadmap/roadmap.md); governed by
  [ADR-0030](../adr/0030-mobile-store-publisher-identity.md) and
  [ADR-0031](../adr/0031-ios-build-host.md).

## Reading the statuses

`Implemented` and `Approved` are used deliberately, not interchangeably. A requirement is
`Implemented` only when **every** acceptance criterion is met and covered.

`Approved` covers two different situations, and it is worth knowing which is which:

- **Specified, not yet built** — **NFR-007** (store distributability), which belongs to no slice
  (see above). The M3 slice (**FR-023..026**, **NFR-008**) left this state on 2026-08-12: it is built
  and browser-verified as [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md).
- **Built, one criterion still open** — **FR-010**, **FR-011** and **NFR-004** from M1, and **FR-017**
  from M2. Each is in use, but carries a criterion that cannot close yet, and the reason is recorded
  rather than quietly dropped:

| Requirement | What is still open | Unblocked by |
|---|---|---|
| FR-010 / FR-011 | passkeys across the MAUI heads (needs `apple-app-site-association` / Digital Asset Links on the real domain) | the hosting & domain decision |
| NFR-004 | the data-residency target, and the "no private key, no biometric data" check that needs a stored passkey credential to inspect | the hosting decision; milestone 7 |
| FR-017 | deliverability of the invitation — SPF/DKIM/DMARC, bounces, abuse reports. Everything else is built and tested against Mailpit, but an invitation is the first thing the suite sends to somebody with no relationship to it, and a local sink cannot exercise any of that | the transactional-email provider decision |

**FR-019 and FR-022 closed with M4.** Their one open criterion each — the managed member *copied* into
a group left unchanged by a later circle change, and the group *created from* a circle that is later
deleted opening unchanged with its dangling origin reference never dereferenced — waited on the first
thing to copy a circle into a group, which [FEAT-005](../features/FEAT-005-m4-linking.md) built and
covered with `CreateFromCircleSnapshotTests`. Both are now `Implemented`.

**FR-015 closed with M2.** Its sole-circle-owner guard was M1's recorded loose end and is now
`SoleOwnerCircleGuard`, verified end to end through real account deletion — which is what the mixed
statuses are for: they mark where a slice stops so the next milestone finds the loose ends instead of
re-deriving them. See [FEAT-002](../features/FEAT-002-m1-identity.md)'s and
[FEAT-003](../features/FEAT-003-m2-circles.md)'s coverage tables, and the coverage notes in
[TP-002](../features/TP-002-m1-identity.md) and [TP-003](../features/TP-003-m2-circles.md).
