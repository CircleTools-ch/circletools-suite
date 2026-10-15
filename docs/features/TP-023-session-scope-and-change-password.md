---
id: TP-023
feature: FEAT-023
status: Draft
related: [FR-008, FR-049, NFR-005, ADR-0079]
---

# TP-023 — Test plan for FEAT-023

## Scope & objectives

Verify that signing out ends only the current session, that "Sign out on all devices" and changing the
password end the right sessions, that a wrong current password is answered like a failed sign-in, and that
a client whose session was ended elsewhere signs out cleanly.

## Test levels

- **Unit** — `SessionService` revoke-one / revoke-others against an in-memory chain store.
- **Integration** — the account endpoints against the real Postgres-backed host (`AccountEndpointsTests`
  style).
- **UI/component** — Security page form validation (mismatch, empty).
- **End-to-end** — two browser contexts signed into the same account.
- **Exploratory** — the Security page on the Android head in DE/EN/FR.

## Test matrix

| Requirement / AC | Test case | Level | Automated? |
|------------------|-----------|-------|------------|
| FR-008 / sign out one device | Two sessions; logout with session A's refresh secret → A's refresh fails, B's refresh succeeds | Integration | ✅ |
| FR-008 / old client | Logout without a body revokes every session | Integration | ✅ |
| FR-008 / foreign token | Logout with another user's refresh secret revokes nothing of theirs | Integration | ✅ |
| FR-049 / sign out all | logout-all → both sessions' refresh fails | Integration | ✅ |
| FR-049 / change password | Correct current password → 204; old password fails sign-in, new succeeds; caller's refresh works, other session's fails; notice queued | Integration | ✅ |
| FR-049 / wrong current | Wrong current password → same response as failed sign-in; password unchanged | Integration | ✅ |
| FR-049 / policy | Too-short new password → validation problem; password unchanged | Integration | ✅ |
| FR-008 / ended elsewhere | Refresh refused → client signed out and notice raised | Unit | ✅ |
| FR-049 / UI | Two different new-password entries → mismatch message, no request | Manual | — |

## Test data & environments

- Accounts created per test through `/account/register` + verification, as the existing account tests do.

## Localization & accessibility notes

- Check the Security page, the notice and the email in DE, EN and FR.

## Exit criteria

- [ ] All acceptance criteria covered by automated tests
- [ ] Coverage target met (see testing guidelines)
- [ ] CI green
