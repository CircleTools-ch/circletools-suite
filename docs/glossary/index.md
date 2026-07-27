# Glossary — Ubiquitous Language

This glossary is the **single source of truth** for domain terms. Names in code (aggregates,
entities, properties, DB tables, API DTOs) **must** match the canonical English term here. If a
term is missing, add it (copy [`_template-glossary-entry.md`](_template-glossary-entry.md))
before using it in code.

Terms may carry different meanings in different [bounded contexts](../arc42/05-building-block-view.md);
note that nuance in the entry. Product display translations (DE/EN/FR) are listed for reference
only — they are **not** code names.

## Terms

| Term | Context | Definition |
|------|---------|------------|
| [Assignment](assignment.md) | Secret-Santa | A single giver→recipient pairing produced by a draw. |
| [Budget](budget.md) | Secret-Santa | The agreed spending limit for gifts in a group. |
| [Circle](circle.md) | Circles | A persistent named set of people sharing apps and data; UI label is user-chosen. |
| [Circle membership](circle-membership.md) | Circles | A person's link to a circle, carrying their role (owner/admin/member). |
| [Draw](draw.md) | Secret-Santa | The act/result of randomly assigning gift-givers to recipients under the draw rules. |
| [Event details](event-details.md) | Secret-Santa | A group's occasion info: date, location, notes. |
| [Exclusion](exclusion.md) | Secret-Santa | A one-directional rule preventing a giver from being matched to a recipient. |
| [Group](group.md) | Secret-Santa | A set of participants who exchange gifts together. |
| [Invitation](invitation.md) | Circles | A pending offer to join a circle, sent to an email address. |
| [Managed member](managed-member.md) | Circles | A person in a circle with a profile but no account, maintained by an admin. |
| [Member](member.md) | Secret-Santa | A person participating in a group; optionally linked to a user. |
| [Reveal link](reveal-link.md) | Secret-Santa | A self-contained encoded link that reveals one giver's assignment client-side. |
| [Sync](sync.md) | Platform | Local-first reconciliation of on-device data with the server for a signed-in user. |
| [User](user.md) | Identity | A person with an authenticated account, shared across all apps in the suite. |
| [Wishlist](wishlist.md) | Secret-Santa | A member's optional list of gift ideas. |

_Keep this table sorted alphabetically as terms are added._
