---
id: FR-047
title: Account-deletion farewell email
type: functional
status: Implemented
priority: Should
source: ADR-0064
verification: test
related: [FR-015, FEAT-018, ADR-0064, NFR-003, NFR-002]
---

# FR-047 — Account-deletion farewell email

## Description

When a user deletes their account ([FR-015](FR-015-delete-account.md)), and
**only once the deletion has actually succeeded**, the system sends **one** confirmation ("farewell")
email to the address the account used. The mail:

- confirms the account and everything tied to it has been removed, with no hidden copy kept;
- thanks the person and makes clear they are welcome back;
- carries **no action link** and **no [assignment](../../glossary/assignment.md)** or any other content —
  there is nothing to act on ([NFR-002](../non-functional/NFR-002-assignment-privacy.md));
- is localized **DE / EN / FR** in the account's preferred language
  ([NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md));
- is branded in the identity of the **source app** the deletion was requested from, like every other
  transactional mail ([ADR-0064](../../adr/0064-transactional-email-html-and-per-app-branding.md)).

The mail is **self-contained**: its content is captured **before** the account row is removed and queued
through the durable outbox, so rendering it never depends on the deleted account still existing.

## Rationale

Deletion today is silent. A one-time, warm confirmation closes the loop honestly — the person has proof
their data is gone (which the privacy and deletion pages promise) and a friendly door left open — at
effectively no cost, since the mail plumbing already exists. It is deliberately link-less: there is
nothing to confirm or undo, and a link would only invite phishing look-alikes.

## Acceptance criteria

```gherkin
Given I am signed in and delete my account, re-entering my password
When the deletion completes successfully
Then exactly one farewell email is queued to my account address
And its body contains no link and no assignment information
And it is rendered in my account's preferred language (DE/EN/FR)

Given the deletion does not succeed (e.g. it is blocked by a guard or fails)
When the request returns
Then no farewell email is queued

Given I deleted my account from the Secret-Santa app
When the farewell email is rendered
Then it is branded as Secret Santa; deleting from CircleTools is branded as CircleTools
```

## Dependencies & related

- Extends: [FR-015](FR-015-delete-account.md) (delete the account)
- Implemented by: [FEAT-018](../../features/FEAT-018-branded-transactional-emails.md) /
  [TP-018](../../features/TP-018-branded-transactional-emails.md)
- Governing ADR: [ADR-0064](../../adr/0064-transactional-email-html-and-per-app-branding.md)
- Related: [NFR-002](../non-functional/NFR-002-assignment-privacy.md) (no assignment in mail),
  [NFR-003](../non-functional/NFR-003-multilingual-de-en-fr.md) (DE/EN/FR)
