---
term: Join link
contexts: [Secret-Santa]
status: Draft
---

# Join link

## Definition

The link that lets a person **take part** in an [online group](online-group.md) ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). It works with or without
an account. Opening it asks two things:

- **Who you are** — pick a [Member](member.md) whose spot is still free, or add yourself under a new name.
- **How to remember you** — with your **account** (on all your devices) or **just this device** (a
  [device key](device-key.md)).

Either way the person becomes a [participant](participant.md): they see their own pairing per event and keep
their own wishlist and gift note — never another member's. The organizer turns the join link on or off and can
replace it with a fresh one; it keeps working when the group's owner changes.

## Bounded context(s)

- **Secret-Santa:** one of four routes to a participant (join link, email invitation, circle, own member).

## Synonyms / do-not-use

- **Synonyms:** participant link.
- **Do NOT use:** "invite link" (ambiguous — reserve for circle email invitations); "member link"
  (the old model's account link).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Beitritts-Link |
| EN | Join link |
| FR | Lien pour rejoindre |

## Related terms

- [Participant](participant.md), [Device key](device-key.md), [Online group](online-group.md),
  [Organizer link](organizer-link.md), [Member](member.md)

## First appearance

- [ADR-0058](../adr/0058-account-free-link-shared-groups.md).
