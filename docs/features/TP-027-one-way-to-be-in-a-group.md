---
id: TP-027
title: Test plan — One way to be in a group
status: Draft
owner: Adrian
related: [FEAT-027, ADR-0086, NFR-001, NFR-002]
---

# TP-027 — One way to be in a group

## Scope

The Secret-Santa server module (store, access rules, view builders, endpoints), the apps' local store and online
sync, and the pages that create, share, join and read groups ([FEAT-027](FEAT-027-one-way-to-be-in-a-group.md)).

## Levels

| Level | Where | What |
|---|---|---|
| Unit | `SecretSanta.Service.Core.Test` | Participant view, reveal view, overview, entry merge, access rules |
| Integration | `Main.Service.Test` (real Postgres) | Every route; authorization with two or three genuine accounts; concurrency |
| Unit / component | `SecretSanta.LocalStore.Test`, `SecretSanta.Ui.Test` | Record shape, online sync (organizer and participant), pages |
| Architecture | `Architecture.Test` | New module layering; no reference from Secret-Santa server code to the platform's repositories |
| E2E | `tests/E2E` | Create → make online → invite / join → draw → participant reads → reveal link |
| Manual | Browser + Pixel | Every route in; DE/EN/FR; console clean |

## Key scenarios

**Privacy (NFR-002) — first-class assertions**
- A participant's view carries exactly their own pairing per event, never another; a request for another group is
  **403**, not empty.
- A reveal read returns one giver's page only; an unknown or rotated token is **404**, indistinguishable from never
  existed.
- A non-owner can't read or push the document, with or without a join token.

**One way in**
- Join link, email invitation (accept and decline), circle, and own member each create one participant row, and
  the participant reads the same view afterwards.
- A member can't get a second participant; an account can't become a second member in the same group.
- An invitation to an address without an account answers like any other (NFR-005).

**One copy of a member's entry**
- A participant's entry appears in the organizer's next read and in the reveal page at once.
- An organizer push with an older copy of those fields doesn't overwrite them.
- Leaving or being released keeps the content in the document and makes it editable for the organizer.
- A reveal-link entry is refused for a member who has a participant.

**Concurrency**
- Two concurrent entry writes and a push in between: no entry is lost.
- A dirty organizer device pushes before it pulls; its offline edit survives.

**Places and owners**
- A device group never calls the server.
- Make online / keep only on this device; an account takes over a link-owned group, and hands it back.
- Account deletion: owned groups handed over or deleted as chosen; participants released; sync rows gone.

**Roles and permissions**
- Every endpoint is guarded by a permission, not a role name (a test enumerates the endpoints and their required
  permission).
- A role → permission table change alone grants or withdraws an action (exercised with a test-only role).
- Exactly one owner per group: a second owner assignment is refused; take-over and hand-over move it.

**Schema version**
- A push with a lower schema version than stored is refused.

**Limits and retention (NFR-009, NFR-010)** — added with phase 4
- A document past the member limit is refused on create and on push.
- A group no account owns and nobody used for the retention window is deleted; one read in the meantime keeps
  it; a group an account owns is kept.
- The delete screen's count of owned online groups comes from `GET /api/v1/account/owned-groups`.

**Leaving the old model** — added with phase 4
- On first load, the groups a device organized under the old local key become groups on this device; joined
  and join-only copies are left behind; the import runs once and removes the old key.
- An organizer who is also in the roster reads their own assignment (the *My assignment* row).
- The account sync carries the preferences between two devices: an edit converges, a concurrent edit is resolved
  by last-write-wins and counted, a lost response is replayed, a refused local write doesn't advance the cursor.

## Exit criteria

All levels green; the E2E suite passes; the manual walkthrough is recorded in the feature's test record with
screenshots for every route in.
