---
status: Accepted
date: 2026-10-16
deciders: [Adrian]
tags: [architecture, privacy, security, sync, secret-santa, sharing]
---

# ADR-0080 — A participant sees only their own pairing, edits only their own entry, and never publishes

## Context and problem statement

[ADR-0058](0058-account-free-link-shared-groups.md) lets a person **join** a shared-by-link group with
the join link. It promises that a joined participant *"sees the group, edits **their own** wishlist /
gift note, and sees **their own** assignment once drawn — never others'."* The
[2026-10-16 two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md)
showed the implementation keeps none of the three promises:

- **Q2:** `GET /shared/{token}` returns the **whole** group document to a join-token holder, draw
  history and everyone's wishlists included, and the participant's device stores it. Anyone in the
  group can read who drew whom with the browser's developer tools.
- **Q1:** on *Sharing & sync* a participant could turn the joined group into an editable local copy
  ("On this device"), after which *Share links* listed every member's reveal link — the secret was
  broken through the normal UI. The prompt promised that the online copy is removed; nothing was.
- **Q5:** a participant cannot edit their own wishlist anywhere. The S6 fix made the whole group
  read-only for join-only devices; no write path for the participant's own data exists. Choosing
  "Keep it on my account" at join did not make the group follow the account to its other devices.
- **Q3/Q4:** every device publishes the [linking projection](0042-events-as-first-class-entities.md)
  (event registry and assignment rows, FEAT-005) for **every** local group, joined ones included. The
  two endpoints (`PUT /groups/{id}/events`, `POST /groups/{id}/assignments/publish`) only require a
  signed-in caller. The participant's rows collide with the organizer's primary key → HTTP 500 on every
  cycle; had the participant published first, its rows would have won.
- The **device handle** sent with a device-only claim is the sync state's `DeviceId`, which a device that
  never saved sync state re-mints on every read. It cannot prove later that this device made the claim.

**How does a participant's device get exactly its own pairing and its own editable entry, how does the
server know which member a request speaks for, and who may publish a group's linking projection?**

## Decision drivers

- **The secret must hold against people in the group**, without relying on them not opening dev tools
  ([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md), weakened by ADR-0058 only to
  the *claim-someone-else-first* insider risk — not to "everyone holds the draw").
- **Participants participate:** own wishlist per event and own gift note, editable, reaching the organizer
  and the giver.
- **Keep the share-token model** (no account needed to join) and **reuse** the claim table, the
  token-at-rest protection ([ADR-0061](0061-share-token-and-creator-key-at-rest-protection.md)) and the
  existing sync engine.
- **The server stays schema-light:** it already reads the body's `members` array for its size caps; it
  must not grow a second copy of the Secret-Santa domain.
- **Old clients keep working** (installed apps update late): additive response fields, no route removed.
- **Server authorization, not client politeness:** a misbehaving client must be refused by the server.

## Considered options

**A. Where the participant's view is produced**

1. **Client-side filtering** — the server keeps sending the whole document; the app hides what a
   participant shouldn't see. Rejected outright: the data is on the device (Q2 *is* this option).
2. **Organizer-published participant views** — the organizer's device uploads a separate stripped body
   plus per-member slices on every push. Keeps the server opaque, but every participant view is only as
   fresh as the organizer's last sync, and wishlists written by participants need a second inbox.
3. **Server-side redaction on read** — the server trims the stored document per request, based on which
   member the request proves to speak for.

**B. How a request proves which member it speaks for**

1. **The claim's device handle** — breaks: the handle is not stable on a device that never saved sync
   state, and it is also sent to other endpoints.
2. **A server-minted participant key** returned once by the claim, stored hashed, presented in a header;
   **plus** the bearer for an account claim (any device of that account).

**C. How a participant writes their own entry**

1. **Allow join-token pushes of the whole body** with server-side diffing. Lets a participant overwrite
   anything they can't see; hard to bound.
2. **A narrow "my entry" write** (`PUT /shared/{token}/me`): own wishlist per event and own gift note
   only. The server merges it into the stored body and records it as the participant's contribution;
   an admin/owner push re-applies recorded contributions so a stale organizer copy can't undo them.

**D. Who may publish the linking projection**

1. **Anyone signed in** (today).
2. **The organizer only:** the account that owns the group (account sync store, or the owner of a
   shared group), the holder of an active admin token for a shared group, or — for a group the server
   holds nowhere (an "On this device" group) — the first account that published it.

## Decision outcome

**Chosen: A3 (server-side redaction on read), B2 (participant key + account bearer), C2 (narrow
"my entry" write with re-applied contributions), D2 (organizer-only publish).**

### Redacted read (A3)

`GET /shared/{token}` with a **join** token returns the stored body trimmed for the member the request
proves (see B). For that member *M* and their current recipient(s) *R*:

| Body part | Kept for a participant |
|---|---|
| members (id, name, archived) | all |
| `members[].giftNote` | only *M* and *R* |
| `members[].userId`, legacy `members[].wishlist` | only *M* |
| exclusions | all (organizer configuration, not the secret) |
| `events[].draws` | only the **current** draw, with **only *M*'s assignment(s)**; earlier draws removed |
| `events[].carriedForwardBaseline`, legacy group `draws` | removed |
| `events[].wishlists` | only *M*'s and *R*'s |
| budget, event details, names | all |

A request that proves no member (the join page before claiming, or an unknown key) gets the current draw
with **no** assignments, no wishlists and no gift notes — enough to show "drawn" and the roster. Admin
and owner reads stay full. The view gains additive fields: `claimedMemberId` (the member the request was
recognised as), `redacted: true`, and `takenMembers` (`[{memberId, reason}]`, reason *joined* or
*linked/organizer*) for the join page (Q6). The trimming lives in the repository next to the store's
existing size caps, as a small JSON transform over the known property names; the domain model is not
duplicated.

A participant device **always adopts the server body** on reconcile (it never pushes the group), so a
full draw stored before this change is replaced on the first sync after the server update.

### Participant key (B2)

`POST /shared/{token}/claims` returns a **participant key** (256-bit, URL-safe) on a successful claim.
The server stores only its hash, as for share tokens (ADR-0061). The device keeps it with the join record
and sends it as `X-Participant-Key` on reads and writes. A claim made **with an account** is also proven
by the account's bearer, on any device of that account. A device that joined before this change exchanges
its legacy device handle for a key once (`POST /shared/{token}/claims/{memberId}/key`), accepted only if
it matches the stored handle; otherwise the organizer releases the person and they re-join.

`GET /shared/joined` (bearer) lists the shared groups the account has claimed a member in — group id,
join token, member id — so the account's other devices add them as joined groups (Q5).

### "My entry" write (C2)

`PUT /shared/{token}/me` (join token + proof) carries the participant's **own** gift note and per-event
wishlists. The server

1. stores them as the member's **contribution** (one row per group + member);
2. patches the stored body (`members[M].giftNote`, `events[e].wishlists[M]`) and bumps the version;
3. on every later **admin or owner push**, re-applies all recorded contributions to the incoming body
   and returns `rewritten: true`, so the pushing client pulls the merged version.

Releasing the person (admin) or the participant leaving (`DELETE /shared/{token}/claims/me`, Q1) deletes
the claim **and** the contribution; the organizer can then edit that member's fields again. While a
member is claimed, the organizer's app shows that member's wishlist and gift note read-only ("Ben keeps
this himself").

### Organizer-only linking publish (D2)

`PUT /groups/{id}/events` and `POST /groups/{id}/assignments/publish` accept the caller only if it is the
group's organizer:

- the account whose **account sync store** holds the group; or
- the **owner account** of a shared group; or
- the caller presents the shared group's **active admin token** (`X-Admin-Token`); or
- for a group held in neither store, the account that **first** published it (rows already recorded for
  another organizer → refused).

Anyone else gets **403**, never 500. A verified owner that finds rows of another organizer replaces them.
The client publishes only groups this device organizes (local, account-store, admin-link or owner) and
skips join-only groups (Q4).

### UI consequences (Q1)

A participant's *Sharing & sync* shows no storage choice — only "You joined as Ben" and **Leave group**.
The client refuses any mode change of a join-only record. The group menu offers *Leave group* instead of
*Delete*.

## Consequences

- **Positive:**
  - A participant's device never holds anyone else's pairing, wishlist or gift note; the UI path (Q1) and
    the storage path (Q2) are both closed.
  - Participants can maintain their own wishlist and gift note; the organizer and the giver see it after
    the next sync, without the organizer having to open the app first.
  - The linking projection can only be written by the organizer; the per-cycle 500s disappear.
  - The claim gets a real, stable proof; the account's other devices find their joined groups.
- **Negative / trade-offs:**
  - The server now interprets more of the body (draws, wishlists, gift notes) — still a narrow, named
    set of properties, but a schema change there must update the redactor and the contribution merge.
    Covered by tests that round-trip a real `GroupState`.
  - Organizer edits to a claimed member's wishlist/gift note are overridden by the participant's
    contribution (by design: the participant owns those fields).
  - A participant sees only their recipient's wishlist, no longer everyone's.
  - One more secret on the device (the participant key) — same storage and protection as the join token.
- **Follow-ups:**
  - Amend [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md): a participant's device
    receives only its own pairing.
  - ✅ Glossary: [participant key](../glossary/participant-key.md),
    [participant contribution](../glossary/participant-contribution.md); [person claim](../glossary/person-claim.md) amended.
  - Server deploys **before** the apps. Old apps keep working: they get the trimmed body (their
    "Your assignment" disappears until they update and obtain a key) and cannot publish another
    organizer's linking rows.
  - Feature spec + test plan for WP-1 (FEAT-024 / TP-024).
- **New risks:**
  - A redaction bug leaks data again — mitigated by server tests asserting the trimmed body for
    organizer / participant / unproven reader, and a client test that a join-only record never stores
    another member's assignment.
  - Insider risk of ADR-0058 (claiming someone else first) is unchanged; Q6 (taken members disabled)
    narrows it.

## Pros and cons of the options

### A1 — Client-side filtering
- 👍 No server work.
- 👎 The data is on the device; this is the defect itself.

### A2 — Organizer-published participant views
- 👍 Server stays fully opaque.
- 👎 Freshness depends on the organizer's device; participant writes need a separate inbox the organizer
  merges; more moving parts.

### A3 — Server-side redaction *(chosen)*
- 👍 Always current; one place to enforce; old clients get the safe view automatically.
- 👎 The server reads a few more body properties.

### B1 — Device handle as proof
- 👍 Already sent.
- 👎 Not stable; not a secret.

### B2 — Participant key + bearer *(chosen)*
- 👍 Stable, unguessable, stored hashed; works signed out; accounts work across devices.
- 👎 One more secret to keep; legacy exchange needed once.

### C1 — Participant pushes the whole body
- 👍 Reuses the push route.
- 👎 A participant could overwrite what they cannot even see.

### C2 — Narrow "my entry" write *(chosen)*
- 👍 Bounded write; contributions survive stale organizer pushes.
- 👎 The server merges two named fields.

### D1 — Anyone signed in may publish
- 👎 Collisions (500), forged rows.

### D2 — Organizer only *(chosen)*
- 👍 Correct owner of the projection; clean 403.
- 👎 A local-only group relies on "first publisher" — acceptable, as only the organizer's device holds it.

## Links

- Related ADRs: [0020](0020-server-side-assignment-privacy.md), [0042](0042-events-as-first-class-entities.md),
  [0058](0058-account-free-link-shared-groups.md) (promises enforced here), [0061](0061-share-token-and-creator-key-at-rest-protection.md),
  [0068](0068-live-reveal-links-for-backend-backed-groups.md), [0078](0078-my-account-groups-record-their-account.md).
- arc42 chapters: 08 Cross-cutting concepts (sync / storage modes).
- Requirements: [NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md).
- Brainstorming: [2026-10-16 two-account test](../brainstorming/2026-10-16-secret-santa-two-account-device-test.md)
  (Q1–Q6), backlog WP-1.
