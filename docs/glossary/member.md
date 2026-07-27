---
term: Member
contexts: [Secret-Santa]
status: Draft
---

# Member

## Definition

A person participating in a [Group](group.md). A member has at least a display name and an optional
[Wishlist](wishlist.md) of gift ideas; contact details are optional. Members are the givers and
recipients in a [Draw](draw.md).

A member may additionally be **linked to a [User](user.md)** by an optional account reference. The
link is what changes how that person is served, not what they *are*:

| | Unlinked member | Linked member |
|---|---|---|
| Identity | a name in the group, nothing more | references a suite account |
| Sees their assignment | by opening their [Reveal link](reveal-link.md) | in the app, signed in on any device |
| Wishlist | the organizer types it for them | they own and edit it themselves |

Unlinked members stay **first-class** — a group can consist entirely of them, and the whole
account-free flow keeps working unchanged. Linking is additive.

## Bounded context(s)

- **Secret-Santa:** a participant within one group. The same real person in two groups is two
  members (membership is per group). A member may hold an optional user reference, but the
  Secret-Santa domain never depends on the Identity or Circles contexts — it stores an identifier,
  not a user.

## Synonyms / do-not-use

- **Synonyms:** participant.
- **Do NOT use:** "user" as a name for a member — a member *may reference* a [User](user.md), but the
  two are different concepts and most members have no account at all. Do not use "member" unqualified
  for someone in a [Circle](circle.md) either; that is a
  [Circle membership](circle-membership.md).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Teilnehmer:in |
| EN | Member |
| FR | Participant·e |

## Related terms

- [Group](group.md), [Wishlist](wishlist.md), [Assignment](assignment.md), [Exclusion](exclusion.md), [User](user.md), [Circle](circle.md), [Managed member](managed-member.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md); wishlist added in [UC-SS-002](../use-cases/secret-santa/UC-SS-002-manage-members.md); optional user link added by [brainstorming v2](../brainstorming/2026-07-24-accounts-families-sync.md) decision D3
