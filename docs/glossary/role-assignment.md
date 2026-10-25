---
term: Role assignment
contexts: [Secret-Santa]
status: Agreed
---

# Role assignment

## Definition

The record that a **principal** holds a **role** in one [online group](online-group.md), optionally as one
[Member](member.md) of its roster ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). It is how the group store answers "who may do what":

- **Principal:** an account, the [organizer link](organizer-link.md), or a [device key](device-key.md).
- **Role:** [owner](owner.md) or [participant](participant.md) today; an admin role is planned.
- **Member:** the roster member the principal is, if any.
- **State:** *invited* (an email invitation waiting for an answer; grants nothing) or *active*.

Endpoints check permissions, never role names; one table maps each role to its permissions. Having a member adds
the permission to read one's own view, so an owner in the roster sees their own assignment. A principal holds at
most one role assignment per group, and a member at most one principal.

## Bounded context(s)

- **Secret-Santa:** a row of the group store (`role_assignments`). It replaces the member's account link, the person claim and the participant key of the old model.

## Synonyms / do-not-use

- **Synonyms:** role, access.
- **Do NOT use:** "linked member", "member link", "person claim" (all replaced by this).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | — (intern) |
| EN | — (internal) |
| FR | — (interne) |

## Related terms

- [Owner](owner.md), [Participant](participant.md), [Device key](device-key.md), [Organizer link](organizer-link.md), [Member](member.md)

## First appearance

- [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) — one way to be in a group; [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md).
