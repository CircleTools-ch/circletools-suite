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

**How that guarantee is enforced depends on the storage mode**
([ADR-0058](../../adr/0058-account-free-link-shared-groups.md) introduced a third), and the modes
differ in what the server knows:

| | On this device *(default)* | Shared by link | Account |
|---|---|---|---|
| Where assignments live | organizer's local storage + the individual links only | the server, keyed by an **unguessable share token** (no account) | the server, owned by the signed-in account |
| A person sees their own assignment via | their reveal link | **claiming their person** via the join link (then in-app), or a reveal link | in-app, on any signed-in device |
| Enforced by | **absence** — no server to leak them | **capability + first-claim-lock** — a join-link holder claims one person; a claimed person **locks** and can't be re-claimed (only an admin can release it) | **authorization** — the API returns only the caller's own row |
| Trust boundary | none (zero server knowledge) | the family/friend circle sharing the join link, plus the operator | the operator |
| Verified by | network capture | claim-lock + authorization tests | authorization tests |

On this device is the default and is unchanged: **the link is the secret**. Sharing by link or signing
in is optional and additive ([NFR-001](NFR-001-local-first-offline.md)); doing neither preserves the
original zero-server-knowledge property in full.

## Rationale

The surprise and fairness of a gift exchange depend on each giver seeing only their own recipient.
In the account-free flow, keeping assignments off any server and scoping each link to a single giver
preserves that secrecy without accounts or trusted infrastructure.

**Shared-by-link mode** ([ADR-0058](../../adr/0058-account-free-link-shared-groups.md)) trades some of
that secrecy for account-free collaboration: the group (including the draw) lives on the server keyed by
a share token, and a participant who **claims their person** via the join link sees that person's
assignment. Because the join link is a *single shared secret*, secrecy here is **trust-bounded, not
cryptographic** — a holder could claim someone else first and see their assignment. It is accepted
knowingly under the family/friend trust context (the same trust the reveal links assume), bounded by
**first-claim-locks** (a claimed person cannot be re-claimed; only an admin can release them). Stronger
**per-person cryptographic secrecy** (per-person keys ≈ server-backed per-member reveal links) is the
recorded future-hardening step, since a single per-group key in the link cannot keep assignments
per-person-secret when everyone shares the join link.

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
>
> **Further amended** by [ADR-0058](../../adr/0058-account-free-link-shared-groups.md) (2026-09-13),
> which adds the **shared-by-link** mode: assignments then reach account-free join-link participants,
> so "zero assignments stored server-side" holds for **on-this-device** mode only, and secrecy in
> shared-by-link mode is **trust-bounded** (first-claim-locks), not cryptographic.
>
> **Clarified** by [ADR-0080](../../adr/0080-participant-access-to-shared-groups.md) (2026-10-16): the
> trust boundary is *claiming someone else first*, **not** "every participant holds the draw". A
> participant's device receives only the pairing of the member it has proven to be (participant key or
> account), plus that member's and their recipient's wishlist and gift note. A request that proves no
> member receives no pairing at all. Only the organizer publishes the linking projection.

## Acceptance criteria

```gherkin
Given a set of generated reveal links
When any single link is decoded
Then it exposes exactly one giver's assignment and no other member's assignment

Given the organizer is not signed in
And the draw has run and links are generated and shared
When servers and network traffic are inspected
Then no giver→recipient assignment is stored server-side or transmitted to a server

Given a shared-by-link group whose draw has run
And a participant device that joined as member M
When the device reads the group with the join link
Then the response contains only M's assignment of the current draw and no earlier draws
And it contains no wishlist or gift note other than M's and M's recipient's

Given a join-link read that proves no member
When the group is read
Then the response contains no assignment, no wishlist and no gift note

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

Given a shared-by-link group and a person who has claimed member X via the join link
When that person fetches the group
Then the response contains X's own assignment only, never another member's recipient

Given a shared-by-link group in which member X is already claimed
When another join-link holder tries to claim member X
Then the claim is refused until an admin releases X
```

## Quality attribute *(NFR only)*

- **Attribute:** privacy / confidentiality.
- **Metric & target:**
  - each reveal link decodes to exactly **one** giver's assignment (0 leakage of other assignments);
  - **on-this-device mode:** **zero** assignments stored server-side;
  - **shared-by-link mode:** **zero** re-claims of a locked person, and a claimant's group response
    carries **only** their claimed person's assignment (secrecy is trust-bounded, not cryptographic —
    see Rationale);
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
- Amended by: [ADR-0020](../../adr/0020-server-side-assignment-privacy.md), from [brainstorming — accounts, families & sync (v2)](../../brainstorming/2026-07-24-accounts-families-sync.md) decision D1; and [ADR-0058](../../adr/0058-account-free-link-shared-groups.md) (shared-by-link mode), from [brainstorming — storage & sharing tiers](../../brainstorming/2026-09-13-storage-and-sharing-tiers.md)
- **Where the amendment becomes visible:** [FR-025](../functional/FR-025-claim-local-groups.md)'s
  claim screen — the first moment a completed draw leaves the device is **M3**, not M4, so the screen
  must say what it uploads before it uploads it;
  [FR-026](../functional/FR-026-remove-group-from-account.md) is the way back off the server
- Governing ADRs: [ADR-0014](../../adr/0014-reveal-link-encoding-and-privacy.md) (on-this-device mode), [ADR-0020](../../adr/0020-server-side-assignment-privacy.md) (account mode), [ADR-0058](../../adr/0058-account-free-link-shared-groups.md) (shared-by-link mode)
- Glossary: [Assignment](../../glossary/assignment.md), [Reveal link](../../glossary/reveal-link.md), [Draw](../../glossary/draw.md), [User](../../glossary/user.md), [Sync](../../glossary/sync.md)
