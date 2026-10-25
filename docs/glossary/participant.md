---
term: Participant
contexts: [Secret-Santa]
status: Agreed
---

# Participant

## Definition

A person who **takes part** in an [online group](online-group.md) as one [Member](member.md) of its roster: a
[role assignment](role-assignment.md) with the participant role, held by their account or by a
[device key](device-key.md) on one device ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). A participant reads their own pairing per event and keeps
their own [member entry](member-entry.md) (wishlists and gift note); they never read the whole document.

There is one way to be a participant and four routes to it, all creating the same thing:

- the **join link** — the person picks themselves (or adds a new name);
- an **email invitation** — the organizer invites an address, the account accepts;
- a **circle** — the organizer brings in people from a shared circle;
- the **own member** — the organizer connects their own account to their own member ("This is me").

The organizer can **free a participant's spot** (release): the role goes, what the person wrote stays in the
group, and the member can be taken again.

## Bounded context(s)

- **Secret-Santa:** a role, not a kind of member. A member without a participant is still a first-class member; the organizer maintains their entry or they use their reveal link.

## Synonyms / do-not-use

- **Synonyms:** taking part, joined person.
- **Do NOT use:** "linked member", "claimed person", "joiner".

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Teilnehmende Person (macht mit) |
| EN | Participant (takes part) |
| FR | Participant·e |

## Related terms

- [Role assignment](role-assignment.md), [Member entry](member-entry.md), [Join link](join-link.md), [Device key](device-key.md), [Member](member.md)

## First appearance

- [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) — one way to be in a group; [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md).
