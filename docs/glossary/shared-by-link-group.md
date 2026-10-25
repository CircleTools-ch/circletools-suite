---
term: Shared-by-link group
contexts: [Secret-Santa, Platform]
status: Superseded
---

# Shared-by-link group

> **Replaced by [Online group](online-group.md)** ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). There is one kind of online group, owned by an account or by an [organizer link](organizer-link.md); "shared by link" and "My account" are no longer separate kinds. The entry below is kept for history.

## Definition

A [Group](group.md) stored on the server and keyed by an unguessable [share token](share-token.md)
rather than owned by a [User](user.md) account — the **third storage mode**, sitting between
on-this-device (offline, local only) and account-synced
([ADR-0058](../adr/0058-account-free-link-shared-groups.md)). It is reachable, with **no sign-up**, by
two links: an [admin link](admin-link.md) (manage the group) and a [join link](join-link.md)
(join as a participant). Its aggregate — including the [Draw](draw.md) and
[Assignments](assignment.md) — is [synced](sync.md) between the server and every device holding a link.

A shared-by-link group can be **claimed to an account** (an account then becomes the sole admin — see
[Claim](claim.md)), and, conversely, an account-owned group can be **released** back to a
shared-by-link group so ownership is never a trap.

## Bounded context(s)

- **Secret-Santa:** the group whose occasions, roster, draw and assignments are shared.
- **Platform:** a storage/sharing mode of the sync capability; assignment secrecy is **trust-bounded**
  in this mode ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)).

## Synonyms / do-not-use

- **Synonyms:** account-free shared group, link-shared group.
- **Do NOT use:** "public group" (a link-holder is required; it is not public); "circle" (a
  [Circle](circle.md) is account-bound cross-app sharing, [ADR-0018](../adr/0018-circle-and-sharing-model.md)).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Über Link geteilt |
| EN | Shared by link |
| FR | Partagé par lien |

## Related terms

- [Admin link](admin-link.md), [Join link](join-link.md), [Share token](share-token.md),
  [Person claim](person-claim.md), [Claim](claim.md), [Group](group.md), [Sync](sync.md)

## First appearance

- [ADR-0058](../adr/0058-account-free-link-shared-groups.md) — account-free, backend-stored,
  link-shared groups.
