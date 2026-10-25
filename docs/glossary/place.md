---
term: Place
contexts: [Secret-Santa]
status: Agreed
---

# Place

## Definition

Where a [Group](group.md) is kept. There are two places ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)):

- **On this device** *(default)* — the group lives only in the device's local storage. Nothing reaches the
  server; people get their pairing through [reveal links](reveal-link.md).
- **Online** — the group is an [online group](online-group.md): one copy on the server, which its
  [organizers](organizer.md) and [participants](participant.md) reach from their own devices.

A group can move between the two at any time. Going back to *on this device* deletes the online copy and keeps
the device's copy.

## Bounded context(s)

- **Secret-Santa:** a property of this device's record of a group (`GroupPlace` in code), never of the domain group itself.

## Synonyms / do-not-use

- **Synonyms:** storage, where the group is kept.
- **Do NOT use:** "storage mode", "My account", "shared by link" (the old four-kind model); "sync on/off".

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Auf diesem Gerät / Online |
| EN | On this device / Online |
| FR | Sur cet appareil / En ligne |

## Related terms

- [Online group](online-group.md), [Group](group.md), [Sync](sync.md)

## First appearance

- [ADR-0086](../adr/0086-one-way-to-be-in-a-group.md) — one way to be in a group; [FEAT-027](../features/FEAT-027-one-way-to-be-in-a-group.md).
