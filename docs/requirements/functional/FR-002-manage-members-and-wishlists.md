---
id: FR-002
title: Manage members and their wishlists
type: functional
status: Implemented
priority: Must
source: UC-SS-002
verification: test
related: [UC-SS-002, FR-001, NFR-001]
---

# FR-002 — Manage members and their wishlists

## Description

The Secret-Santa app must let a user add, edit, and remove [Members](../../glossary/member.md) of a
[Group](../../glossary/group.md) and maintain each member's
[Wishlist](../../glossary/wishlist.md) (an ordered set of free-text gift ideas), persisting all
changes to local storage **without an account or network connection**.

## Rationale

Members are the givers and recipients of the draw; their wishlists provide the gift guidance that
the reveal delivers to each giver. Managing them offline keeps the local-first promise (Q1).

## Acceptance criteria

```gherkin
Given a group exists
When the user adds a member with a non-empty display name
Then the member is saved to the group in local storage

Given the user adds a wishlist item to a member
When they save
Then the item is stored on that member's wishlist and shown in order

Given the user tries to add a member with an empty name
When they attempt to save
Then saving is blocked and the user is prompted for a name

Given a member is referenced by one or more exclusions
When the user removes that member
Then the member and the affected exclusions are removed together
And the user is informed that those exclusions were removed
```

## Amendment (2026-10-16) — errors in place, autosave

From the [2026-10-16 two-account test](../../brainstorming/2026-10-16-secret-santa-two-account-device-test.md) (Q10, Q29):

- [ ] A duplicate name keeps the add-member sheet open, shows the error under the field with a suggestion
      ("Anna B."), keeps the text selected and the keyboard up. The sheet stays open between names ("Dora
      added — next name?") until *Done*. A rename shows its error under the field being edited.
- [ ] The organizer's wishlists page saves as you type and on leaving a field — no Save buttons — with a small
      "Saved ✓"; a failed save says so and keeps the text.

## Dependencies & related

- Derived from: [UC-SS-002](../../use-cases/secret-santa/UC-SS-002-manage-members.md)
- Related: [FR-001](FR-001-create-secret-santa-group.md), [FR-003](FR-003-define-exclusions.md), [NFR-001](../non-functional/NFR-001-local-first-offline.md)
- Glossary: [Member](../../glossary/member.md), [Wishlist](../../glossary/wishlist.md), [Group](../../glossary/group.md), [Exclusion](../../glossary/exclusion.md)
