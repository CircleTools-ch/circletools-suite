---
term: Assignment
contexts: [Secret-Santa]
status: Draft
---

# Assignment

## Definition

A single giver→recipient pairing produced by a [Draw](draw.md): one [Member](member.md) gives a
gift to exactly one other member. Each member is a giver once and a recipient once.

## Bounded context(s)

- **Secret-Santa:** the private result each giver sees ("you give to X"). The full mapping is never
  revealed to the group. How a giver receives it depends on the mode
  (see [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md), amended by
  [ADR-0020](../adr/0020-server-side-assignment-privacy.md)):
  - **Account-free mode** — delivered only through that giver's [Reveal link](reveal-link.md) and
    **not stored on any server**. This is the pre-v2 statement, and it still holds for account-free
    groups.
  - **Account mode (M4)** — a **linked** giver reads their own pairing in the app, signed in on any
    device. The pairing is then **stored server-side** (giver, recipient, group — nothing more) and
    protected by **authorization**: a request for anyone else's is denied, not filtered. The operator
    can technically read it, which is stated plainly in the UI rather than described as "secure".

## Synonyms / do-not-use

- **Synonyms:** pairing, match.
- **Do NOT use:** "target" (unfriendly), "gift" (that's the present, not the pairing).

## Product display terms (reference only — not code names)

| Language | Term |
|----------|------|
| DE | Zuteilung |
| EN | Assignment |
| FR | Attribution |

## Related terms

- [Draw](draw.md), [Member](member.md), [Reveal link](reveal-link.md)

## First appearance

- [UC-SS-001](../use-cases/secret-santa/UC-SS-001-create-group.md)
