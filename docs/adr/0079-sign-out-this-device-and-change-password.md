---
status: Accepted
date: 2026-10-15
deciders: [Adrian]
tags: [identity, sessions, security, ux, account]
---

# ADR-0079 — Sign out ends this device's session; "sign out everywhere" and password change are explicit

## Context and problem statement

[FR-008](../requirements/functional/FR-008-sign-in-with-password.md) and its `POST /account/logout` ended
**every** session of the account ("ends the session everywhere"). On the
[2026-10-15 sync test](../brainstorming/2026-10-15-secret-santa-sync-device-test.md) (S9), signing out on
the phone silently signed the browser out too. The browser didn't say so: its next refresh got a `401`, and
it kept calling authenticated endpoints with the dead token.

People know "Sign out" from Google, Apple, WhatsApp or their bank: it signs out **this** device. Ending a
session on a borrowed or shared computer must not also end the session on your own phone, where you ticked
"stay signed in".

The same test showed a gap next to it (D10): there is **no way to change your password** while signed in.
The only path is "forgot password" by email. Yet the email-change notice tells the reader to "change your
password immediately" if the change wasn't theirs.

**What should signing out end, and how does a user deliberately end sessions elsewhere or change their
password?**

## Decision drivers

- Match what users expect from the apps they use daily (UX).
- A lost device or an intruder must still be ejectable (security, FR-009 / FR-015).
- The token model of [ADR-0021](0021-web-auth-token-model.md) stays: short-lived access token in memory,
  rotated refresh chain. A session is one refresh chain.
- Apps already installed (0.1.115 and older) must keep working while the server changes first.

## Considered options

1. **Keep "sign out everywhere"** and only change the label.
2. **Sign out ends this session (chosen).** "Sign out on all devices" is a separate, explicit action, and a
   password change ends the other sessions.
3. **Ask on every sign-out** ("this device / all devices").

## Decision outcome

**Chosen: option 2.**

- **`POST /account/logout`** takes the client's **refresh secret** in the body (`LogoutRequest`). The server
  revokes **only the chain that secret belongs to**, if the chain belongs to the caller. The access token
  carries no session id, so the refresh secret is what identifies "this session". A request **without** a
  body (an app built before this change) keeps the old behaviour and revokes all chains. Old clients never
  lose the ability to end their session.
- **`POST /account/logout-all`** revokes every chain of the account. It is offered in **Security → "Sign
  out on all devices"** and also signs out the current device.
- **`POST /account/change-password`** takes the current password, the new password and the caller's
  refresh secret. It checks the current password (with the same limits as sign-in), applies the password
  policy and the breached-password check, sets the new password, then **revokes every other session** and
  keeps the caller's. A notice email goes to the account's address ("your password was changed"), with no
  link, like the email-change notice. Every account has a password (sign-up requires one), so there is no
  "set a first password" path.
- **Password reset** (FR-009) and **account deletion** (FR-015) still end every session.
- **A session ended elsewhere is noticed.** When a refresh is refused, the client drops its local session,
  switches to signed out and shows a short notice ("You were signed out"). It doesn't keep calling the API
  with the old token.

Option 1 keeps the surprise. Option 3 puts a security question in front of every sign-out, though "all
devices" is the rare case.

## Consequences

- **Positive:** sign-out does what people expect. "Sign out on all devices" and changing the password are
  real, findable actions. The email notice's advice ("change your password") can now be followed.
- **Negative:** a user who relied on sign-out ending every session now has to choose "Sign out on all
  devices". The Security page offers it next to the passkeys.
- **Compatibility:** server first. Old apps keep revoke-all on sign-out until they update. A new app against
  an old server sends a body the old server ignores, so it revokes all — the old behaviour, never worse.
- **Follow-ups:** FR-008 is amended; FR-049 (change password) and
  [FEAT-023](../features/FEAT-023-session-scope-and-change-password.md) implement this.

## Pros and cons of the options

### 1. Keep "sign out everywhere"
- 👍 No change; strongest ejection on every sign-out.
- 👎 Surprising; signing out of a borrowed browser kills your own phone's session.

### 2. Sign out ends this session; explicit "everywhere" (chosen)
- 👍 Matches common apps; keeps ejection available where people look for it (Security).
- 👎 One more action on the Security page.

### 3. Ask every time
- 👍 Explicit.
- 👎 A security choice on every sign-out; most people pick the default without reading.

## Links

- Amends [FR-008](../requirements/functional/FR-008-sign-in-with-password.md); adds
  [FR-049](../requirements/functional/FR-049-change-password.md)
- Builds on [ADR-0021](0021-web-auth-token-model.md) (token model), [ADR-0009](0009-authentication-and-passkeys.md)
- Implemented by [FEAT-023](../features/FEAT-023-session-scope-and-change-password.md)
