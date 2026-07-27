---
term: User
contexts: [Identity]
status: Draft
---

# User

## Definition

A person with an authenticated account in the suite. One user account is shared across **all** apps —
there is no per-app login. A user has credentials (email + password, optionally one or more
passkeys), a profile (display name, preferred language DE/EN/FR), and the right to have their data
exported and their account deleted.

An account is **optional and additive**. Every offline, account-free flow keeps working without one
(see [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)); signing in adds
[Circles](circle.md) and [Sync](sync.md), it never becomes a precondition for using an app.

## Bounded context(s)

- **Identity:** the aggregate — credentials, profile, account lifecycle. Backed by ASP.NET Core
  Identity with built-in passkeys per
  [ADR-0009](../adr/0009-authentication-and-passkeys.md).
- **Secret-Santa:** referenced only as an optional `UserId` on a [Member](member.md). The
  Secret-Santa domain never depends on the Identity context.

## Synonyms / do-not-use

- **Synonyms:** account (the credential/lifecycle facet of the same concept — not a separate entity).
- **Do NOT use:** "member" for a user — a [Member](member.md) is a participant in one Secret-Santa
  [Group](group.md) and usually has no account at all; "person"; "customer"; "profile" for the whole
  user (the profile is one part of it).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Konto |
| EN | Account |
| FR | Compte |

## Related terms

- [Circle](circle.md), [Circle membership](circle-membership.md), [Member](member.md), [Managed member](managed-member.md), [Sync](sync.md)

## First appearance

- [Brainstorming — Accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md)
