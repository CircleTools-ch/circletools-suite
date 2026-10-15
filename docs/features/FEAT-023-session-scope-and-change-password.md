---
id: FEAT-023
title: Session scope (sign out this device / all devices) and change password
status: In progress
owner: Adrian
target-milestone: Launch readiness
bounded-context: Identity (Main)
related: [UC-MAIN-002, FR-008, FR-009, FR-049, NFR-005, ADR-0079, ADR-0021, TP-023]
---

# FEAT-023 — Session scope (sign out this device / all devices) and change password

## Problem & goals

- **Problem:** signing out on one device ended every session of the account, silently
  ([2026-10-15 sync test](../brainstorming/2026-10-15-secret-santa-sync-device-test.md), S9). There was no
  way to change a password while signed in (D10).
- **Goals:** "Sign out" ends this device's session. "Sign out on all devices" and "Change password" are
  explicit actions on the Security page. A session ended elsewhere is noticed and explained.
- **Non-goals:** a list of individual sessions/devices with per-device sign-out; setting a first password
  for a passwordless account (every account has a password).

## Requirements & use cases covered

| Item | Covered by this feature? | Notes |
|------|--------------------------|-------|
| FR-008 (amended) | ✅ | Sign out ends this session; a refused refresh signs out cleanly with a notice |
| FR-049 | ✅ | Change password; sign out on all devices |
| NFR-005 | ✅ | A wrong current password answers like a failed sign-in; rate-limited |
| ADR-0079 | ✅ | |

## Design overview

- **Server (Main service, `AccountEndpoints`):**
  - `POST /account/logout` takes an optional `LogoutRequest(RefreshToken)`. With a token: revoke the chain
    holding it, when the chain belongs to the caller. Without one (older apps): revoke every chain, as
    before.
  - `POST /account/logout-all`: revoke every chain of the caller.
  - `POST /account/change-password` with `ChangePasswordRequest(CurrentPassword, NewPassword, RefreshToken)`:
    check the current password, then `UserManager.ChangePasswordAsync` (policy + breached-password
    validator), then revoke every chain except the caller's, then queue the `mail.password-changed` notice.
    Sensitive rate limit, like sign-in.
  - `SessionService.RevokeAsync(userId, refreshSecret)` and `RevokeOthersAsync(userId, refreshSecret)` on
    top of two new store queries.
- **Client (`CircleTools.Ui.Account`):**
  - `AccountSession.SignOutAsync` sends the stored refresh secret; new `SignOutEverywhereAsync` and
    `ChangePasswordAsync`.
  - A refused refresh drops the local session and raises a "signed out" notice the shell shows once.
  - Security page: a "Password" section (current, new, repeat) and a "Sign out on all devices" button behind
    a confirmation.

## Domain model impact

- **Aggregates / entities / value objects touched or added:** `RefreshTokenChain` is unchanged. The store
  gains per-chain lookups for revocation.
- **New domain events:** none.
- **Glossary terms added or changed:** none.

## API / contract changes

- **Endpoints / DTOs:** `LogoutRequest`, `ChangePasswordRequest`; `POST /account/logout-all`,
  `POST /account/change-password`. `POST /account/logout` accepts an optional body.
- **Versioning / compatibility:** additive. Deploy the server first. Older apps (no logout body) keep
  revoke-all.

## Data & migration considerations

- **Postgres (server):** no schema change.
- **SQLite / browser storage (local):** none.
- **Sync impact:** none.

## Localization impact

- New strings in DE / EN / FR: Security page sections, validation messages, the "you were signed out"
  notice, and the password-changed email.

## Testing strategy

See [TP-023](TP-023-session-scope-and-change-password.md): endpoint integration tests (this-session
logout, logout-all, change password incl. wrong current password, policy failure, other sessions revoked,
own kept), session-service unit tests, and a browser walkthrough with two devices.

## Rollout / deployment & feature flags

- Server first, then the web app and the mobile apps. No flag.

## Risks & open questions

- A user who relied on sign-out ending everything must now use "Sign out on all devices". Mitigated by
  placing it on the Security page next to the password.

## Definition of Done

- [ ] Meets acceptance criteria of all covered requirements
- [ ] Builds clean under analyzers (warnings = errors)
- [ ] Tests per the test plan pass; coverage target met
- [ ] Architecture boundaries respected (arch tests green)
- [ ] Docs, glossary, and API comments updated
- [ ] Localized strings provided (DE/EN/FR)
- [ ] Traceability links closed
