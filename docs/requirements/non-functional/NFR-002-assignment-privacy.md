---
id: NFR-002
title: Assignment privacy — each person learns only their own pairing
type: non-functional
status: Implemented
priority: Must
source: UC-SS-004, UC-SS-005, UC-SS-006
verification: test
related: [UC-SS-004, UC-SS-005, UC-SS-006, FR-004, FR-005, FR-006, FR-029, FR-039, NFR-001, ADR-0014, ADR-0020, ADR-0086]
---

# NFR-002 — Assignment privacy — each person learns only their own pairing

> **Rewritten 2026-10-25 for [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)** ([FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)): one way to be in a group. The previous version described the four-kind storage model (on this device, shared by link, my account, my account · shared) and is in the git history.

## Description

A person must be able to learn **only their own** [Assignment](../../glossary/assignment.md). The full giver→recipient mapping
is never shown to the group, not even to the organizer in the app (the draw page says so), though the organizer's
device and, for an online group, the server hold it.

How it is enforced depends on the group's [place](../../glossary/place.md) — **two columns** ([ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md)):

| | On this device *(default)* | Online |
|---|---|---|
| Where assignments live | the organizer's device and the individual reveal links | the group's one copy on the server |
| A person learns their pairing through | their reveal link, which carries exactly one giver's page | their participant page, or their reveal link, which the server answers with one giver's page |
| Enforced by | **absence** — no server holds them | **authorization** — only the owner reads the document; a participant reads only their own view; a reveal token gives one giver's page |
| Trust boundary | none | the operator; and anyone who holds a reveal link or picks someone else's free spot through the join link |
| Verified by | network capture | authorization tests |

What every view of an online group contains, when it is read:

- **Owner:** the whole document — the organizer's own device holds the same.
- **Participant:** per event, their own pairing and that recipient's wishlist and gift note; nothing about other
  pairings or earlier draws.
- **Reveal link:** one giver's page for one event.
- **Join page:** the group's name and which members are free; no pairing.
- **Anyone without a role:** the same "not found" as for a group that does not exist.

## Rationale

The surprise and fairness of a gift exchange depend on each giver seeing only their own recipient. A group on the
device keeps this without any server. An online group buys people their pairing in the app on any device, and pays
by making the operator a trusted party: **the operator can technically read every pairing of an online group**, as
can anyone with database or backup access. This is accepted knowingly (ADR-0020), as the organizer already holds the
complete mapping. Mitigations: store the minimum, never expose another person's pairing through the API, keep all
access secrets hashed, and treat per-person envelope encryption as the recorded future hardening step.

Within the group, secrecy is **trust-bounded**: a person holding the join link could pick someone else's free spot
first and see that pairing. A taken spot is shown as taken and only the organizer can free it, so a wrong pick shows
up when the real person cannot take part.

## Acceptance criteria

```gherkin
Given a set of reveal links of a group kept on this device
When any single link is decoded
Then it exposes exactly one giver's assignment

Given a group kept on this device whose draw has run
When servers and network traffic are inspected
Then no assignment is stored on or sent to a server

Given an online group and a participant
When the participant's device reads the group
Then the response contains only their own pairing per event and their recipients' wishlists and gift notes

Given an online group and a participant
When the participant requests the whole document
Then the request is refused like a group that does not exist

Given an online group's reveal token
When it is read
Then the response is one giver's page for one event

Given an online group's join link
When its page is read
Then it lists the free members and contains no assignment
```

## Quality attribute *(NFR only)*

- **Attribute:** privacy / confidentiality.
- **Metric & target:**
  - a reveal link of a group on this device decodes to exactly **one** giver's assignment;
  - groups on this device: **zero** assignments stored on or sent to a server;
  - online groups: **zero** successful reads of another person's pairing — a participant's response never holds
    more than their own pairing per event, and a request without the read-document permission never returns the
    document.
- **Measurement approach:** decoding tests for device links; network capture for groups on this device;
  authorization tests over real HTTP and Postgres for online groups (TP-027, "Privacy").

## Dependencies & related

- Derived from: [UC-SS-004](../../use-cases/secret-santa/UC-SS-004-run-draw.md),
  [UC-SS-005](../../use-cases/secret-santa/UC-SS-005-share-reveal-links.md),
  [UC-SS-006](../../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Related: [FR-004](../functional/FR-004-run-fair-draw.md), [FR-005](../functional/FR-005-generate-reveal-links.md),
  [FR-006](../functional/FR-006-reveal-page-decode.md), [FR-029](../functional/FR-029-see-my-assignment.md),
  [FR-039](../functional/FR-039-shared-by-link-storage-and-sync.md), [NFR-001](NFR-001-local-first-offline.md)
- Governing ADRs: [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (on this device),
  [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) (the operator as a trusted party), [ADR-0086](../../adr/0086-one-way-to-be-in-a-group.md) (online)
- Implemented by: [FEAT-027](../../features/FEAT-027-one-way-to-be-in-a-group.md)
- Glossary: [Assignment](../../glossary/assignment.md), [Reveal link](../../glossary/reveal-link.md), [Participant](../../glossary/participant.md)
