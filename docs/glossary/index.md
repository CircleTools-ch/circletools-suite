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
| [Admin link](admin-link.md) | Secret-Santa | A capability link granting the admin role in a shared-by-link group; account-free, multi-holder, rotatable. |
| [Assignment](assignment.md) | Secret-Santa | A single giver→recipient pairing produced by a draw. |
| [Budget](budget.md) | Secret-Santa | The agreed spending limit for gifts in a group. |
| [Carry-forward](carry-forward.md) | Secret-Santa | Creating a new event by copying wishlists, budget and the prior draw (as the no-repeat baseline) from an existing event. |
| [Circle](circle.md) | Circles | A persistent named set of people sharing apps and data; UI label is user-chosen. |
| [Circle membership](circle-membership.md) | Circles | A person's link to a circle, carrying their role (owner/admin/member). |
| [Claim](claim.md) | Platform · Secret-Santa | Taking account-free local data into a user's account, per aggregate and by explicit choice. |
| [Draw](draw.md) | Secret-Santa | The act/result of randomly assigning gift-givers to recipients under the draw rules, per event. |
| [Event](event.md) | Secret-Santa | One gift-exchange occasion within a group, with its own draw, wishlists, budget and date. |
| [Event details](event-details.md) | Secret-Santa | One event's occasion info: date, location, notes. |
| [Exclusion](exclusion.md) | Secret-Santa | A one-directional rule preventing a giver from being matched to a recipient. |
| [Gift note](gift-note.md) | Secret-Santa | A stable per-member note (allergies / dietary / do-not-gift) the giver sees on the reveal. |
| [Group](group.md) | Secret-Santa | A shared roster of participants who exchange gifts across one or more events. |
| [Invitation](invitation.md) | Circles | A pending offer to join a circle, sent to an email address. |
| [Join link](join-link.md) | Secret-Santa | A capability link to join a shared-by-link group as a participant, with or without an account. |
| [Managed member](managed-member.md) | Circles | A person in a circle with a profile but no account, maintained by an admin. |
| [Member](member.md) | Secret-Santa | A person participating in a group; optionally linked to a user. |
| [Passkey](passkey.md) | Identity | A WebAuthn credential letting a user sign in without a password. |
| [Person claim](person-claim.md) | Secret-Santa | A joiner asserting which member they are in a shared-by-link group; first-claim-locks, admin-released. |
| [Reveal link](reveal-link.md) | Secret-Santa | A self-contained encoded link that reveals one giver's assignment client-side. |
| [Share token](share-token.md) | Platform · Secret-Santa | The unguessable key a shared-by-link group is addressed by on the server; carried in its links. |
| [Shared-by-link group](shared-by-link-group.md) | Secret-Santa · Platform | A group stored on the server keyed by a share token, no account owner — the third storage mode. |
| [Sync](sync.md) | Platform | Local-first reconciliation of on-device data with the server for a signed-in user. |
| [Tombstone](tombstone.md) | Platform | The record that an aggregate was deleted, so the deletion travels instead of resurrecting. |
| [User](user.md) | Identity | A person with an authenticated account, shared across all apps in the suite. |
| [Wishlist](wishlist.md) | Secret-Santa | A member's optional list of gift ideas, per event. |

_Keep this table sorted alphabetically as terms are added._
