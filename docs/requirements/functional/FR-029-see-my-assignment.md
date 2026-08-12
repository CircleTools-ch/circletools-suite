---
id: FR-029
title: See my assignment while signed in
type: functional
status: Draft
priority: Must
source: UC-SS-011
verification: test
related: [UC-SS-011, UC-SS-010, UC-SS-006, NFR-002, NFR-003, ADR-0020, ADR-0019, ADR-0014, FEAT-005]
---

# FR-029 — See my assignment while signed in

## Description

A signed-in [User](../../glossary/user.md) who is a **linked** [Member](../../glossary/member.md) of a
drawn [Group](../../glossary/group.md) must be able to see **their own**
[Assignment](../../glossary/assignment.md) in the app — their recipient, that recipient's current
[Wishlist](../../glossary/wishlist.md), the [budget](../../glossary/budget.md) and the
[event details](../../glossary/event-details.md) — on **any** device, **without a
[reveal link](../../glossary/reveal-link.md)**.

- The assignment is revealed behind a **deliberate anti-spoiler step**, as on the reveal page, so a
  glance at a shared screen does not ruin the surprise
  ([ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md)).
- The server **authorizes every read** and returns **only the caller's own** pairing. A request for
  any other member's assignment is **denied — not filtered to an empty result, denied**. A
  non-organizer response **never contains more than one assignment**. The client never decides what
  the user may see ([ADR-0020](../../adr/0020-server-side-assignment-privacy.md)).
- A user linked in **several** drawn groups sees each group's assignment, each behind its own reveal
  step.
- When the organizer **re-runs** a draw, the user sees the **current** assignment on the next sync; a
  stale pairing is never shown.
- **Offline**, the last-synced own assignment is readable from the local cache; the authorization
  decision was made server-side when it was fetched and the cache does not re-decide it.
- An **unlinked** member is unaffected: they open their reveal link exactly as today
  ([FR-006](FR-006-reveal-page-decode.md)). In-app reveal is an additional door, not a replacement.

## Rationale

Seeing your own assignment without juggling a link is the single reason most people would sign in, and
the reason assignments reach the server at all
([ADR-0020](../../adr/0020-server-side-assignment-privacy.md), decision D1). It is also the sharpest
new failure mode in the whole suite: the account-free design had nothing to leak, whereas here a bug
in one authorization rule leaks the exact secret the app exists to keep. That is why the rule is
**deny, not filter**, why a non-organizer response is capped at one assignment, and why this
requirement is verified by **authorization tests** with explicit cross-member cases rather than by an
ordinary happy-path test ([NFR-002](../non-functional/NFR-002-assignment-privacy.md)).

The anti-spoiler step is carried over deliberately from the reveal page — the shared-screen risk is
identical, and reusing the interaction keeps one mental model.

## Acceptance criteria

```gherkin
Given a signed-in user linked to a member of a drawn group
When they open that group and complete the reveal step
Then they see their own recipient, that recipient's wishlist, the budget and the event

Given a signed-in linked member
When the assignment view is shown before they act
Then the recipient is hidden behind a deliberate reveal step

Given a signed-in user who is not the organizer
When the API receives a request for another member's assignment
Then it is denied
And no response ever contains more than one assignment for a non-organizer

Given a user linked in three drawn groups
When they open their assignments overview
Then each group offers its own assignment behind its own reveal step

Given a group whose draw has not run
When a linked member opens it
Then no assignment is shown and no assignment is stored

Given the organizer re-runs the draw
When the linked member next syncs
Then they see the new assignment
And never the previous one

Given a linked member who has synced their assignment
When the device goes offline
Then their own assignment is still readable
And no other member's assignment is present on the device

Given a member who is unlinked from a drawn group
When they had been seeing it in-app
Then the in-app assignment disappears without error

Given the assignment view in German, English and French
When it is shown
Then its labels are localized
And the recipient's free-text wishlist is displayed untranslated
```

## Dependencies & related

- Derived from: [UC-SS-011](../../use-cases/secret-santa/UC-SS-011-see-my-assignment.md)
- Related: [FR-028](FR-028-link-member-to-account.md) (what makes a member linked),
  [FR-030](FR-030-edit-my-wishlist.md) (the wishlist shown here),
  [FR-006](FR-006-reveal-page-decode.md) (the account-free path this parallels),
  [NFR-002](../non-functional/NFR-002-assignment-privacy.md) — this requirement is the amended NFR-002
  in action, [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md)
- Governing ADRs: [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) (server-side storage +
  authorization rule), [ADR-0019](../../adr/0019-member-user-linking.md) (what a linked member is),
  [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (anti-spoiler UX; partly superseded
  for account mode)
- Implemented by: [FEAT-005](../../features/FEAT-005-m4-linking.md) *(M4 — linking; designed, `Draft`)*, verified by [TP-005](../../features/TP-005-m4-linking.md)
- Glossary: [Assignment](../../glossary/assignment.md), [Member](../../glossary/member.md),
  [User](../../glossary/user.md), [Reveal link](../../glossary/reveal-link.md)
