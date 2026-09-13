---
id: TP-013
feature: FEAT-013
status: Draft
related: [FEAT-013, UC-SS-017, UC-SS-018, UC-SS-019, UC-SS-020, UC-MAIN-006, FR-038, FR-039, FR-040, FR-041, FR-042, FR-043, NFR-002, NFR-009, NFR-010]
---

# TP-013 — Test plan for FEAT-013

## Scope & objectives

Verify the third storage tier — **Shared by link** — end to end: the per-group mode choice and its
reversible transitions ([FR-038](../requirements/functional/FR-038-per-group-storage-mode.md),
[FR-043](../requirements/functional/FR-043-release-ownership-and-deletion-disposition.md)); the
token-keyed server storage and sync
([FR-039](../requirements/functional/FR-039-shared-by-link-storage-and-sync.md)); the admin- and
join-link capabilities ([FR-040](../requirements/functional/FR-040-admin-link-capability.md),
[FR-041](../requirements/functional/FR-041-join-link-and-person-claim.md)); person-claim locking and
admin release ([FR-042](../requirements/functional/FR-042-release-a-claimed-person.md)); and that the
new tier honours the **amended** assignment-privacy guarantee
([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)) plus the abuse-resistance and
retention NFRs ([NFR-009](../requirements/non-functional/NFR-009-account-free-write-abuse-resistance.md),
[NFR-010](../requirements/non-functional/NFR-010-shared-group-retention-lifecycle.md)).

The load-bearing properties to prove: **On this device still stores nothing server-side**; the join link
is a bearer capability whose secrecy is **trust-bounded but locked** (first claim wins, only an admin
releases); an **account claim retires the admin link but not the join link**; and **release-admin /
deletion never destroys a device's local copy**.

## Test levels

- **Unit** — storage-mode transitions, admin-capability-vs-ownership rules, person-claim first-claim-lock
  and release, token rotation, release-admin re-keying (no I/O).
- **Integration** — the token-keyed store and endpoints against real Postgres via Testcontainers/Aspire;
  capability authorization (admin vs join vs none); claim/release concurrency.
- **Architecture** — dependency-direction / boundary rules (the token-keyed sync sits behind
  `IGroupRepository`; domain stays I/O-free).
- **Contract** — admin vs join capability on the token-authorized endpoints; additive to the account-sync
  API; reveal-link payload unchanged.
- **UI/component** — bUnit: the storage-mode picker, Sharing & sync screen, the join flow, and the
  consequence/retired-link states.
- **End-to-end** — Playwright over the live Aspire stack: join-via-link, claim-admin, release-admin,
  deletion disposition.
- **Exploratory** — abuse/rate-limit probing of the account-free write path; multi-holder concurrent
  edits; device-reinstall recovery via admin release.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-038/AC — mode chosen at creation, default *On this device* | Creating a group without changing the picker yields a local group; picking *Shared by link* uploads it | UI + Integration | ✅ |
| FR-038/AC — reversible switch with consequence prompt | Each switch shows the before→after prompt; confirm performs it, cancel leaves state | UI | ✅ |
| FR-038/AC — downgrade keeps local copy | *Shared/Account → On this device* removes the server copy and keeps the local aggregate (no tombstone) | Integration | ✅ |
| FR-039/AC — token-keyed storage | A shared group is stored keyed by share token, no `UserId`; fetch by token returns the aggregate | Integration | ✅ |
| FR-039/AC — sync across link-holders | Two devices holding the join/admin link converge last-write-wins per aggregate | Integration + E2E | ✅ |
| FR-039/AC — On this device stores nothing | Network capture over a full local flow shows zero group/assignment writes server-side | E2E (network capture) | ✅ |
| FR-040/AC — admin link is a shared capability | Two holders of one admin link can both manage the group | Integration | ✅ |
| FR-040/AC — disable & regenerate | Rotating the admin link stops the old token resolving; the new one works | Integration | ✅ |
| FR-040/AC — admin link account-free-only & retired on claim | After an account claims admin, opening the old admin link shows "<name> has claimed the admin role for this group" | E2E | ✅ |
| FR-041/AC — join identity × persistence | Join as existing member vs new person, × account vs device-only — all four combinations land correctly | UI + Integration | ✅ |
| FR-041/AC — first-claim-lock | A claimed member cannot be re-claimed by another join-link holder; a device claim is not overridden by a later account claim | Unit + Integration | ✅ |
| FR-041/AC — join link survives claim-admin | After an account claims admin, the join link still lets a participant join/see their own assignment | E2E | ✅ |
| FR-041 / NFR-002/AC — joiner sees only own assignment | A claimant's group response carries only their claimed member's assignment, never another's | Integration | ✅ |
| FR-042/AC — admin release | An admin frees a locked member; the join link can then claim that member afresh | Integration + UI | ✅ |
| FR-043/AC — release-admin re-keys & mints admin link | *My account → Shared by link* drops account ownership, mints a fresh admin link, preserves assignments/claims | Integration + E2E | ✅ |
| FR-043/AC — account-deletion disposition | On deletion, each owned shared group is routed hand-off (default when others joined) / keep-on-device / delete; the confirmation enumerates them | Integration + E2E | ✅ |
| FR-043 / NFR-010/AC — deletion never destroys local | After any disposition, the deleting user's device copy is intact | Integration | ✅ |
| NFR-002/AC — no re-claim of a locked person | Re-claim attempt on a locked member is refused until admin release | Integration | ✅ |
| NFR-009/AC — token entropy | Share tokens are ≥128-bit unguessable; sequential/guessed tokens do not resolve | Unit | ✅ |
| NFR-009/AC — rate limits & size caps | Excessive create/write requests are throttled; oversized rosters/wishlists/notes are rejected | Integration | ✅ |
| NFR-010/AC — TTL / expiry & owner-less delete | An inactive shared group expires per policy; any admin-link holder can delete the online copy; local copies survive | Integration | ✅ |

## Test data & environments

- **Postgres via Testcontainers/Aspire** for the token-keyed store and claim state; seed a shared group
  with a mix of *free / device-claimed / account-claimed* members.
- **bUnit** for the mode picker, Sharing & sync, join flow and sheet/retired-link components (MudBlazor
  test context as elsewhere).
- **Playwright/Chromium over the live Aspire stack** (as the existing e2e host,
  [ADR-0045](../adr/0045-browser-driving-e2e-test-host.md)) for join-via-link, claim-admin, release-admin
  and deletion; drive at ~390px to exercise the mobile sheets.
- **Network capture** fixture to assert the *On this device* mode transmits no group/assignment data.
- Fixtures for token rotation and expiry (clock control for the TTL).

## Localization & accessibility notes

- Verify **DE / EN / FR** for the mode picker, Sharing & sync, join flow, consequence sheets and the
  retired-admin-link message ([NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md));
  the browser console stays clean in each locale.
- Sheets and links meet touch-target and focus-visible requirements; the mode chip and hub row are
  reachable and labelled for assistive tech.

## Exit criteria

- [ ] All acceptance criteria above covered by automated tests
- [ ] Coverage target met (see testing guidelines)
- [ ] The *On this device* network-capture check is green (zero server-side writes)
- [ ] CI green
