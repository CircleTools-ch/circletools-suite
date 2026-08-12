---
id: FR-027
title: Create a group from a circle
type: functional
status: Draft
priority: Must
source: UC-SS-009
verification: test
related: [UC-SS-009, UC-SS-010, UC-SS-011, FR-019, FR-022, FR-028, NFR-001, NFR-006, ADR-0018, ADR-0019, ADR-0033, FEAT-005]
---

# FR-027 — Create a group from a circle

## Description

A signed-in user who belongs to a [Circle](../../glossary/circle.md) must be able to create a
Secret-Santa [Group](../../glossary/group.md) **from that circle**, bringing its people in as
[Members](../../glossary/member.md) instead of retyping them.

- The user picks one of the circles they belong to and chooses which of its people to include;
  **everyone is selected by default** (the circle already asserts "these are my people").
- For each chosen person the system copies them into the new group as a member:
  - a circle person **with an account** becomes a **linked** member (an optional
    [User](../../glossary/user.md) reference is set — [ADR-0019](../../adr/0019-member-user-linking.md));
  - a [managed member](../../glossary/managed-member.md), and anyone without an account, becomes an
    **unlinked** named member — a managed member is **never** linked
    ([ADR-0018](../../adr/0018-circle-and-sharing-model.md)).
- Each copied member records the **circle person it originated from**. That reference is read
  defensively: it is never dereferenced into the circle at read time, so a deleted circle cannot make
  the group fail to open.
- The result is a **snapshot**, taken at create time. The group holds its own copy of the people and
  **does not track the circle afterwards** — a person added to or removed from the circle later does
  not change the group. Running the draw freezes the participant list, as it already does for any
  group.
- The group is **account-scoped from creation** and syncs
  ([FR-023](FR-023-sync-across-devices.md)); no separate [claim](../../glossary/claim.md) is needed
  (claiming is for groups that pre-date the account — [FR-025](FR-025-claim-local-groups.md)).
- Two chosen people must **not** produce two members linked to the **same** account; the second is
  brought in unlinked and the user is told.
- From creation onward the group is an ordinary group: budget, event details, exclusions and draw all
  behave exactly as for a hand-made group ([FR-001](FR-001-create-secret-santa-group.md)..[FR-004](FR-004-run-fair-draw.md)).

This requirement realises the carried Secret-Santa halves of
[FR-019](FR-019-manage-managed-members.md) (a managed member copied into a group is unaffected by
their later removal from the circle) and [FR-022](FR-022-delete-circle.md) (a group created from a
later-deleted circle opens unchanged, dangling circle id and all).

## Rationale

Retyping the same family every December is the friction the circle was introduced to remove; a group
created from a circle is the payoff. Doing it as a **copy** rather than a live view is
[ADR-0018](../../adr/0018-circle-and-sharing-model.md)'s decision D2: a completed draw is history and
must stay reproducible, so a circle that changes in January must not silently rewrite a December draw.

Recording the origin circle person — without ever dereferencing it at read time — is what lets the
group survive the circle's deletion and lets FR-019's "unaffected by removal" hold, without the
Secret-Santa domain taking a dependency on the Circles context
([ADR-0019](../../adr/0019-member-user-linking.md)).

## Acceptance criteria

```gherkin
Given a signed-in user who belongs to a circle of six people
When they create a group from that circle
Then a group is created with six members
And every person is selected by default before creation

Given a circle person who has an account
When they are copied into the new group
Then their member is linked to that account

Given a managed member in the circle
When they are copied into the new group
Then their member is unlinked
And no account reference is set for them

Given a group created from a circle
When a person is later added to or removed from that circle
Then the group's members are unchanged

Given a group created from a circle whose draw has since run
When that circle changes
Then the completed draw is unchanged

Given a group created from a circle
When that circle is later deleted
Then the group still opens without error
And the dangling circle reference is not dereferenced

Given a managed member copied into a group
When they are removed from the circle
Then the group's copy of that person is unchanged

Given two circle people that resolve to the same account
When the group is created
Then only one member is linked to that account
And the other is created unlinked with the user informed

Given a group created from a circle
When it is created
Then it is account-scoped and syncs without a separate claim step

Given the circle people picker in German, English and French
When one person is listed
Then the count reads as a singular in each language
```

## Dependencies & related

- Derived from: [UC-SS-009](../../use-cases/secret-santa/UC-SS-009-create-group-from-circle.md)
- Related: [FR-028](FR-028-link-member-to-account.md) (the ad-hoc link this establishes implicitly),
  [FR-019](FR-019-manage-managed-members.md) and [FR-022](FR-022-delete-circle.md) (whose carried
  Secret-Santa halves this closes), [FR-023](FR-023-sync-across-devices.md),
  [NFR-001](../non-functional/NFR-001-local-first-offline.md),
  [NFR-006](../non-functional/NFR-006-circle-authorization.md) (reading the circle to populate the
  picker is authorized per request)
- Governing ADRs: [ADR-0018](../../adr/0018-circle-and-sharing-model.md) (snapshot at draw time,
  managed members), [ADR-0019](../../adr/0019-member-user-linking.md) (optional `UserId`,
  duplicate-link guard), [ADR-0033](../../adr/0033-which-aggregates-sync.md) (the circle itself does
  not sync)
- Implemented by: [FEAT-005](../../features/FEAT-005-m4-linking.md) *(M4 — linking; not yet written)*
- Glossary: [Group](../../glossary/group.md), [Circle](../../glossary/circle.md),
  [Member](../../glossary/member.md), [Managed member](../../glossary/managed-member.md)
