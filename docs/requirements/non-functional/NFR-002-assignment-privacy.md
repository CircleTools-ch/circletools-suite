---
id: NFR-002
title: Assignment privacy — the link is the secret
type: non-functional
status: Implemented
priority: Must
source: UC-SS-004, UC-SS-005, UC-SS-006
verification: review
related: [UC-SS-004, UC-SS-005, UC-SS-006, FR-004, FR-005, FR-006, NFR-001]
---

# NFR-002 — Assignment privacy — the link is the secret

## Description

A member must be able to learn **only their own**
[Assignment](../../glossary/assignment.md). The full giver→recipient mapping is never shown to the
group; only the organizer holds it. Each [Reveal link](../../glossary/reveal-link.md) must carry
exactly one giver's payload; opening it must reveal only that giver's recipient, wishlist, budget,
and event details.

**How that guarantee is enforced depends on the mode**, and the two modes differ in what the server
knows:

| | Account-free mode | Account mode |
|---|---|---|
| Where assignments live | organizer's local storage and the individual links only | additionally in the server database |
| Enforced by | **absence** — there is no server to leak them | **authorization** — the API returns only the caller's own row |
| Verified by | network capture | authorization tests |

Account-free mode is the default and is unchanged: **the link is the secret**. Signing in is optional
and additive ([NFR-001](NFR-001-local-first-offline.md)), and choosing not to sign in preserves the
original zero-server-knowledge property in full.

## Rationale

The surprise and fairness of a gift exchange depend on each giver seeing only their own recipient.
In the account-free flow, keeping assignments off any server and scoping each link to a single giver
preserves that secrecy without accounts or trusted infrastructure.

Account mode buys a real capability — a signed-in member sees their assignment on any device, without
needing their link — and pays for it by making the **operator a trusted party**. Stated plainly:
**in account mode the operator can technically read every giver→recipient pairing**, as can anyone
with database or backup access. This is accepted knowingly (decision D1, option P1) rather than
by accident, on the grounds that the organizer already holds the complete mapping today, so the only
new observer is the operator. The mitigations are: store the minimum (no assignment data beyond
giver, recipient and group), never expose another member's row through the API, and treat
**per-member envelope encryption (option P3) as the recorded future hardening step** should the trust
assumption stop being acceptable.

> **Amended** by [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md),
> decision D1, and recorded architecturally in
> [ADR-0020](../../adr/0020-server-side-assignment-privacy.md). The original requirement stated an
> unconditional *"zero assignments stored server-side"*; that now holds for account-free mode only.
> ADR-0020 **partly supersedes**
> [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) — only its
> "never stored server-side" claim; the reveal-link design itself still stands.

## Acceptance criteria

```gherkin
Given a set of generated reveal links
When any single link is decoded
Then it exposes exactly one giver's assignment and no other member's assignment

Given the organizer is not signed in
And the draw has run and links are generated and shared
When servers and network traffic are inspected
Then no giver→recipient assignment is stored server-side or transmitted to a server

Given the reveal page for one member's link
When it is displayed
Then it never shows the full group mapping or any other member's recipient

Given a synced group whose members A and B are linked to accounts
And member B is signed in
When B requests member A's assignment from the API
Then the request is denied and no part of A's assignment is returned

Given a synced group and a signed-in member who is not the organizer
When that member fetches the group
Then the response contains their own assignment only, never the full mapping
```

## Quality attribute *(NFR only)*

- **Attribute:** privacy / confidentiality.
- **Metric & target:**
  - each reveal link decodes to exactly **one** giver's assignment (0 leakage of other assignments);
  - **account-free mode:** **zero** assignments stored server-side;
  - **account mode:** **zero** successful cross-member assignment reads — every request for an
    assignment other than the caller's own is denied, and no non-organizer response ever contains
    more than one assignment.
- **Measurement approach:** decoding one member's link in tests yields only that member's payload;
  for account-free mode, code/data review plus a network-capture check confirm no server-side
  assignment storage or transmission; for account mode, **authorization tests** assert that a member
  cannot read another member's assignment and that non-organizer responses carry a single assignment.
  Tamper handling is specified in [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md).

## Dependencies & related

- Derived from: [UC-SS-004](../../use-cases/secret-santa/UC-SS-004-run-draw.md), [UC-SS-005](../../use-cases/secret-santa/UC-SS-005-share-reveal-links.md), [UC-SS-006](../../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Related: [FR-004](../functional/FR-004-run-fair-draw.md), [FR-005](../functional/FR-005-generate-reveal-links.md), [FR-006](../functional/FR-006-reveal-page-decode.md), [NFR-001](NFR-001-local-first-offline.md)
- Amended by: [ADR-0020](../../adr/0020-server-side-assignment-privacy.md), from [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md) decision D1
- Governing ADRs: [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (account-free mode), [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) (account mode)
- Glossary: [Assignment](../../glossary/assignment.md), [Reveal link](../../glossary/reveal-link.md), [Draw](../../glossary/draw.md), [User](../../glossary/user.md), [Sync](../../glossary/sync.md)
