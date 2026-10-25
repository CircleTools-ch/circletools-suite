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
| [Admin link](admin-link.md) | Secret-Santa | *Replaced by [Organizer link](organizer-link.md).* |
| [Assignment](assignment.md) | Secret-Santa | A single giver→recipient pairing produced by a draw. |
| [Budget](budget.md) | Secret-Santa | The agreed spending limit for gifts in a group. |
| [Carry-forward](carry-forward.md) | Secret-Santa | Creating a new event by copying wishlists, budget and the prior draw (as the no-repeat baseline) from an existing event. |
| [Circle](circle.md) | Circles | A persistent named set of people sharing apps and data; UI label is user-chosen. |
| [Circle membership](circle-membership.md) | Circles | A person's link to a circle, carrying their role (owner/admin/member). |
| [Claim](claim.md) | Platform · Secret-Santa | *Retired: groups are put online; an account takes over a link-owned group.* |
| [Device key](device-key.md) | Secret-Santa | The secret a device gets when someone joins an online group without an account. |
| [Draw](draw.md) | Secret-Santa | The act/result of randomly assigning gift-givers to recipients under the draw rules, per event. |
| [Event](event.md) | Secret-Santa | One gift-exchange occasion within a group, with its own draw, wishlists, budget and date. |
| [Event details](event-details.md) | Secret-Santa | One event's occasion info: date, location, notes. |
| [Exclusion](exclusion.md) | Secret-Santa | A one-directional rule preventing a giver from being matched to a recipient. |
| [Gift note](gift-note.md) | Secret-Santa | A stable per-member note (allergies / dietary / do-not-gift) the giver sees on the reveal. |
| [Group](group.md) | Secret-Santa | A shared roster of participants who exchange gifts across one or more events. |
| [Invitation](invitation.md) | Circles | A pending offer to join a circle, sent to an email address. |
| [Join link](join-link.md) | Secret-Santa | The link through which a person takes part in an online group, with an account or on one device. |
| [Managed member](managed-member.md) | Circles | A person in a circle with a profile but no account, maintained by an admin. |
| [Member](member.md) | Secret-Santa | A person in a group's roster; carries no account. |
| [Member entry](member-entry.md) | Secret-Santa | What a member keeps themselves in an online group: gift note and wishlists; merged into the document. |
| [Online group](online-group.md) | Secret-Santa | A group kept on the server; reached by its owner and participants through role assignments. |
| [Organizer](organizer.md) | Secret-Santa | The person who manages a group; in an online group, whoever holds the owner role. |
| [Organizer link](organizer-link.md) | Secret-Santa | The link that lets devices organize an online group without an account. |
| [Owner](owner.md) | Secret-Santa | The one organizing role of an online group, held by an account or the organizer link. |
| [Participant](participant.md) | Secret-Santa | A person taking part in an online group as one member: their own pairing and entry. |
| [Participant contribution](participant-contribution.md) | Secret-Santa | *Replaced by [Member entry](member-entry.md).* |
| [Participant key](participant-key.md) | Secret-Santa | *Replaced by [Device key](device-key.md).* |
| [Passkey](passkey.md) | Identity | A WebAuthn credential letting a user sign in without a password. |
| [Person claim](person-claim.md) | Secret-Santa | *Replaced by [Participant](participant.md).* |
| [Place](place.md) | Secret-Santa | Where a group is kept: on this device or online. |
| [Reveal link](reveal-link.md) | Secret-Santa | A self-contained encoded link that reveals one giver's assignment client-side. |
| [Role assignment](role-assignment.md) | Secret-Santa | A principal's role in an online group, optionally as one member. |
| [Share token](share-token.md) | Platform · Secret-Santa | *Retired: online groups are addressed by id; access secrets are stored hashed.* |
| [Shared-by-link group](shared-by-link-group.md) | Secret-Santa · Platform | *Replaced by [Online group](online-group.md).* |
| [Sync](sync.md) | Platform · Secret-Santa | Keeping device and server in step: online groups have their own sync; the account sync carries profile and preferences. |
| [Tombstone](tombstone.md) | Platform | The record that an aggregate was deleted, so the deletion travels instead of resurrecting. |
| [User](user.md) | Identity | A person with an authenticated account, shared across all apps in the suite. |
| [Wishlist](wishlist.md) | Secret-Santa | A member's optional list of gift ideas, per event. |

_Keep this table sorted alphabetically as terms are added._
