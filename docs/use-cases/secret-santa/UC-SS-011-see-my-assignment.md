---
id: UC-SS-011
title: See my assignment while signed in
app: Secret-Santa
actors: [Member]
status: Approved
priority: Must
related: [FR-029, NFR-002, ADR-0020, ADR-0019, ADR-0014]
---

# UC-SS-011 — See my assignment while signed in

## Summary / goal

A signed-in [User](../../glossary/user.md) who is a **linked** [Member](../../glossary/member.md) of a
drawn [Group](../../glossary/group.md) opens the app and sees **their own**
[Assignment](../../glossary/assignment.md) — who they give to, plus that person's
[Wishlist](../../glossary/wishlist.md), the [budget](../../glossary/budget.md) and the
[event details](../../glossary/event-details.md) — on **any** device, with **no
[reveal link](../../glossary/reveal-link.md)**. This is the single reason most people would sign in,
and the reason assignments now reach the server at all.

## Primary actor & stakeholders

- **Primary actor:** the **linked member** — a signed-in user linked to a member in a group whose
  draw has run.
- **Stakeholders & interests:** every **other** member, whose assignment must stay unreadable to this
  user; the operator, who is now a trusted party holding the pairings in plaintext
  ([ADR-0020](../../adr/0020-server-side-assignment-privacy.md)) and must say so; the organizer, whose
  full mapping is unchanged and still theirs alone.

## Preconditions

- The actor is **signed in**.
- The actor is **linked** to a member of a group ([UC-SS-010](UC-SS-010-link-member-to-account.md) or
  implicitly via [UC-SS-009](UC-SS-009-create-group-from-circle.md)).
- The group's **draw has run** and the group is account-scoped and synced
  ([UC-MAIN-015](../main/UC-MAIN-015-sync-across-devices.md)).

## Postconditions

- **Success:** the actor has seen their own recipient and that recipient's wishlist, budget and event
  — and **nothing about anyone else's pairing**.
- **Failure:** nothing is shown; no other member's assignment is ever exposed as a failure mode.

## Trigger

The actor opens the app (signed in) and views a group they are linked in, or an "my assignments"
overview across groups.

## Main success scenario

1. Actor opens the app signed in and sees the groups they are a linked member of; each drawn one
   offers "see who I'm giving to".
2. Actor opens one. Behind an **anti-spoiler** step — a deliberate reveal action, as on the reveal
   page ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)), so a glance at a shared
   screen doesn't ruin the surprise — the app requests the caller's assignment.
3. The server **authorizes** the request and returns **only the caller's own** pairing — a request for
   anyone else's is **denied, not filtered to empty**, and a non-organizer response never contains
   more than one assignment ([ADR-0020](../../adr/0020-server-side-assignment-privacy.md)). The client
   is never the thing that decides what the user may see.
4. Actor sees their recipient, that recipient's current wishlist, the budget and the event details.

## Extensions / alternate flows

- **1a. The draw has not run yet** → the group shows a plain "the organizer hasn't drawn yet"; there
  is nothing to reveal and nothing stored.
- **2a. The actor is linked in several groups** → each drawn group has its own assignment; the
  overview lists them, each behind its own reveal step.
- **4a. The organizer re-runs the draw** (members changed, so the old draw was invalidated) → on the
  next sync the actor sees the **current** assignment only; a stale pairing is never shown.
- **Offline** → the last-synced assignment is readable from the local cache (local-first,
  [UC-MAIN-016](../main/UC-MAIN-016-work-offline-and-reconcile.md)). It is the actor's **own**
  assignment on the actor's **own** device — the authorization decision was already made server-side
  when it was fetched; the cache does not re-decide it.

## Exceptions

- **The actor was unlinked** (organizer unlinked them, or the group was deleted) → the in-app
  assignment disappears; if the organizer still wants them in, they are served the account-free way
  via a reveal link ([UC-SS-005](UC-SS-005-share-reveal-links.md)). No error, no stale reveal.
- **A request for another member's assignment reaches the API** (a tampered client, a guessed id) →
  **denied**. This is the sharp failure mode the amended NFR-002 exists to guard, and it carries
  explicit cross-member acceptance criteria and authorization tests
  ([NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)).

## Non-functional notes

- **Offline/local-first:** account mode is additive. An **unlinked** member is unaffected — they open
  their reveal link exactly as today
  ([NFR-001](../../requirements/non-functional/NFR-001-local-first-offline.md),
  [UC-SS-006](UC-SS-006-open-reveal-link.md)); in-app reveal is an extra door for linked members, not
  a replacement.
- **Localization:** DE/EN/FR; the recipient's wishlist content is **free text and not translated**
  ([NFR-003](../../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)), like everywhere
  else it appears.
- **Privacy:** this use case **is** the amended
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md) in action — account mode
  stores the pairing server-side and protects it by **authorization** (verified by authorization
  tests), not by absence. The privacy copy must state plainly that the operator can technically read
  the pairings ([ADR-0020](../../adr/0020-server-side-assignment-privacy.md)); the account-free path
  keeps its zero-knowledge property untouched.
- **Performance:** reading one's own assignment is a single authorized fetch; the anti-spoiler step is
  a UX gate, not a latency source.

## Open questions

- **Does the in-app reveal reuse the exact anti-spoiler interaction of the reveal page, or a lighter
  one?** Recommendation: reuse the same deliberate-reveal pattern for consistency and for the same
  shared-screen reason it exists on the reveal page. Confirm in the feature spec and re-verify on
  screen (the reveal-page defects that only showed up in a browser are the precedent).
- **Should the assignment view show the giver-side view only, or also confirm the member is a
  recipient?** By construction every member is exactly one giver and one recipient; the view is
  giver-side ("who I give to"). No open modelling question — noted only to fix the wording.

## Traceability

- Requirement: [FR-029](../../requirements/functional/FR-029-see-my-assignment.md); governed by
  [NFR-002](../../requirements/non-functional/NFR-002-assignment-privacy.md)
- ADRs: [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) (server-side storage +
  authorization rule), [ADR-0019](../../adr/0019-member-user-linking.md) (what a linked member is),
  [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (anti-spoiler UX, account-free path
  it partly supersedes)
- Related use cases: [UC-SS-010](UC-SS-010-link-member-to-account.md),
  [UC-SS-006](UC-SS-006-open-reveal-link.md), [UC-SS-012](UC-SS-012-edit-my-wishlist.md),
  [UC-MAIN-015](../main/UC-MAIN-015-sync-across-devices.md)
- Glossary: [Assignment](../../glossary/assignment.md), [Member](../../glossary/member.md),
  [User](../../glossary/user.md), [Reveal link](../../glossary/reveal-link.md)
- Origin: [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md),
  decision D1
