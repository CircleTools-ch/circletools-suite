---
term: Circle
contexts: [Circles]
status: Draft
---

# Circle

## Definition

A persistent, named set of people who belong together and share apps and data across the suite — a
family, a household, or a group of friends. A circle owns its
[Circle memberships](circle-membership.md) (each carrying a role), its pending
[Invitations](invitation.md), and any [Managed members](managed-member.md) that have no account of
their own. A [User](user.md) may belong to several circles.

A circle is **not** a Secret-Santa [Group](group.md): a circle is the standing set of people, while a
group is one gift exchange. A group may be created *from* a circle, which copies its people in as
[Members](member.md); the group then keeps its own membership from that point on.

## Bounded context(s)

- **Circles:** the aggregate root — name, label, memberships, roles, invitations.
- **Secret-Santa:** appears only as the optional origin of a group's members. The Secret-Santa domain
  never depends on the Circles context; it holds at most a circle identifier.

## Naming

`Circle` is the **canonical code name** — it is what appears in aggregates, DB tables, DTOs and URLs.
The name users *see* is a per-circle **label they choose themselves**, defaulting to "Family"
(DE *Familie*, FR *Famille*). The label is display data; it never affects code, storage or routing.
This split exists because the product serves families *and* friend-groups, and neither word is honest
for the other.

## Synonyms / do-not-use

- **Synonyms:** family, household, crew (all **display labels only**, never code names).
- **Do NOT use:** "group" — it is taken by the Secret-Santa aggregate and reusing it collides;
  "family" as an identifier, table or type name (it is one possible label among several); "team",
  "org", "tenant".

## Product display terms (reference only — not code names)

The feature itself is called **Circle** (plural **Circles**) in every language, capitalised as a
product name ([ADR-0089](../adr/0089-circle-as-a-product-name-in-every-language.md)): DE *der Circle,
die Circles*; FR *le Circle, les Circles*; EN *Circle, Circles*. Not "Kreis" or "cercle".

Each circle's own label, chosen by its members:

| Language | Term |
|----------|------|
| DE | Familie *(Standard-Bezeichnung, änderbar)* |
| EN | Family *(default label, user-changeable)* |
| FR | Famille *(libellé par défaut, modifiable)* |

## Related terms

- [Circle membership](circle-membership.md), [User](user.md), [Invitation](invitation.md), [Managed member](managed-member.md), [Group](group.md), [Member](member.md)

## First appearance

- [Brainstorming — Accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D6
