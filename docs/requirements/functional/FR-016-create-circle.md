---
id: FR-016
title: Create a circle with a user-chosen label
type: functional
status: Approved
priority: Must
source: UC-MAIN-008
verification: test
related: [UC-MAIN-008, NFR-003, NFR-004, NFR-006, ADR-0018, ADR-0008]
---

# FR-016 — Create a circle with a user-chosen label

## Description

A signed-in [User](../../glossary/user.md) with a **verified** email address must be able to create a
[Circle](../../glossary/circle.md) by giving it a **name** and a **label**.

- The **name** identifies the circle to its members (*"Müller"*, *"Tuesday climbing crew"*) and is
  required.
- The **label** is the word the UI uses for this circle. It is pre-filled with the default for the
  creator's current language — *Family* / *Familie* / *Famille* — is freely editable, and is stored as
  **user data**: it is never re-translated and does not change when a viewer switches language.

The creator becomes the circle's single **owner**
([membership](../../glossary/circle-membership.md) in the `owner` role) in the same operation that
creates the circle. A user may create and belong to several circles.

## Rationale

The name/label split is [ADR-0018](../../adr/0018-circle-and-sharing-model.md)'s decision D6 made
concrete: `Circle` is honest in the database, and the warmth lives in a label that varies per circle,
so a friend-group is never told it is a "family" and a family never sees the word "circle".

Requiring verification is not extra ceremony — it is the existing rule that an unverified account gets
nothing account-scoped ([UC-MAIN-001](../../use-cases/main/UC-MAIN-001-create-account.md)). Creating
the owner membership atomically with the circle exists because an ownerless circle can never be
administered, transferred or deleted by anyone.

## Acceptance criteria

```gherkin
Given a signed-in user with a verified email address
When they create a circle with a name and a label
Then the circle exists with that name and label
And the creator holds the only membership, in the owner role

Given a user creating a circle in German
When the label field is shown
Then it is pre-filled with "Familie"
And the same field is pre-filled with "Family" in English and "Famille" in French

Given a circle labelled "Familie"
When a member whose language is French views it
Then the label still reads "Familie"

Given a user creating a circle
When they clear the label field and confirm
Then the default label for their language is stored
And the circle never has an empty label

Given a user creating a circle
When the name is empty or whitespace only
Then creation is refused and the name is reported as required

Given a signed-in user whose email address is not verified
When they attempt to create a circle
Then creation is refused with the reason
And a way to resend the verification mail is offered

Given a user who already owns one or more circles
When they create another
Then it is created independently, with its own name, label and membership

Given circle creation that fails partway
When the failure occurs
Then neither a circle nor a membership remains
```

## Dependencies & related

- Derived from: [UC-MAIN-008](../../use-cases/main/UC-MAIN-008-create-circle.md)
- Related: [FR-017](FR-017-invite-to-circle.md), [FR-019](FR-019-manage-managed-members.md),
  [FR-020](FR-020-manage-circle-roles-and-membership.md),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md),
  [NFR-006](../non-functional/NFR-006-circle-authorization.md)
- Governing ADRs: [ADR-0018](../../adr/0018-circle-and-sharing-model.md),
  [ADR-0008](../../adr/0008-entity-id-strategy.md)
- **Open for the M2 feature spec:** per-user circle cap and creation rate limit (abuse limits, not
  product limits).
- Glossary: [Circle](../../glossary/circle.md),
  [Circle membership](../../glossary/circle-membership.md), [User](../../glossary/user.md)
