---
term: Claim
contexts: [Platform, Secret-Santa]
status: Superseded
---

# Claim

> **Retired** ([ADR-0086](../adr/0086-one-way-to-be-in-a-group.md)). Groups no longer move into an account by a claim: a group is put [online](place.md), and an account can **take over** a group its organizer link owns ([Owner](owner.md)). The account sync carries profile and preferences only ([Sync](sync.md)). The entry below is kept for history.

## Definition

The act of taking data that exists **only on this device**, created without an account, into a
[User](user.md)'s account, so that it is stored on the server and
[synced](sync.md) to their other devices. In v2 the only thing that is claimed is a Secret-Santa
[Group](group.md).

A claim is:

- **explicit and per aggregate** — the user picks which groups, one by one, and nothing is selected
  for them (decision D4, [ADR-0017](../adr/0017-anonymous-to-account-claim.md));
- **additive** — the local copy is kept as a cache, never cleared, so a mistaken claim costs nothing;
- **repeatable** — a group left behind can be claimed later from its own screen;
- **reversible in effect** — a claimed group can be removed from the account again, which deletes the
  server copy and leaves the local one intact.

The reverse operation has no separate domain term: it is *removing a group from the account*
([FR-026](../requirements/functional/FR-026-remove-group-from-account.md)). It is **not** a delete —
deleting removes the group everywhere, via a [tombstone](tombstone.md).

## Bounded context(s)

- **Platform:** the mechanism — an aggregate gains an account scope and enters
  [sync](sync.md).
- **Secret-Santa:** the only aggregate that is claimable in v2. A claimed group carries its
  [Draw](draw.md) and therefore its [Assignments](assignment.md), which is the moment
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)'s amended, account-mode
  half takes effect.
- **Circles:** not applicable — a [Circle](circle.md) is created in an account and never exists
  account-free, so there is nothing to claim.

## Synonyms / do-not-use

- **Synonyms:** none in code. In the UI it is phrased as *adding to your account*.
- **Do NOT use:** "import" or "migrate" (both imply the local copy is consumed — it is not);
  "upload" (describes the transport, not the change of ownership); "merge" (nothing is merged — a
  claim of "the same" group from a second device deliberately produces a duplicate); "backup"
  (see [Sync](sync.md)).
- **Naming hazard:** `Claim` is also an ASP.NET Core Identity type (`System.Security.Claims.Claim`),
  which is in scope across the Identity context. Never introduce a bare `Claim` type or variable in
  code that also touches authentication claims — use the verb form and a qualified noun
  (`ClaimGroupCommand`, `GroupClaim`, `IsClaimed`) so the two never look alike at a call site.

## Product display terms (reference only — not code names)

The UI never says "claim".

| Language | Term |
|----------|------|
| DE | Zu deinem Konto hinzufügen |
| EN | Add to your account |
| FR | Ajouter à votre compte |

## Related terms

- [Sync](sync.md), [Tombstone](tombstone.md), [Group](group.md), [User](user.md),
  [Assignment](assignment.md)

## First appearance

- [Brainstorming — Accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md),
  decision D4 → [ADR-0017](../adr/0017-anonymous-to-account-claim.md) →
  [UC-SS-007](../use-cases/secret-santa/UC-SS-007-claim-local-groups.md)
