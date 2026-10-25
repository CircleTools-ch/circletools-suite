---
status: Superseded by ADR-0086
date: 2026-07-27
deciders: [Adrian]
tags: [architecture, domain, secret-santa, identity]
---

# ADR-0019 — Member↔User linking

> **Superseded by [ADR-0086](0086-one-way-to-be-in-a-group.md) (2026-10-22).** Linked members are replaced by participants: every route into a group creates the same participant role assignment.

## Context and problem statement

In the MVP a [Member](../glossary/member.md) is just a name inside a
[Group](../glossary/group.md) — the organizer types everyone in, writes their
[wishlists](../glossary/wishlist.md) for them, and hands out
[reveal links](../glossary/reveal-link.md).

v2 introduces [Users](../glossary/user.md). A member can now *be* someone with an account, who could
see their own [Assignment](../glossary/assignment.md) in the app on their own device and maintain
their own wishlist. **What does `Member` become once accounts exist?**

The answer must not compromise the offline, account-free mode: a group of nothing but typed-in names
has to keep working exactly as it does today.

## Decision drivers

- **Unlinked members stay first-class.** Most members, in most groups, will never have an account.
- **The Secret-Santa domain must not depend on the Identity or Circles contexts** — dependencies point
  inward and the domain has no I/O (`CLAUDE.md` rule 6, architecture guidelines).
- Members and users have genuinely different lifecycles: a member exists per group, a user exists once
  for the whole suite.
- The change should be the smallest one that unlocks in-app reveal and self-owned wishlists.

## Considered options

1. **`Member` keeps its identity and gains an optional `UserId`.**
2. **`Member` becomes two subtypes** — named-only vs account-backed.
3. **Members are always users** — every participant must have an account.

## Decision outcome

**Chosen: option 1 — an optional `UserId` on `Member`.**

`Member` remains what it is: a participant in one group, with a display name and an optional
wishlist. It gains a **nullable reference to a user**. The link changes how that person is *served*,
never what they *are*:

| | Unlinked member | Linked member |
|---|---|---|
| Identity | a name in the group, nothing more | references a suite account |
| Sees their assignment | by opening their reveal link | in the app, signed in, on any device |
| Wishlist | the organizer types it for them | they own and edit it themselves |

- **The domain stores an identifier, not a user.** `Member` holds a `UserId` value — it does not
  navigate to an Identity aggregate, and the Secret-Santa `Core` project takes no dependency on the
  Identity or Circles contexts. Resolving a `UserId` to a display name or avatar is an application-
  layer concern.
- **Linking is per member, per group**, and works two ways: implicitly, when a group is created from
  a [Circle](../glossary/circle.md) whose people have accounts
  ([ADR-0018](0018-circle-and-sharing-model.md)), or **ad hoc**, when an organizer links an individual
  member to an account with no circle involved.
- **Mixed groups are normal and expected** — some members linked, some not. The organizer keeps
  generating reveal links for the unlinked ones; nothing about that flow changes.
- **Unlinking is allowed** and reverts that member to unlinked behaviour. It does not delete the
  member or their wishlist; wishlist ownership returns to the organizer.
- **A [managed member](../glossary/managed-member.md) is never linked** — they have no account by
  definition. If they later get one, the promotion in ADR-0018 supplies a `UserId` and the member
  becomes linked at that point.
- **The link does not confer organizer rights.** A linked member sees their own assignment and edits
  their own wishlist; running the draw, editing the group and generating links stay with the
  organizer.

## Consequences

- **Positive:** the smallest possible change to a shipped, tested aggregate; unlinked members are
  untouched and the account-free flow keeps working unchanged; the domain stays free of identity
  dependencies, so the layering rule holds; mixed groups need no special case.
- **Negative / accepted — a nullable foreign reference invites null-handling bugs.** Every read path
  that shows "who is this member" must handle both shapes. Mitigated by keeping the branch in one
  place (the application layer resolving display data) rather than scattering `if (UserId is null)`
  through the domain.
- **Negative / accepted — the same person in two groups is still two members**, each linked
  separately. This is existing behaviour (membership is per group) and it means linking is a
  per-group chore, not a one-time act. Creating groups from a circle is what makes this bearable.
- **Negative / accepted:** a `UserId` referencing a deleted account leaves a dangling reference. The
  member must degrade gracefully to unlinked rather than break the group — account deletion has to
  unlink, which is a cross-context concern the M4 spec must specify.
- **Negative / accepted:** two members in one group could be linked to the *same* user. This should be
  rejected at the application layer (a person cannot give a gift to themselves twice), but the domain
  itself cannot enforce it without knowing about identity.
- **Follow-ups:** M4 feature spec (linking UI, unlink, account-deletion cascade, duplicate-link
  guard); `UC-SS-007+` use cases; the authorization rules that make in-app reveal safe are in
  [ADR-0020](0020-server-side-assignment-privacy.md).
- **New risks:** in-app reveal is only meaningful because assignments now live server-side — this ADR
  is therefore load-bearing on ADR-0020, and if that privacy posture were ever reversed, linked
  members would lose the main benefit of linking.

## Pros and cons of the options

### 1. Optional `UserId` (chosen)
- 👍 Smallest change; unlinked members stay first-class; domain stays identity-free; mixed groups fall
  out naturally.
- 👎 Nullable reference to handle on every display path.

### 2. Two subtypes
- 👍 Type-safe — the compiler distinguishes the two cases.
- 👎 Premature polymorphism: the two differ in how the person is *served*, not in what they *are*, so
  the subtypes would share every field and differ only in presentation. Doubles the aggregate's shape
  for no invariant.

### 3. Members are always users
- 👍 One concept; no nulls; every member can self-serve.
- 👎 **Kills the offline, account-free mode** — the product's core promise. Rejected outright.

## Links

- Related ADRs: [ADR-0018](0018-circle-and-sharing-model.md),
  [ADR-0020](0020-server-side-assignment-privacy.md),
  [ADR-0016](0016-sync-and-conflict-resolution.md),
  [ADR-0014](0014-reveal-link-encoding-and-privacy.md)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md) §8.1
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md), [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)
- Source: [brainstorming — accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D3
- Glossary: [Member](../glossary/member.md), [User](../glossary/user.md), [Wishlist](../glossary/wishlist.md), [Managed member](../glossary/managed-member.md)
