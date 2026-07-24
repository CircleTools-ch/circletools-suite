---
term: Member
contexts: [Secret-Santa]
status: Draft
---

# Member

## Definition

A person participating in a [Group](group.md). A member has at least a display name; contact
details and gift preferences are optional. Members are the givers and recipients in a draw.

## Bounded context(s)

- **Secret-Santa:** a participant within one group. The same real person in two groups is two
  members (membership is per group).

## Synonyms / do-not-use

- **Synonyms:** participant.
- **Do NOT use:** "user" (a user is an authenticated account; a member need not be a user).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Teilnehmer:in |
| EN | Member |
| FR | Participant·e |

## Related terms

- [Group](group.md), [Assignment](assignment.md), [Exclusion](exclusion.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md)
