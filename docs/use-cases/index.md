# Use Cases

Use cases describe how actors achieve goals with the system. They are the bridge from
[brainstorming](../brainstorming/index.md) to [requirements](../requirements/index.md) and
[feature specs](../features/index.md).

- New use case → copy [`_template-use-case.md`](_template-use-case.md) into the app's subfolder
  (e.g. `secret-santa/`) and name it `UC-<APP>-NNN-title.md`.

## Register

| ID | Title | App | Status |
|----|-------|-----|--------|
| [UC-SS-001](secret-santa/UC-SS-001-create-group.md) | Create and configure a Secret-Santa group | Secret-Santa | Draft |
| [UC-SS-002](secret-santa/UC-SS-002-manage-members.md) | Manage members and their wishlists | Secret-Santa | Draft |
| [UC-SS-003](secret-santa/UC-SS-003-define-exclusions.md) | Define exclusions between members | Secret-Santa | Draft |
| [UC-SS-004](secret-santa/UC-SS-004-run-draw.md) | Run the fair draw | Secret-Santa | Draft |
| [UC-SS-005](secret-santa/UC-SS-005-share-reveal-links.md) | Generate and share reveal links | Secret-Santa | Draft |
| [UC-SS-006](secret-santa/UC-SS-006-open-reveal-link.md) | Open a reveal link | Secret-Santa | Draft |
| [UC-MAIN-001](main/UC-MAIN-001-create-account.md) | Create a suite account | Main | Draft |
| [UC-MAIN-002](main/UC-MAIN-002-sign-in-with-password.md) | Sign in with email and password | Main | Draft |
| [UC-MAIN-003](main/UC-MAIN-003-register-passkey.md) | Register a passkey | Main | Draft |
| [UC-MAIN-004](main/UC-MAIN-004-sign-in-with-passkey.md) | Sign in with a passkey | Main | Draft |
| [UC-MAIN-005](main/UC-MAIN-005-manage-profile.md) | Manage profile and preferred language | Main | Draft |
| [UC-MAIN-006](main/UC-MAIN-006-delete-account-and-export-data.md) | Delete the account and export personal data | Main | Draft |
| [UC-MAIN-007](main/UC-MAIN-007-reset-password.md) | Reset a forgotten password | Main | Draft |

_Add a row per use case._

## Slices

- **UC-SS-001..006** — the Secret-Santa MVP (shipped, [FEAT-001](../features/FEAT-001-secret-santa-mvp.md)).
- **UC-MAIN-001..007** — **M1 — Identity in Main**: sign-up, sign-in, passkeys, profile, deletion &
  export, password reset. Input for the M1 requirements and feature spec.
- Still to write: `UC-MAIN-008+` for **M2 — Circle** (create a circle, invite a new person, invite an
  existing account, accept/decline, leave/remove) and `UC-SS-007+` for **M3/M4** (claim a group,
  create a group from a circle, link a member, see my assignment while signed in).
