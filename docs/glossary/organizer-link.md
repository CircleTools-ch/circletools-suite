---
term: Organizer link
contexts: [Secret-Santa]
status: Agreed
---

# Organizer link

## Definition

The link that lets a device **organize an [online group](online-group.md) without an account** ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md), decision
D1). It carries the group id and an unguessable token (`/admin#g=…&t=…`); every device that opens it becomes an
[organizer](organizer.md) of the group, so it is also how organizers share the work. The token is a principal of
the group's [owner](owner.md) role assignment and is stored only as a hash (plus an encrypted copy, so the
organizer's devices can show the link again).

It can be replaced with a fresh one (the old one stops working). When an account takes the group over, the
organizer link stops working; handing the group back to a link mints a new one.

## Bounded context(s)

- **Secret-Santa:** the owner principal of a group no account owns. Its token travels in a request header, never in a URL path.

## Synonyms / do-not-use

- **Synonyms:** management link.
- **Do NOT use:** "admin link" (its old name, from the shared-by-link model); "share token".

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Verwaltungs-Link |
| EN | Management link |
| FR | Lien de gestion |

## Related terms

- [Owner](owner.md), [Online group](online-group.md), [Join link](join-link.md), [Role assignment](role-assignment.md)

## First appearance

- [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) — one way to be in a group; [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md).
