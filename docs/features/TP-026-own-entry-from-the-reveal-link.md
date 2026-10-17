---
id: TP-026
feature: FEAT-026
status: Reviewed
related: [FR-050, UC-SS-006, NFR-002, NFR-009, ADR-0084]
---

# TP-026 — Test plan for FEAT-026

## Scope & objectives

Verify that a reveal-link holder can read and change exactly their own gift note and this event's
wishlist, that the organizer's copy becomes read-only and the giver's live link follows, and that nothing
beyond that one entry is reachable through a reveal token.

## Test levels

- **Integration** — `SharedGroupStore` against the test database: read/write by reveal token.
- **UI/component** — bUnit: the reveal-page editor and the organizer's read-only state.
- **End-to-end / exploratory** — browser: organizer + holder + giver in three contexts; Pixel 9 Pro.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-050 read | Token with member/event → current gift note + this event's items (from the body) | Integration | ✅ |
| FR-050 write | Write → entry stored, body updated, version bumped; other events' wishlists of the entry kept | Integration | ✅ |
| FR-050 scope | Write touches only the token's member and event | Integration | ✅ |
| FR-050 linked | Member linked to an account → read 404, write refused | Integration | ✅ |
| FR-050 legacy slice | Slice without member/event → 404 | Integration | ✅ |
| FR-050 size | Oversized entry → 413 | Integration | ✅ |
| FR-050 ownership | Claims answer lists the member as self-maintained; release deletes the entry | Integration | ✅ |
| FR-050 UI | Live link shows the editor; typing saves; offline write says "not saved yet" | Component | ✅ |
| FR-050 UI | Snapshot / unreachable link shows no editor and keeps the hint | Component | ✅ |
| BTP-12 | Leaving a changed field offers Undo, which saves the earlier text | Component | ✅ |
| FR-050 E2E | Holder edits → organizer read-only → giver's link shows it after sync | E2E manual | ✅ 2026-10-17 |

## Test data & environments

Local Aspire stack; a Shared-by-link group with a current draw.

## Localization & accessibility notes

New strings in DE/EN/FR (`LocalizationParityTests`); the editor fields have labels; status is `role=status`.

## Exit criteria

- [x] All acceptance criteria covered by automated tests (the E2E walk is manual)
- [ ] Coverage target met (see testing guidelines)
- [ ] CI green
