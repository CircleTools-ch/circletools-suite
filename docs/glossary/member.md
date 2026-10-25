---
term: Member
contexts: [Secret-Santa]
status: Draft
---

# Member

## Definition

A person in a [Group](group.md)'s roster. A member has a display name, an optional [gift note](gift-note.md) and
a [wishlist](wishlist.md) per event; members are the givers and recipients in a [Draw](draw.md).

The member is a **name in the roster**, nothing more: the group's document carries no account. In an
[online group](online-group.md), a person can take part **as** a member — a [participant](participant.md),
through their account or a device key ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). That changes how the person is served, not what the member is:

| | Member without a participant | Member with a participant |
|---|---|---|
| Sees their assignment | through their [reveal link](reveal-link.md) | in the app, on their participant page |
| Wishlist and gift note | the organizer types them, or the person uses their reveal link | the person keeps them ([member entry](member-entry.md)) |

Members without a participant stay **first-class** — a group can consist entirely of them, and a group on this
device always does.

## Bounded context(s)

- **Secret-Santa:** a roster entry within one group. The same real person in two groups is two members. Who a
  member is as a person lives in the group store's [role assignments](role-assignment.md), never in the domain.

## Synonyms / do-not-use

- **Synonyms:** roster entry.
- **Do NOT use:** "participant" for a member — a [participant](participant.md) is a person taking part *as* a
  member; "user" — a member never references an account; "linked member". Do not use "member" unqualified
  for someone in a [Circle](circle.md) either; that is a
  [Circle membership](circle-membership.md).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Teilnehmer:in |
| EN | Member |
| FR | Participant·e |

## Related terms

- [Group](group.md), [Wishlist](wishlist.md), [Assignment](assignment.md), [Exclusion](exclusion.md), [Participant](participant.md), [Member entry](member-entry.md), [Circle](circle.md), [Managed member](managed-member.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md); wishlist added in [UC-SS-002](../use-cases/secret-santa/UC-SS-002-manage-members.md); optional user link added by [brainstorming v2](../brainstorming/2026-07-24-accounts-families-sync.md) decision D3
