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
| [FR-001](functional/FR-001-create-secret-santa-group.md) | Create and configure a Secret-Santa group offline | Functional | Must | Draft |
| [FR-002](functional/FR-002-manage-members-and-wishlists.md) | Manage members and their wishlists | Functional | Must | Draft |
| [FR-003](functional/FR-003-define-exclusions.md) | Define exclusions between members | Functional | Must | Draft |
| [FR-004](functional/FR-004-run-fair-draw.md) | Run a fair draw with rules, history, and graceful failure | Functional | Must | Draft |
| [FR-005](functional/FR-005-generate-reveal-links.md) | Generate self-contained reveal links | Functional | Must | Draft |
| [FR-006](functional/FR-006-reveal-page-decode.md) | Reveal page decodes a link client-side | Functional | Must | Draft |
| [NFR-001](non-functional/NFR-001-local-first-offline.md) | Local-first and fully offline (no account or backend) | Non-functional | Must | Draft |
| [NFR-002](non-functional/NFR-002-assignment-privacy.md) | Assignment privacy — the link is the secret | Non-functional | Must | Draft |
| [NFR-003](non-functional/NFR-003-multilingual-de-en-fr.md) | Multilingual DE/EN/FR with de-CH default | Non-functional | Must | Draft |
| [FR-007](functional/FR-007-create-suite-account.md) | Create a suite account with email verification | Functional | Must | Draft |
| [FR-008](functional/FR-008-sign-in-with-password.md) | Sign in with email and password | Functional | Must | Draft |
| [FR-009](functional/FR-009-reset-password.md) | Reset a forgotten password | Functional | Must | Draft |
| [FR-010](functional/FR-010-manage-passkeys.md) | Register and remove passkeys | Functional | Should | Draft |
| [FR-011](functional/FR-011-sign-in-with-passkey.md) | Sign in with a passkey | Functional | Should | Draft |
| [FR-012](functional/FR-012-manage-profile.md) | Manage profile and preferred language | Functional | Must | Draft |
| [FR-013](functional/FR-013-change-email-address.md) | Change the account email address | Functional | Should | Draft |
| [FR-014](functional/FR-014-export-personal-data.md) | Export personal data | Functional | Must | Draft |
| [FR-015](functional/FR-015-delete-account.md) | Delete the account | Functional | Must | Draft |
| [NFR-004](non-functional/NFR-004-data-protection-and-retention.md) | Data protection & retention | Non-functional | Must | Draft |
| [NFR-005](non-functional/NFR-005-account-enumeration-resistance.md) | Account-enumeration resistance | Non-functional | Must | Draft |

_Add a row per requirement._

## Slices

- **FR-001..006, NFR-001..003** — the Secret-Santa MVP (shipped,
  [FEAT-001](../features/FEAT-001-secret-santa-mvp.md)).
- **FR-007..015, NFR-004..005** — **M1 — Identity in Main**, derived from
  [UC-MAIN-001..007](../use-cases/index.md). Input for the M1 feature spec.
- **NFR-005** is cross-cutting: it also governs M2 invitations, where the rule originated
  ([ADR-0018](../adr/0018-circle-and-sharing-model.md)).
