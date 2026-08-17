---
term: Managed member
contexts: [Circles]
status: Draft
---

# Managed member

## Definition

A person who belongs to a [Circle](circle.md) and has a profile, but **no account** — they cannot
sign in, and an admin of the circle maintains their details on their behalf. This is how young
children and relatives who will not sign up still take part.

A managed member has a [Circle membership](circle-membership.md) like anyone else, always in the
`member` role, and can be carried into a Secret-Santa [Group](group.md) as a
[Member](member.md) exactly like an account-backed person. Because they cannot sign in, they receive
their [Assignment](assignment.md) the account-free way — through a
[Reveal link](reveal-link.md) that the organizer passes on.

A managed member may later be **promoted**: when the person does get an account, the profile is
linked to the new [User](user.md) rather than duplicated, and their history stays intact. Promotion
happens through an [invitation](invitation.md), aimed at the profile one of two ways — the inviting
admin picks it when sending, or, when they don't, the invited person picks themselves out of the
circle's offline people as they accept ([ADR-0043](../adr/0043-invitee-chosen-managed-member-promotion.md)).

## Bounded context(s)

- **Circles:** a person in a circle without an identity behind them. Created and edited by a circle
  owner or admin, never through an [Invitation](invitation.md).

## Synonyms / do-not-use

- **Synonyms:** managed profile.
- **Do NOT use:** "child" or "kid" — age is not the defining trait, the absence of an account is;
  "guest"; "dependent"; "dummy" or "placeholder" user — these are real people.

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Verwaltetes Mitglied |
| EN | Managed member |
| FR | Membre géré·e |

## Related terms

- [Circle](circle.md), [Circle membership](circle-membership.md), [User](user.md), [Member](member.md), [Reveal link](reveal-link.md)

## First appearance

- [Brainstorming — Accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D7
