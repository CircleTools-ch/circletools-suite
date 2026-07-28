---
status: Accepted
date: 2026-07-28
deciders: [Adrian]
tags: [architecture, security, identity, auth, web]
---

# ADR-0026 — Refresh-token persistence in the browser

## Context and problem statement

[ADR-0021](0021-web-auth-token-model.md) settled the token model: bearer tokens in the WASM client,
short-lived access tokens **in memory only**, refresh-token rotation with reuse detection. It says
the refresh token is persisted "only where a session genuinely needs to survive a reload" — and stops
there, because at the time there was no client to be concrete about.

Building the client makes the gap unavoidable. The access token dies with the tab by design, so on
every reload the refresh token is the only thing that can tell the difference between "the same user,
one second later" and "a stranger". Where it is kept decides how long a session lasts, and web
storage offers exactly two answers:

- `sessionStorage` — survives a reload, dies when the tab closes.
- `localStorage` — survives a browser restart, until removed.

Neither is right for everyone. A phone the owner never signs out of and a shared family laptop are
both real, and they want opposite defaults.

**Where is the refresh token kept, and who decides?**

## Decision drivers

- ADR-0021's threat model is XSS. **Both** storage areas are readable by injected script; this
  decision cannot improve that, only decide how long a stolen token stays useful.
- The refresh token has a 30-day lifetime and rotates on every use. A durable copy is a 30-day
  session; a session copy is a session that ends with the tab.
- Devices are genuinely shared in the target audience — this is a family product.
- The user knows which kind of device they are on, and the app cannot.
- Whatever is chosen must not silently upgrade itself. A session that quietly became permanent is
  worse than either option chosen deliberately.

## Considered options

1. **`sessionStorage` by default, `localStorage` behind an explicit "stay signed in" checkbox.**
2. **`localStorage` always** — one code path, sessions always survive.
3. **`sessionStorage` always** — shortest exposure, no choice to get wrong.
4. **In-memory only** — nothing persisted; every reload signs the user out.

## Decision outcome

**Chosen: option 1 — `sessionStorage` by default, `localStorage` when the user ticks "stay signed
in".**

The mechanics matter as much as the choice, because each of these is a way to get it wrong:

- The checkbox is on the sign-in form, **unticked by default**, with a line of help text saying to
  tick it only on a device that is yours. Defaulting to the safer answer means the borrowed-laptop
  case needs no thought and the own-phone case costs one click.
- **Saving to one scope removes the token from the other.** Without that, signing in once with the
  box ticked and again without it would leave a durable token behind that the read path keeps
  preferring — quietly defeating the newer, more cautious choice.
- **Rotation keeps the scope it found.** A refresh is not a new decision about session length, so it
  writes back where the previous token was (`IRefreshTokenStore.RenewAsync`). Routing rotation
  through the save path would force every caller to remember an answer given hours ago, and getting
  that wrong silently promotes a session to permanent.
- Signing out, a completed password reset, and account deletion each clear **both** scopes.
- Storage failures are swallowed. Private-browsing modes and blocked site data make web storage throw;
  the app then behaves as though nothing was remembered, which costs a sign-in and never a page.
- **The access token is still never persisted.** That part of ADR-0021 is unchanged and not up for
  negotiation here.

Option 2 is what most apps do, and it makes every sign-in on a shared machine a 30-day session the
user did not ask for. Option 4 is defensible on security grounds alone and unusable in practice: it
turns an accidental reload into a sign-in prompt, on a product whose whole premise is that it stays
out of the way.

**This extends ADR-0021 rather than amending it** — the model, the lifetimes and the CSP are all
unchanged.

## Consequences

- **Positive:** the safe default is the one the user gets without deciding anything; the durable
  session is opt-in and named plainly rather than inferred.
- **Positive:** one seam (`IKeyValueStore` with a `StorageScope`) covers both areas, so the MAUI head
  maps the same two scopes onto its own storage without any of this logic changing.
- **Negative / accepted:** a ticked box means a token readable by injected script for up to 30 days.
  This is the ADR-0021 trade at its longest, taken knowingly and only when asked for. The CSP remains
  the control that matters.
- **Negative / accepted:** users who do not tick the box will be asked to sign in more often than
  they expect, because most other sites default the other way.
- **Negative / accepted:** two storage scopes means two places to clear, and every sign-out path has
  to clear both. It is centralised in `RefreshTokenStore` for exactly that reason.
- **Follow-ups:** if a "sign out everywhere" screen is ever added, it should also list active
  sessions — the server already tracks the chains needed to show them.
- **New risks:** none beyond ADR-0021's, other than the longer window the checkbox deliberately buys.

## Pros and cons of the options

### 1. Session by default, durable behind a checkbox (chosen)
- 👍 Safe default; the durable session is a deliberate, named choice; matches how shared devices are
  actually used.
- 👎 Two code paths and two scopes to keep in step; more frequent sign-ins than users may expect.

### 2. `localStorage` always
- 👍 Simplest; sessions "just work" everywhere.
- 👎 Turns every shared-device sign-in into a 30-day session nobody asked for; longest possible XSS
  window, always.

### 3. `sessionStorage` always
- 👍 Simple; the window closes with the tab.
- 👎 Removes a choice users legitimately want on their own devices, for no gain when they are the only
  person with the device.

### 4. In-memory only
- 👍 Nothing persisted at all; smallest possible attack surface.
- 👎 A reload signs you out. Unacceptable for a PWA meant to feel like an app.

## Links

- Related ADRs: extends [ADR-0021](0021-web-auth-token-model.md);
  [ADR-0009](0009-authentication-and-passkeys.md),
  [ADR-0025](0025-shared-ui-building-blocks.md) (where the stores live)
- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md) §8.4
- Requirements: [FR-008](../requirements/functional/FR-008-sign-in-with-password.md)
- Feature: [FEAT-002](../features/FEAT-002-m1-identity.md)
- Guideline: [security & privacy guidelines](../guidelines/security-privacy-guidelines.md)
