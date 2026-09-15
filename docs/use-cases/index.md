# Use Cases

Use cases describe how actors achieve goals with the system. They are the bridge from
[brainstorming](../brainstorming/index.md) to [requirements](../requirements/index.md) and
[feature specs](../features/index.md).

- New use case → copy [`_template-use-case.md`](_template-use-case.md) into the app's subfolder
  (e.g. `secret-santa/`) and name it `UC-<APP>-NNN-title.md`.

## Register

| ID | Title | App | Status |
|----|-------|-----|--------|
| [UC-SS-001](secret-santa/UC-SS-001-create-group.md) | Create and configure a Secret-Santa group | Secret-Santa | Approved |
| [UC-SS-002](secret-santa/UC-SS-002-manage-members.md) | Manage members and their wishlists | Secret-Santa | Approved |
| [UC-SS-003](secret-santa/UC-SS-003-define-exclusions.md) | Define exclusions between members | Secret-Santa | Approved |
| [UC-SS-004](secret-santa/UC-SS-004-run-draw.md) | Run the fair draw | Secret-Santa | Approved |
| [UC-SS-005](secret-santa/UC-SS-005-share-reveal-links.md) | Generate and share reveal links | Secret-Santa | Approved |
| [UC-SS-006](secret-santa/UC-SS-006-open-reveal-link.md) | Open a reveal link | Secret-Santa | Approved |
| [UC-MAIN-001](main/UC-MAIN-001-create-account.md) | Create a suite account | Main | Approved |
| [UC-MAIN-002](main/UC-MAIN-002-sign-in-with-password.md) | Sign in with email and password | Main | Approved |
| [UC-MAIN-003](main/UC-MAIN-003-register-passkey.md) | Register a passkey | Main | Approved |
| [UC-MAIN-004](main/UC-MAIN-004-sign-in-with-passkey.md) | Sign in with a passkey | Main | Approved |
| [UC-MAIN-005](main/UC-MAIN-005-manage-profile.md) | Manage profile and preferred language | Main | Approved |
| [UC-MAIN-006](main/UC-MAIN-006-delete-account-and-export-data.md) | Delete the account and export personal data | Main | Approved |
| [UC-MAIN-007](main/UC-MAIN-007-reset-password.md) | Reset a forgotten password | Main | Approved |
| [UC-MAIN-008](main/UC-MAIN-008-create-circle.md) | Create a circle | Main | Approved |
| [UC-MAIN-009](main/UC-MAIN-009-invite-to-circle.md) | Invite a person to a circle | Main | Approved |
| [UC-MAIN-010](main/UC-MAIN-010-respond-to-invitation.md) | Accept or decline a circle invitation | Main | Approved |
| [UC-MAIN-011](main/UC-MAIN-011-manage-managed-members.md) | Add and maintain managed members | Main | Approved |
| [UC-MAIN-012](main/UC-MAIN-012-manage-circle-people.md) | Manage a circle's people, roles and ownership | Main | Approved |
| [UC-MAIN-013](main/UC-MAIN-013-leave-circle.md) | Leave a circle | Main | Approved |
| [UC-MAIN-014](main/UC-MAIN-014-delete-circle.md) | Delete a circle | Main | Approved |
| [UC-MAIN-015](main/UC-MAIN-015-sync-across-devices.md) | See my data on every device I sign in on | Main | Approved |
| [UC-MAIN-016](main/UC-MAIN-016-work-offline-and-reconcile.md) | Work offline while signed in, and reconcile when back online | Main | Approved |
| [UC-SS-007](secret-santa/UC-SS-007-claim-local-groups.md) | Take my existing groups into my account | Secret-Santa | Approved |
| [UC-SS-008](secret-santa/UC-SS-008-remove-group-from-account.md) | Take a group back out of my account | Secret-Santa | Approved |
| [UC-SS-009](secret-santa/UC-SS-009-create-group-from-circle.md) | Create a group from a circle | Secret-Santa | Approved |
| [UC-SS-010](secret-santa/UC-SS-010-link-member-to-account.md) | Link a member to an account | Secret-Santa | Approved |
| [UC-SS-011](secret-santa/UC-SS-011-see-my-assignment.md) | See my assignment while signed in | Secret-Santa | Approved |
| [UC-SS-012](secret-santa/UC-SS-012-edit-my-wishlist.md) | Edit my own wishlist | Secret-Santa | Approved |
| [UC-SS-013](secret-santa/UC-SS-013-manage-events.md) | Manage multiple events (occasions) in a group | Secret-Santa | Approved |
| [UC-MAIN-017](main/UC-MAIN-017-single-sign-on-across-apps.md) | Stay signed in when moving between suite apps | Main | Approved |
| [UC-SS-014](secret-santa/UC-SS-014-carry-forward-new-occasion.md) | Create a new occasion carrying forward from a previous one | Secret-Santa | Draft |
| [UC-SS-015](secret-santa/UC-SS-015-budget-minimum-and-maximum.md) | Set a minimum as well as a maximum gift budget | Secret-Santa | Reviewed |
| [UC-SS-016](secret-santa/UC-SS-016-member-gift-note.md) | Give a member a gift note (allergies, dietary, do-not-gift) their giver sees | Secret-Santa | Reviewed |
| [UC-SS-017](secret-santa/UC-SS-017-choose-and-switch-storage-mode.md) | Choose a group's storage mode at creation and switch it later | Secret-Santa | Approved |
| [UC-SS-018](secret-santa/UC-SS-018-share-and-manage-as-admin.md) | Share and manage a shared-by-link group as an admin | Secret-Santa | Approved |
| [UC-SS-019](secret-santa/UC-SS-019-join-a-shared-group.md) | Join a shared-by-link group via the join link | Secret-Santa | Approved |
| [UC-SS-020](secret-santa/UC-SS-020-release-a-claimed-person.md) | Free (release) a claimed person so they can rejoin | Secret-Santa | Approved |
| [UC-SS-021](secret-santa/UC-SS-021-add-occasion-to-calendar.md) | Add an occasion to my phone calendar | Secret-Santa | Draft |

_Add a row per use case._

## Slices

- **UC-SS-001..006** — the Secret-Santa MVP (shipped, [FEAT-001](../features/FEAT-001-secret-santa-mvp.md)).
- **UC-MAIN-001..007** — **M1 — Identity in Main**: sign-up, sign-in, passkeys, profile, deletion &
  export, password reset. Built and closed as [FEAT-002](../features/FEAT-002-m1-identity.md).
- **UC-MAIN-008..014** — **M2 — Circles**: create a circle, invite, accept/decline, managed members,
  roles & ownership, leave, delete. Realised by [FR-016..022 and NFR-006](../requirements/index.md);
  built and closed as [FEAT-003](../features/FEAT-003-m2-circles.md). Two of them close loose ends M1
  left: UC-MAIN-014 unblocks
  [FR-015](../requirements/functional/FR-015-delete-account.md)'s sole-circle-owner guard, and
  UC-MAIN-011 answers what happens to managed members when their creator leaves.
- **UC-MAIN-015..016 + UC-SS-007..008** — **M3 — Sync & claim**: sync across devices, offline editing
  and reconciliation, taking local groups into the account, and taking one back out. Realised by
  [FR-023..026 and NFR-008](../requirements/index.md). Two of them close things earlier milestones
  left open: UC-MAIN-015 carries [FR-012](../requirements/functional/FR-012-manage-profile.md)'s
  offline language change, and UC-SS-007 is where
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)'s amended account-mode half
  first becomes visible to a user — the claim screen is the moment a completed
  [Draw](../glossary/draw.md) leaves the device.
- **UC-SS-009..012** — **M4 — Linking**: create a group from a circle, link a member to an account,
  see my assignment while signed in, edit my own wishlist. This is the slice where the M1–M3
  foundations (accounts, circles, sync) turn into product value: a linked member sees their own
  [Assignment](../glossary/assignment.md) in-app ([UC-SS-011](secret-santa/UC-SS-011-see-my-assignment.md),
  the amended [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) in action) and
  owns their own [Wishlist](../glossary/wishlist.md). Two of them close the Secret-Santa halves M2
  carried: [UC-SS-009](secret-santa/UC-SS-009-create-group-from-circle.md) is where a
  [Circle](../glossary/circle.md) is first copied into a
  [Group](../glossary/group.md), which is the missing "copy" behind
  [FR-019](../requirements/functional/FR-019-manage-managed-members.md)'s and
  [FR-022](../requirements/functional/FR-022-delete-circle.md)'s open criteria. Realised by
  [FR-027..030](../requirements/index.md) and [FEAT-005](../features/FEAT-005-m4-linking.md); built and
  closed 2026-08-12. *(M3 took `UC-SS-007..008`, so M4 starts at 009.)*
