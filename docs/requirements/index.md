# Requirements

Functional requirements (**FR**), non-functional requirements (**NFR**), and user stories
(**US**) for AppSuite and its apps. Requirements are testable, traceable statements derived
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
| [FR-012](functional/FR-012-manage-profile.md) | Manage profile and preferred language | Functional | Must | Approved |
| [FR-013](functional/FR-013-change-email-address.md) | Change the account email address | Functional | Should | Implemented |
| [FR-014](functional/FR-014-export-personal-data.md) | Export personal data | Functional | Must | Implemented |
| [FR-015](functional/FR-015-delete-account.md) | Delete the account | Functional | Must | Approved |
| [NFR-004](non-functional/NFR-004-data-protection-and-retention.md) | Data protection & retention | Non-functional | Must | Approved |
| [NFR-005](non-functional/NFR-005-account-enumeration-resistance.md) | Account-enumeration resistance | Non-functional | Must | Implemented |

_Add a row per requirement._

## Slices

- **FR-001..006, NFR-001..003** — the Secret-Santa MVP (shipped,
  [FEAT-001](../features/FEAT-001-secret-santa-mvp.md)).
- **FR-007..015, NFR-004..005** — **M1 — Identity in Main**, derived from
  [UC-MAIN-001..007](../use-cases/index.md), built and closed as
  [FEAT-002](../features/FEAT-002-m1-identity.md).
- **NFR-005** is cross-cutting: it also governs M2 invitations, where the rule originated
  ([ADR-0018](../adr/0018-circle-and-sharing-model.md)).

## Reading the statuses

`Implemented` and `Approved` are used deliberately, not interchangeably. A requirement is
`Implemented` only when **every** acceptance criterion is met and covered. The five that sit at
`Approved` — **FR-010**, **FR-011**, **FR-012**, **FR-015** and **NFR-004** — are built and in use,
but each still carries one criterion that cannot close yet, and the reason is recorded rather than
quietly dropped:

| Requirement | What is still open | Unblocked by |
|---|---|---|
| FR-010 / FR-011 | passkeys across the MAUI heads (needs `apple-app-site-association` / Digital Asset Links on the real domain) | the hosting & domain decision |
| FR-012 | changing the language *offline*, pushed on the next sync | M3 — sync |
| FR-015 | the sole-circle-owner guard (`IAccountDeletionGuard` exists with nothing to guard) | M2 — circles |
| NFR-004 | the data-residency target, and the "no private key, no biometric data" check that needs a stored passkey credential to inspect | the hosting decision; milestone 7 |

The mixed statuses are the point: they mark exactly where the M1 slice stops, so a later milestone
can find the loose ends instead of re-deriving them. See
[FEAT-002](../features/FEAT-002-m1-identity.md)'s coverage table and
[TP-002](../features/TP-002-m1-identity.md)'s coverage notes.
