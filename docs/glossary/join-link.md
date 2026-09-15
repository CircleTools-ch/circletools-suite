---
term: Join link
contexts: [Secret-Santa]
status: Draft
---

# Join link

## Definition

A capability link to a [shared-by-link group](shared-by-link-group.md) that lets a person **join as a
participant** ([ADR-0058](../adr/0058-account-free-link-shared-groups.md)). It works **with or without
an account**. Opening it starts a short *"who are you in this group?"* flow with two independent
choices:

- **Which person** — a [person claim](person-claim.md): claim an existing [Member](member.md) in the
  roster, or add a new person (yourself).
- **How remembered** — link the person to an **account** (durable, cross-device — reuses member↔user
  linking, [ADR-0019](../adr/0019-member-user-linking.md)), or **just this device** (a device-local
  claim, nothing synced beyond the device).

A joined participant sees the group and edits **their own** [wishlist](wishlist.md) / [gift
note](gift-note.md), and sees **their own** [Assignment](assignment.md) once drawn — never another
member's. Unlike the [admin link](admin-link.md), the join link **keeps working** after an account
claims admin.

## Bounded context(s)

- **Secret-Santa:** the participant-entry link for a shared-by-link group.

## Synonyms / do-not-use

- **Synonyms:** participant link.
- **Do NOT use:** "invite link" (ambiguous — reserve for circle email invitations); "member link"
  (collides with [member↔user linking](member.md)).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Beitritts-Link |
| EN | Join link |
| FR | Lien pour rejoindre |

## Related terms

- [Admin link](admin-link.md), [Person claim](person-claim.md),
  [Shared-by-link group](shared-by-link-group.md), [Share token](share-token.md), [Member](member.md)

## First appearance

- [ADR-0058](../adr/0058-account-free-link-shared-groups.md).
