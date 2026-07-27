---
status: Accepted
date: 2026-07-27
deciders: [Adrian]
tags: [architecture, privacy, security, secret-santa, sync]
---

# ADR-0020 — Server-side assignment storage & privacy

## Context and problem statement

This is the one v2 decision that **removes a guarantee** instead of adding one.

The MVP promises that no [Draw](../glossary/draw.md) result is stored on any server: assignments
exist only in the organizer's local storage and inside individual
[reveal links](../glossary/reveal-link.md) — *"the link is the secret"*
([ADR-0014](0014-reveal-link-encoding-and-privacy.md)).
[NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) states it as a **measurable**
target — "zero assignments stored server-side", verified by network capture.

v2 lets a signed-in [Member](../glossary/member.md) see their own
[Assignment](../glossary/assignment.md) in the app on any device
([ADR-0019](0019-member-user-linking.md)). That is the main reason anyone would log in. But it
requires the assignment to reach a device that has no link — which means it has to pass through, and
therefore be known to, a server.

**Does the server store draw results, and if so, who can read them?**

## Decision drivers

- **The published promise is explicit and measurable.** Changing it must be a deliberate, written
  decision, not a side effect of shipping sync.
- **The account must do the thing people log in for.** An account that cannot show you your own
  assignment has no reason to exist.
- **The organizer already holds the complete giver→recipient mapping today**, in their own local
  storage. v2 exposes nothing new to *them*. The only genuinely new observer is the **server
  operator** — us, plus anyone with database or backup access.
- Password reset must never destroy a user's data — a family app cannot have "lost your password,
  lost your gift assignment" as a failure mode.
- Account-free mode must keep its original property in full
  ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).

## Considered options

1. **P1 — plaintext + authorization.** Assignments live in Postgres; the API returns only the caller's
   own row.
2. **P2 — hybrid, assignments never leave the client.** Groups, members, wishlists and exclusions
   sync; the draw result stays local and travels only in reveal links, as today.
3. **P3 — per-member envelope encryption.** Each assignment is encrypted to its member; the server
   stores ciphertext only.

## Decision outcome

**Chosen: option 1 — plaintext storage protected by authorization** — with the trade-off stated
openly rather than implied.

### What is stored

The minimum that makes in-app reveal work: for each synced group, the **giver, the recipient, and
the group**. No commentary, no derived data, nothing about the assignment beyond the pairing itself.

### The authorization rule

- A signed-in member may read **only their own** assignment. A request for anyone else's is **denied**
  — not filtered to empty, denied.
- A non-organizer response **never contains more than one assignment**.
- The **full mapping is returned only to the organizer**, who already has it locally.
- Enforcement is server-side, in the API. The client is never the thing that decides what a user may
  see.

### What this changes, deliberately

- **NFR-002 is amended** (already done): the "zero assignments server-side" target now scopes to
  **account-free mode**, where it remains true and network-capture-verifiable. Account mode is
  governed by an authorization target verified by **authorization tests** instead.
- **ADR-0014 is partly superseded** — specifically its claim that assignments are never stored
  server-side. Everything else in ADR-0014 stands unchanged: the encode-only reveal link, the URL
  fragment, the anti-spoiler UX, and the whole account-free flow. ADR-0014 remains **Accepted**,
  because the account-free path still depends on it entirely.

### Stated plainly

**In account mode, the operator can technically read every giver→recipient pairing**, as can anyone
with database or backup access. This is the cost of P1 and we do not pretend otherwise — the privacy
documentation must say it in those words rather than describing the data as "secure".

### Future hardening

**P3 (per-member envelope encryption) is recorded as the hardening step**, to be taken if the
operator-as-trusted-party assumption stops being acceptable — a real user base, a hosting change, or
a regulatory reason. Doing it now would mean either key escrow (which dissolves the guarantee it
buys) or losing a user's assignment on password reset.

## Consequences

- **Positive:** a signed-in member sees their assignment on any device, with no link and no
  reissuing; the mechanism is ordinary authorization that we can test, review and reason about; no key
  management, no escrow, no unrecoverable-data failure mode; account-free users keep the original
  zero-knowledge property in full.
- **Negative / accepted — the operator becomes a trusted party.** Database access, a backup, or a
  compromised server exposes every pairing in every synced group. Mitigations are minimality (store
  only giver/recipient/group), strict server-side authorization, and normal operational security —
  none of which change the fact that the data is readable to whoever holds the database.
- **Negative / accepted — a published promise changed.** Anyone who read NFR-002 before v2 was told
  "zero assignments stored server-side" without qualification. The amendment is recorded in NFR-002,
  in the brainstorming session and here, and the change is scoped so that users who never sign in are
  unaffected.
- **Negative / accepted:** a bug in the authorization rule leaks a real secret — the whole point of
  the app. This is a much sharper failure mode than the account-free design, where there was nothing
  to leak. It is the reason NFR-002 now carries explicit cross-member acceptance criteria.
- **Follow-ups:** the authorization tests required by NFR-002 must exist before M4 ships; the privacy
  policy and any in-app privacy copy must state the operator-readable fact; revisit P3 if the trust
  assumption changes.
- **New risks:** assignments become **retained** data subject to deletion and export obligations
  (revDSG / GDPR) — deleting an account must remove or unlink its assignments, which the M1 deletion
  flow has to cover.

## Pros and cons of the options

### 1. P1 — plaintext + authorization (chosen)
- 👍 Simplest; in-app reveal works everywhere; nothing to lose on password reset; testable with
  ordinary authorization tests.
- 👎 Operator can read every pairing; a single authorization bug is a real leak; changes a published
  promise.

### 2. P2 — hybrid, assignments stay local
- 👍 Keeps the promise literally intact; nothing new to protect.
- 👎 A signed-in member on a new device **cannot see their assignment without their link** — which
  removes the main reason to have an account. Delivers sync that fails at the one thing users want
  from it.

### 3. P3 — per-member envelope encryption
- 👍 Strongest: the operator genuinely cannot read the pairings.
- 👎 Substantial key-management work; password reset means either key escrow (dissolving the
  guarantee) or permanently losing the assignment — an unacceptable failure mode for a family app.
  Parked as future hardening rather than rejected.

## Links

- Related ADRs: **partly supersedes** [ADR-0014](0014-reveal-link-encoding-and-privacy.md);
  [ADR-0019](0019-member-user-linking.md), [ADR-0016](0016-sync-and-conflict-resolution.md),
  [ADR-0021](0021-web-auth-token-model.md), [ADR-0007](0007-persistence-approach.md)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md) §8.8
- Guideline: [security & privacy guidelines](../guidelines/security-privacy-guidelines.md)
- Requirements: [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) (amended by this decision), [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
- Source: [brainstorming — accounts, families & sync (v2)](../brainstorming/2026-07-24-accounts-families-sync.md), decision D1
- Glossary: [Assignment](../glossary/assignment.md), [Reveal link](../glossary/reveal-link.md), [Sync](../glossary/sync.md)
