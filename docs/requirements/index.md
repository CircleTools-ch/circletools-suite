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

_Add a row per requirement._
