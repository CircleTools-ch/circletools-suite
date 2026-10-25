---
term: Device key
contexts: [Secret-Santa]
status: Agreed
---

# Device key

## Definition

The secret a device receives when a person joins an [online group](online-group.md) **without an account**
("just this device") ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). It is the principal of that person's [participant](participant.md)
[role assignment](role-assignment.md): only that device can read the person's own view and write their
[member entry](member-entry.md). The server keeps only its hash.

A lost device means a lost key: the organizer frees the spot, and the person joins again from their new device.

## Bounded context(s)

- **Secret-Santa:** one of the three principal kinds (account, organizer link, device key). It travels in a request header.

## Synonyms / do-not-use

- **Synonyms:** —
- **Do NOT use:** "participant key" (its old name).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | — (Nur dieses Gerät) |
| EN | — (Just this device) |
| FR | — (Cet appareil uniquement) |

## Related terms

- [Participant](participant.md), [Role assignment](role-assignment.md), [Join link](join-link.md)

## First appearance

- [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) — one way to be in a group; [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md).
