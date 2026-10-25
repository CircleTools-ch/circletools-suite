---
term: Owner
contexts: [Secret-Santa]
status: Agreed
---

# Owner

## Definition

The role that organizes an [online group](online-group.md): read and write the whole document, manage
participants and links, manage roles, delete or hand over the group ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). Every online group has exactly one
owner, held by an **account** or by the [organizer link](organizer-link.md).

Ownership moves without loss: an account can **take over** a group its organizer link owns (the link then stops
working), and the owning account can **hand it over** to a fresh organizer link — also what happens to its groups
when the account is deleted, unless the user chose to delete them
([FR-043](../requirements/functional/FR-043-release-ownership-and-deletion-disposition.md)).

## Bounded context(s)

- **Secret-Santa:** one [role assignment](role-assignment.md) per group, with the owner role. The permissions it grants live in one table (`RolePermissions`); a later admin role is a new row there.

## Synonyms / do-not-use

- **Synonyms:** organizing role.
- **Do NOT use:** "admin" (a separate, planned role); "claim" for taking over.

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Gehört dir (Besitz) |
| EN | Owner |
| FR | Propriétaire |

## Related terms

- [Role assignment](role-assignment.md), [Organizer](organizer.md), [Organizer link](organizer-link.md), [Online group](online-group.md)

## First appearance

- [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) — one way to be in a group; [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md).
