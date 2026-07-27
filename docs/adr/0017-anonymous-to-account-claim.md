---
status: Accepted
date: 2026-07-27
deciders: [Adrian]
tags: [architecture, sync, identity, offline, ux]
---

# ADR-0017 — Anonymous → account claim

## Context and problem statement

Every user starts **without an account**. By the time someone signs up they may already have real
data on the device — [Groups](../glossary/group.md) they created, members, wishlists, a completed
[Draw](../glossary/draw.md) whose [reveal links](../glossary/reveal-link.md) are already in people's
hands.

The moment they first sign in, that anonymous local data meets an (initially empty, later possibly
populated) account. **What happens to it?** Getting this wrong is the fastest way to destroy trust in
a sync feature: the first thing a new account does would be to lose or duplicate the user's real
data.

## Decision drivers

- **First impression.** This flow runs exactly once per user, at the moment they are deciding whether
  the account was a good idea.
- **Nothing irreversible without consent.** Local data is the user's only copy — there is no server
  backup to restore from.
- Signing in must remain **additive** — it may not damage or remove the account-free experience
  ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).
- A user may sign in on a device that already holds data belonging to a *different* anonymous session
  (a shared family tablet).

## Considered options

1. **Choose per group** — show the local groups and let the user pick which to take into the account.
2. **Merge everything automatically** — upload all local groups on first sign-in.
3. **Replace local with server** — the account is the truth; local anonymous data is discarded.
4. **Never claim** — local data stays local forever; only newly created groups sync.

## Decision outcome

**Chosen: option 1 — choose per group, on first sign-in.**

- On the first sign-in on a device holding anonymous groups, the user is shown the list — *"You have
  3 groups on this device. Which should be added to your account?"* — and selects per group. Default
  selection is none; claiming is an opt-in action, not a checkbox to notice and untick.
- **Local data is kept as a cache after claiming, not cleared.** If the claim was a mistake, or the
  account is later deleted, the data is still there. Clearing would make an ostensibly additive
  action destructive.
- **Unclaimed groups stay local and account-free**, behaving exactly as they do today. They are not
  second-class and not nagged about.
- **Claiming is repeatable** — a group left behind can be claimed later from the group's own screen.
  The one-time prompt is a convenience, not the only door.
- **Claiming is one-directional but reversible in effect:** a claimed group can be removed from the
  account, which leaves the local copy intact.
- **Signing out does not delete local data.** It stops syncing; the cached copy remains usable
  offline, which is the account-free behaviour the app already has.

## Consequences

- **Positive:** no surprises and no silent data movement; the user sees exactly what they are
  uploading at the moment they upload it; a mistaken claim is recoverable because the local copy
  survives; the account-free path is genuinely untouched.
- **Negative / accepted:** an extra screen in the sign-up funnel, shown at the least convenient moment
  (someone who just wanted to log in). Accepted — the alternative is deciding on their behalf what
  happens to their only copy of real data.
- **Negative / accepted — data can exist twice.** After claiming, the same group is in the account
  *and* in local cache; if the user then signs in on a **second** device that also has an anonymous
  copy of what is really the same group, claiming it again produces a **duplicate**, because the two
  local copies have unrelated ids ([ADR-0008](0008-entity-id-strategy.md) mints them client-side with
  no shared origin). We do **not** attempt automatic de-duplication — heuristic matching on names and
  members would sometimes merge two genuinely different groups, which is worse than a visible
  duplicate the user can delete.
- **Negative / accepted:** on a shared device, one person can claim groups another person created
  anonymously. There is no identity on anonymous data, so there is nothing to check against. This is
  inherent to account-free local storage, and the same is already true of the raw `localStorage`
  today.
- **Follow-ups:** the claim screen and its wording are specified in the M3 feature spec; the transfer
  itself uses the mechanism in [ADR-0016](0016-sync-and-conflict-resolution.md).
- **New risks:** a user who claims nothing and later wipes the browser loses everything and may
  believe the account protected them. The claim prompt must say plainly that unclaimed groups stay on
  this device only.

## Pros and cons of the options

### 1. Choose per group (chosen)
- 👍 Explicit, no surprises, recoverable; the user learns what sync means by doing it once.
- 👎 An extra step; duplicates possible across devices.

### 2. Merge everything automatically
- 👍 Zero friction; nothing to explain.
- 👎 Uploads data the user may deliberately have kept local — an unannounced privacy change, and
  exactly the behaviour that makes people distrust "optional" accounts.

### 3. Replace local with server
- 👍 Simplest mental model; one source of truth.
- 👎 **Destroys local data on first sign-in.** Unacceptable: for a new account the server side is
  empty, so this deletes everything the user has.

### 4. Never claim
- 👍 Trivial; no migration logic at all.
- 👎 Existing groups can never benefit from sync, so the feature is useless to exactly the users who
  already have data — i.e. everyone who liked the app enough to sign up.

## Links

- Related ADRs: [ADR-0016](0016-sync-and-conflict-resolution.md),
  [ADR-0008](0008-entity-id-strategy.md), [ADR-0015](0015-mvp-client-offline-storage.md),
  [ADR-0009](0009-authentication-and-passkeys.md)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md) §8.3
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
- Source: [brainstorming — accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D4
- Glossary: [Sync](../glossary/sync.md), [User](../glossary/user.md), [Group](../glossary/group.md)
