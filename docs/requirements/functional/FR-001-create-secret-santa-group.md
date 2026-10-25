---
id: FR-001
title: Create and configure a Secret-Santa group offline
type: functional
status: Implemented
priority: Must
source: UC-SS-001
verification: test
related: [UC-SS-001, NFR-001, NFR-003]
---

# FR-001 — Create and configure a Secret-Santa group offline

## Description

The Secret-Santa app must let a user create a [Group](../../glossary/group.md) with a name and,
optionally, a [Budget](../../glossary/budget.md) (amount + currency) and
[Event details](../../glossary/event-details.md) (date, location, notes), and persist it to local
storage **without requiring an account or network connection**. Budget and event details must be
editable after creation.

## Rationale

Local-first is a core quality goal (Q1): the app must be fully usable offline. The group is the
entry point to every other Secret-Santa flow, and it owns the budget and event context that the
draw and reveal later carry to each giver.

## Acceptance criteria

```gherkin
Given the app is offline and the user has no account
When the user creates a group with a non-empty name
Then the group is saved to local storage
And it is immediately visible in the user's list of groups

Given the user is creating or editing a group
When they set a budget amount and currency and valid event details
Then the budget and event details are saved with the group

Given the user tries to create a group with an empty name
When they attempt to save
Then saving is blocked and the user is prompted to enter a name

Given the user enters a negative or non-numeric budget amount
When they attempt to save
Then saving is blocked and the user is prompted to enter a valid amount
```

## Amendment (2026-10-16) — group actions and starting from a template

From the [2026-10-16 two-account test](../../brainstorming/2026-10-16-secret-santa-two-account-device-test.md) (Q34, T1, Q23):

- [ ] The group menu — on the list and on the group page — offers Rename, Pin/Unpin (signed in), Sharing &
      sync, **Use as template for a new group**, then Delete, set apart at the end. A joined group offers
      Sharing & sync and Leave group instead.
- [ ] "Use as template" opens the create sheet prefilled ("Family (new)"), says what comes along, and creates
      a new group with the **members (and their gift notes) and exclusions** copied; events, wishlists,
      draws and account links are not.
- [ ] "Take members from a circle" (FR-027) is offered only inside *Create group*, and only when the account
      has a circle with two or more people. Without circles, the circle page links to the web instead of
      naming an app that does not exist on the phone.
- [ ] Signed in with no groups, the list shows the same welcome card as signed out, without the account sell,
      plus the groups the account takes part in and its invitations, when there are any (amended for
      ADR-0086: *My assignments* folded into the group list).

## Dependencies & related

- Derived from: [UC-SS-001](../../use-cases/secret-santa/UC-SS-001-create-group.md)
- Related: [NFR-001](../non-functional/NFR-001-local-first-offline.md), [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md)
- Related quality goal: Q1 (local-first) — [arc42 ch. 1](../../arc42/01-introduction-and-goals.md)
- Glossary: [Group](../../glossary/group.md), [Budget](../../glossary/budget.md), [Event details](../../glossary/event-details.md)
