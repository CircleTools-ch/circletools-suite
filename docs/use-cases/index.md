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

_Add a row per use case._

## Slices

- **UC-SS-001..006** — the Secret-Santa MVP (shipped, [FEAT-001](../features/FEAT-001-secret-santa-mvp.md)).
- **UC-MAIN-001..007** — **M1 — Identity in Main**: sign-up, sign-in, passkeys, profile, deletion &
  export, password reset. Built and closed as [FEAT-002](../features/FEAT-002-m1-identity.md).
- **UC-MAIN-008..014** — **M2 — Circles**: create a circle, invite, accept/decline, managed members,
  roles & ownership, leave, delete. Input for [FR-016..022 and NFR-006](../requirements/index.md) and
  the M2 feature spec. Two of them close loose ends M1 left: UC-MAIN-014 unblocks
  [FR-015](../requirements/functional/FR-015-delete-account.md)'s sole-circle-owner guard, and
  UC-MAIN-011 answers what happens to managed members when their creator leaves.
- Still to write: `UC-SS-007+` for **M3/M4** (claim a group, create a group from a circle, link a
  member, see my assignment while signed in).
