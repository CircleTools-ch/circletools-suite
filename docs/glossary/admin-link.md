---
term: Admin link
contexts: [Secret-Santa]
status: Superseded
---

# Admin link

> **Replaced by [Organizer link](organizer-link.md)** ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). Same idea — organize a group without an account — now the owner principal of an [online group](online-group.md), carrying the group id (`/admin#g=…&t=…`). The entry below describes the old shared-by-link model and is kept for history.

## Definition

A capability link to a [shared-by-link group](shared-by-link-group.md) that grants the **admin
(organizer) role** — manage members and exclusions, set the budget, run/redo the [Draw](draw.md), and
manage sharing ([ADR-0058](../adr/0058-account-free-link-shared-groups.md)). Key properties:

- **A shared capability, not a seat** — several co-organizers can hold one admin link at once.
- **Account-free only** — it works *only while no account owns the group*. When an account
  [claims admin](claim.md), that account becomes the sole admin and the admin link **stops working**
  (a holder then sees *"<name> has claimed the admin role for this group"*).
- **Rotatable** — an admin can **disable the current link and mint a fresh one**; the old
  [share token](share-token.md) stops resolving.

Distinct from the [join link](join-link.md), which grants only participant access.

## Bounded context(s)

- **Secret-Santa:** the organizer-capability link for a shared-by-link group.

## Synonyms / do-not-use

- **Synonyms:** organizer link, manage link.
- **Do NOT use:** "invite link" (ambiguous with the [join link](join-link.md) and with circle
  invitations); "owner link" (there is no single owner in account-free mode).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Admin-Link |
| EN | Admin link |
| FR | Lien admin |

## Related terms

- [Join link](join-link.md), [Shared-by-link group](shared-by-link-group.md),
  [Share token](share-token.md), [Claim](claim.md)

## First appearance

- [ADR-0058](../adr/0058-account-free-link-shared-groups.md).
