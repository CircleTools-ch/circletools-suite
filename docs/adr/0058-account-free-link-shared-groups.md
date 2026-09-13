---
status: Accepted
date: 2026-09-13
deciders: [Adrian]
tags: [architecture, privacy, sync, secret-santa, sharing, mobile]
---

# ADR-0058 — Account-free, backend-stored, link-shared groups (a third storage tier)

## Context and problem statement

Today a Secret-Santa [Group](../glossary/group.md) has **two** storage modes, and the docs treat them
as a hard binary — *no account ⇒ no server; account ⇒ server sync*:

- **Local-only** — offline, account-free; state lives on the device; sharing is via self-contained,
  client-encoded per-recipient [reveal links](../glossary/reveal-link.md)
  ([ADR-0014](0014-reveal-link-encoding-and-privacy.md),
  [ADR-0015](0015-mvp-client-offline-storage.md),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)).
- **Account-synced** — the whole group aggregate (draw + [assignments](../glossary/assignment.md)
  included) lives in Postgres and syncs across the signed-in [User](../glossary/user.md)'s devices
  ([ADR-0016](0016-sync-and-conflict-resolution.md), [ADR-0020](0020-server-side-assignment-privacy.md),
  [ADR-0033](0033-which-aggregates-sync.md)).

The [glossary](../glossary/sync.md), [arc42 §8](../arc42/08-crosscutting-concepts.md), and
[ADR-0053](0053-backend-free-first-mobile-release.md) all state this binary explicitly: *"Without an
account there is no server, no sync."*

The gap: reveal links already give **recipients** a no-account experience, but there is **no way for a
co-organizer or a participant to see and act on the shared group** without either the organizer's own
device or a full account. The jump from *private-offline* to *shared-collaborative* is also a jump to
*sign up* — friction the [competitor scan](../brainstorming/2026-09-02-secret-santa-competitor-research.md)
found the market punishes (DrawNames' *"joinable by link, no email / no app / no account required"* is
repeatedly praised).

**Should we add a third, account-free storage tier whose group lives on our backend and is reached by a
shareable link — and if so, what does the link grant, and how do we keep the draw non-spoiling without
an authenticated identity?**

This decision was worked through in
[Brainstorming — Storage & sharing tiers (2026-09-13)](../brainstorming/2026-09-13-storage-and-sharing-tiers.md)
(Concluded); this ADR records it. It **knowingly changes a published privacy promise** and therefore
supersedes/amends several settled statements (see Consequences and Links).

## Decision drivers

- **Keep the account-free promise intact and default.** [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)
  must still hold for anyone who does nothing: a group is local-only until the user deliberately
  escalates it.
- **Let a group become multi-person / multi-device without demanding identity.** The headline user value.
- **The draw must stay non-spoiling.** A shared group cannot let everyone see who-gives-to-whom
  ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)).
- **One owner, no ambiguity.** Two people must never both believe they run the draw.
- **Recoverable.** A lost/replaced device or a wrong pick must have a clear, non-destructive fix.
- **Reuse, don't reinvent.** Lean on the existing aggregate-sync machinery, the claim flow, and
  member↔user linking rather than a parallel stack.
- **Bounded operational risk.** Anonymous writes to our Postgres are a spam/cost/abuse surface that
  must be contained.
- **Trust context is a family / friend circle** — the same trust the reveal links already assume; the
  design may rely on it rather than defeating every insider.

## Considered options

The decision has three coupled axes. Options are grouped by axis; the chosen combination is in
**Decision outcome**.

**Axis 1 — does the tier exist at all?**
1. **No — keep the two-tier binary.** Simplest; leaves the market gap open.
2. **Yes — add an account-free, backend-stored, link-shared tier.**

**Axis 2 — what does a shared link grant (authorization model)?**
1. **Single all-access link.** One link = full access. *Fatal for Secret-Santa:* it would show the draw
   to everyone.
2. **Admin link only.** A whole-group link shared only with co-organizers; participants keep per-person
   reveal links. Sidesteps assignment privacy but gives no participant self-service.
3. **Admin link + join link.** Two purpose-built links: an **admin** (organizer) capability and a
   **join** (participant) flow where a joiner identifies as a person in the group.

**Axis 3 — assignment privacy under a shared link.**
1. **Trust-based + first-claim-locks.** A joiner claims a person and sees that person's assignment;
   claiming **locks** the person; recovery is an **admin release**. Relies on the circle's trust.
2. **Per-person cryptographic secrecy.** Each assignment encrypted to a per-person key/sub-link so even
   a link-holder can't read others'. A *single group key in the link fragment does not achieve this*
   (everyone shares the join link), so it needs per-person material ≈ server-backed per-member reveal
   links.

## Decision outcome

**Chosen: Axis 1 → option 2 (add the tier); Axis 2 → option 3 (admin link + join link); Axis 3 →
option 1 (trust-based + first-claim-locks with admin release).** Per-person cryptographic secrecy
(Axis 3 option 2) is recorded as **future hardening**, not v1.

### The three storage modes (per group)

A group is in exactly one of three modes, chosen **at creation** (with **On this device** pre-selected)
and changeable anytime from the group's **Sharing & sync** settings:

| Mode | Where the group lives | Sharing |
|------|----------------------|---------|
| **On this device** *(default)* | local storage / local file (ADR-0015) | per-person reveal links only |
| **Shared by link** | our Postgres, keyed by an **unguessable share token**, **no account** | **admin link** + **join link** (below) |
| **My account** | Postgres, owned by the signed-in account (today's sync) | account ownership; reveal links still available |

Switching modes is a first-class, reversible action with a **short, plain-language consequence** (a
one-line before → after, never legalese). Upgrade uploads the aggregate and mints tokens; downgrade
removes the server copy and stops the links, keeping the local copy (mirrors today's *unclaim* — a
delete of the server copy, **not** a tombstone).

### The two links (Shared-by-link mode)

- **Admin link** — grants the **admin capability** (members, exclusions, budget, run/redo the draw,
  manage sharing). It is a **shared capability**: several co-organizers may hold it at once. It works
  **only while no account owns the group.** An admin can **disable it and mint a fresh one** at any
  time (old token stops resolving). When an account **claims admin** (see below), the admin link is
  **retired** and anyone opening it sees *"<name> has claimed the admin role for this group."*
- **Join link** — lets a person **join as a participant**, and works **with or without an account**.
  Opening it runs a *"who are you in this group?"* flow with two independent choices:
  - **Which person:** claim an **existing** person in the roster, or add a **new** person (yourself).
  - **How remembered:** **account** (durable, cross-device — reuses member↔user linking,
    [ADR-0019](0019-member-user-linking.md), and invitee-chosen promotion,
    [ADR-0043](0043-invitee-chosen-managed-member-promotion.md)), **or just this device** (a
    device-local claim; nothing synced beyond the device).

  A joined participant sees the group, edits **their own** wishlist / gift note, and sees **their own**
  assignment once drawn — never others'.

### Claim-admin (Shared-by-link → My account)

Signing in and **claiming admin** makes that account the **sole admin**, retires the admin link, and
keeps the **join link working**. Only the *admin capability* locks; participants are unaffected. This
reuses the anonymous→account [claim](../glossary/claim.md) shape ([ADR-0017](0017-anonymous-to-account-claim.md),
[ADR-0044](0044-sign-in-reconciliation-and-account-scoped-visibility.md)), extended so a *token-keyed*
group can be claimed to a *user-keyed* one.

### Release-admin (My account → Shared by link)

The reverse transition, so ownership is not a trap: an account owner can **hand the group back to a
shared-by-link group.** The server copy is **re-keyed from the user to a fresh share token**, a new
**admin link is minted**, and the account stops owning it — assignments and existing join-link claims
are preserved (a device-only claim stays a device-only claim; an account claim on *another* member is
untouched). This is consistent with "the admin link works only while no account owns the group": once
the account releases it, the admin link is live again. Available from Group settings like the other
mode switches, and — importantly — as an option during **account deletion** (below).

### Interaction with account deletion (extends ADR-0022)

[ADR-0022](0022-account-deletion-and-data-export.md) deletes an account immediately and, today, deletes
the server copy of the Secret-Santa groups it solely owns (the local device copy survives, so the
*owner's* group simply reverts to **On this device**). With this tier, deleting the server copy would
**strand every other participant** of a shared group (join-link members, device claims). So account
deletion routes **each account-owned group** through a per-group choice, mirroring ADR-0022's existing
"a sole circle owner must transfer or delete the circle first" rule:

- **Hand off as shared by link** — runs *release-admin* above: the group stays online, an admin link is
  minted for whoever continues to organize it, everyone keeps access. **This is the default when the
  group has other joined people.**
- **Keep on this device** — the local copy survives (today's behaviour); the online copy is removed.
  The default when the owner is effectively the only participant.
- **Delete the online copy** — the explicit destructive choice.

The confirmation screen ADR-0022 already requires must therefore enumerate the account-owned shared
groups and their chosen disposition, not just "your data will be deleted."

### Assignment privacy — trust-based + first-claim-locks

Because the join link is a **single shared secret** and lets a holder claim any *not-yet-claimed*
person, a nosy holder could claim someone else first and peek. We accept this under the **trust
context**, bounded by:

- **First claim wins, and it locks.** Once a person is claimed (by an account **or** a device), that
  person **cannot be claimed again** — no automatic override (a device claim is *not* silently
  superseded by a later account claim).
- **Only an admin can release a locked person.** This is the **single recovery mechanism** for a new
  device, a lost device, or a mis-claim: the admin frees the person, after which the join link can
  claim it afresh.

This means **assignments now reach device-only joiners**, not only signed-in users — a real change to
[NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md), which must be amended.

### UI surface

The mode + sharing live in a **Sharing & sync** entry on the group hub (a hub row per
[ADR-0055](0055-event-navigation-mobile-section-hub.md), plus a small **mode chip** by the group name).
Link delivery reuses the existing `Share` seam — native share sheet where available, Copy everywhere,
QR everywhere. The three modes are **shared feature code** ([ADR-0046](0046-suite-packaging-and-app-composition.md))
and present on **all heads** (Web WASM + MAUI); only *mechanics* differ (native share sheet on
mobile/desktop; opening a shared link deep-links into the app on Android, falls back to the browser on
iOS until App Links are wired — [ADR-0053](0053-backend-free-first-mobile-release.md), MNF-10).

## Consequences

- **Positive:**
  - Closes the market gap: co-organizers and participants collaborate on a group **without signing up**;
    a group survives across a user's devices without an account.
  - The privacy **default is unchanged** (local-only, NFR-001 intact) — escalation is deliberate.
  - Reuses aggregate sync (ADR-0016/0033), claim (ADR-0017), and member↔user linking (ADR-0019/0043).
  - One owner is unambiguous; recovery is a single, admin-controlled mechanism.
  - **Ownership is not a trap:** *release-admin* (My account → Shared by link) lets an owner hand a
    group back, and account deletion can keep a group alive for everyone else instead of stranding them.
- **Negative / trade-offs accepted:**
  - **Weakens NFR-002.** Assignments for a Shared-by-link group are readable by whoever holds the join
    link and claims a person first; the guarantee becomes *trust-bounded*, not cryptographic.
  - Adds an **account-free write path** to the backend (abuse/cost surface).
  - New "released person can be re-claimed" state to model, sync, and test.
- **Follow-ups:**
  - ✅ **Done (2026-09-13, on acceptance):** amended [glossary/sync.md](../glossary/sync.md),
    [arc42 §8](../arc42/08-crosscutting-concepts.md) and
    [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) (the "no account ⇒ no
    server" binary and assignment reachability), and added the glossary terms
    [Shared-by-link group](../glossary/shared-by-link-group.md), [Admin link](../glossary/admin-link.md),
    [Join link](../glossary/join-link.md), [Share token](../glossary/share-token.md),
    [Person claim](../glossary/person-claim.md) (release covered there). The narrowing of
    [ADR-0053](0053-backend-free-first-mobile-release.md) ("API only for login/sync") is noted here.
  - **Use cases → requirements → feature spec + test plan:** choose mode at creation; switch mode
    (incl. release-admin, My account → Shared by link); share via admin link; join via join link
    (identity × persistence); claim admin to an account; admin releases a claimed person.
  - **Extend [ADR-0022](0022-account-deletion-and-data-export.md) and its use case
    [UC-MAIN-006](../use-cases/main/UC-MAIN-006-delete-account-and-export-data.md):** account deletion
    offers a per-owned-group disposition (hand off as shared by link — default when others have joined /
    keep on this device / delete online copy), and the confirmation screen enumerates it. Add a note on
    ADR-0022 pointing here.
  - **Anti-abuse & retention NFRs:** share-token entropy, rate limits, size caps, TTL/expiry, owner-less
    delete.
  - Distinguish this from the account-bound **circle** sharing ([ADR-0018](0018-circle-and-sharing-model.md))
    and reconcile the **parked "invite by link/short code"** backlog idea.
- **New risks:**
  - **Insider spoiler** (claim someone else first) — mitigated by locking + trust, not eliminated.
  - **Abuse of the anonymous write path** — mitigated by the anti-abuse NFRs above.
  - **Operator visibility** of assignments already accepted in [ADR-0020](0020-server-side-assignment-privacy.md)
    now extends to token-keyed groups with no account behind them.

## Pros and cons of the options

### Axis 2 — Single all-access link
- 👍 Trivial.
- 👎 Shows the draw to everyone — **unacceptable** for Secret-Santa.

### Axis 2 — Admin link only
- 👍 No assignment-privacy problem (an admin may see all, ADR-0020); simplest safe cut.
- 👎 No participant self-service — the core of the ask; participants still need reveal links.

### Axis 2 — Admin link + join link *(chosen)*
- 👍 Delivers co-organizing **and** participant self-service; matches the mental model ("who are you?").
- 👎 Two links + a join/identity flow to build; forces the Axis-3 privacy decision.

### Axis 3 — Trust-based + first-claim-locks *(chosen)*
- 👍 Simple; matches "claim any user via the join link"; single admin-release recovery path.
- 👎 Weakens NFR-002 to a trust boundary; insider spoiler possible.

### Axis 3 — Per-person cryptographic secrecy
- 👍 Strongest secrecy; closest to today's "the link is the secret".
- 👎 Needs per-person keys/sub-links (a single group key can't work); significant extra build — deferred.

## Links

- Related ADRs: [0014](0014-reveal-link-encoding-and-privacy.md), [0015](0015-mvp-client-offline-storage.md),
  [0016](0016-sync-and-conflict-resolution.md), [0017](0017-anonymous-to-account-claim.md),
  [0018](0018-circle-and-sharing-model.md), [0019](0019-member-user-linking.md),
  [0020](0020-server-side-assignment-privacy.md) (amended reach), [0022](0022-account-deletion-and-data-export.md) (deletion flow extended),
  [0033](0033-which-aggregates-sync.md),
  [0043](0043-invitee-chosen-managed-member-promotion.md), [0044](0044-sign-in-reconciliation-and-account-scoped-visibility.md),
  [0053](0053-backend-free-first-mobile-release.md) (narrowed), [0055](0055-event-navigation-mobile-section-hub.md).
- arc42 chapters: 08 Cross-cutting concepts (sync/storage — to be amended).
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md) (preserved),
  [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md) (to be amended); new
  anti-abuse/retention NFRs to be written.
- Brainstorming: [Storage & sharing tiers (2026-09-13)](../brainstorming/2026-09-13-storage-and-sharing-tiers.md).
