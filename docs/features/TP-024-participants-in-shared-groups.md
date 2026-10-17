---
id: TP-024
feature: FEAT-024
status: Approved
related: [ADR-0080, ADR-0058, NFR-002]
---

# TP-024 — Test plan for FEAT-024

## Scope & objectives

Verify that a participant of a shared-by-link group receives only their own pairing, can edit only their
own entry, cannot become organizer, and that only the organizer publishes the linking projection.

## Test levels

- **Unit (server)** — the body redactor and the contribution merge over a serialized `GroupState`.
- **Integration / API (server)** — shared-group and linking endpoints against the test host.
- **Unit (client)** — publish filtering, adopt-on-reconcile, participant key handling, self-update queue.
- **UI/component** — participant pages (Sharing & sync, Wishlists, Join), organizer badges and counter.
- **End-to-end / exploratory** — two accounts in two browsers; the Pixel against the local stack.

## Test matrix

| Item | Test case | Level | Automated? |
|------|-----------|-------|------------|
| Q2 | Join read with a valid key: only the member's assignment in the current draw; earlier draws, baseline, legacy draws removed; only own + recipient wishlists and gift notes | Unit + API | ✅ |
| Q2 | Join read without proof: current draw with no assignments, no wishlists, no gift notes | API | ✅ |
| Q2 | Admin and owner reads stay full | API | ✅ |
| Q2 | Account claim proven by bearer on another device | API | ✅ |
| Q2 | Client: a join-only record adopts the server body even at an equal version (full draw replaced) | Unit | ✅ |
| B2 | Claim returns a key; the stored value is a hash; a wrong key proves nothing | API | ✅ |
| B2 | Legacy exchange: matching device handle → key; mismatch → refused | API | ✅ |
| Q5 | `PUT /me` patches gift note and own wishlist, bumps the version; refused without proof | API | ✅ |
| Q5 | A stale admin push does not undo a contribution; result says `rewritten` | API | ✅ |
| Q5 | Release / leave deletes claim and contribution | API | ✅ |
| Q5 | `GET /shared/joined` lists the account's claimed groups | API | ✅ |
| Q5 | Participant Wishlists page shows only the own entry, editable, autosaved | Component | ✅ |
| Q1 | Participant Sharing & sync has no storage options, shows Leave group; a mode change of a join-only record is refused | Component + Unit | ✅ |
| Q3 | Linking publish: organizer (account store / owner / admin token / first publisher) allowed; participant and stranger 403; no 500 on collision | API | ✅ |
| Q4 | Client publishes no join-only group; sends the admin token for shared groups | Unit | ✅ |
| Q6 | View lists taken members with reason; join page disables them; all-taken message | API + Component | ✅ |
| Q7 | Signed in → account option preselected; signed out → sign-in returns to the join page | Component + manual | partly |
| Q20 | Share counter excludes connected members | Component | ✅ |
| Q30 | "Verbunden" badge for linked and joined members | Component | ✅ |
| Q19 | Password inputs are `type=password` with `autocomplete`; Pixel comparison with plain Chrome | Manual | ❌ |
| All | Two-account walkthrough: join, own wishlist, draw, your assignment, leave; console clean | E2E manual | ❌ |

## Test data & environments

- Server tests use the existing API test host; bodies are produced by serializing real `GroupState`s.
- Manual: local Aspire stack, two browser contexts, Pixel 9 Pro over `adb reverse`.

## Localization & accessibility notes

- New strings in DE/EN/FR; walk the participant pages in all three.
- Disabled join choices are announced with their reason.

## Exit criteria

- [x] All automated cases above pass (server: 16 store + 4 API cases; client: 9 sync + 1 publisher case)
- [x] Two-account browser walkthrough clean (screens + console)
- [ ] CI green (runs on the next push)
