---
status: Proposed
date: 2026-11-13
deciders: [Adrian]
tags: [mobile, api, sync, self-hosting, compatibility, ux]
---

# ADR-0096 — Choosing the server in the apps, and a discovery endpoint

## Context and problem statement

The official Android and iOS apps talk to the hosted backend only: `ApiBaseUrl` and `RevealBaseUrl` are
**build-time** constants baked into the assembly ([ADR-0053](0053-backend-free-first-mobile-release.md),
`MauiProgram.cs`). The owner wants the store apps to keep connecting to the hosted service **by
default**, while advanced users can point the **same** app at their own server
([ADR-0095](0095-self-host-deployment-topology.md)).

Changing servers is not just a URL: accounts, synced groups, the storage mode
([ADR-0071](0071-default-storage-mode-and-first-run-onboarding.md),
[ADR-0073](0073-storage-mode-is-authoritative.md)), passkeys and links all depend on it. And a self-hosted
server may be **older or newer** than the app, the reverse of the case [ADR-0090](0090-backend-stays-compatible-with-released-apps.md)
covers.

**How does a user choose a server, how does the app learn what that server is and can do, and what
happens to data when the server changes?**

## Decision drivers

- The default experience must not change: no new step, no new choice for regular users.
- Offline, account-free use stays untouched — the server setting only matters for sign-in and sync
  (NFR-001).
- A user must always be able to see which server holds their account.
- No data may move silently from one server to another, or be lost by switching.
- App and server versions drift apart on self-hosted instances.

## Considered options

1. **Advanced "Server" setting + discovery document; one server per signed-in session**
2. **Several servers at once** (accounts on multiple servers side by side)
3. **Separate "self-host" build of the app** with a configurable URL

## Decision outcome

**Chosen: Option 1.**

**Discovery document.** Every server serves `GET /.well-known/circletools` (no auth, cacheable, CORS-open)
returning JSON such as:

```json
{
  "schema": 1,
  "name": "Example family server",
  "serverVersion": "1.4.2",
  "apiVersions": ["v1"],
  "publicUrl": "https://santa.example.net",
  "apiBaseUrl": "https://santa.example.net/api",
  "apps": { "secretsanta": { "baseUrl": "https://santa.example.net/secretsanta" } },
  "auth": { "passwords": true, "passkeys": { "rpId": "santa.example.net" } },
  "registration": "invite",
  "features": ["sync", "sharedGroups", "circles", "inviteMail"],
  "operator": { "name": "…", "legalUrl": "…" }
}
```

The hosted service serves the same document. Clients read capabilities from `features` and
`apiVersions` instead of assuming them, and hide or explain features a server does not offer.

**Server setting in the apps.**

- Under Settings → Advanced: **Server** with "CircleTools (default)" and "Custom server…". The user types
  a URL; the app fetches the discovery document over **HTTPS only**, shows the server name and operator,
  and asks for confirmation.
- The setting can only be changed while **signed out**. Switching does not touch local groups; they stay
  on the device. Groups stored online on the previous server are not moved — the user exports/imports
  through the existing data export ([ADR-0022](0022-account-deletion-and-data-export.md)) if they want to
  move.
- The account screen and the sign-in screen always show the server's host name when it is not the
  default one.
- The web app served by a self-hosted instance uses its own origin as its server and does not show the
  setting.

**Version handshake.** The app compares `apiVersions` with what it supports. If there is no overlap it
refuses to sign in and says whether the app or the server needs an update. Within a shared API version,
missing features are hidden. This extends ADR-0090 in the other direction: **new apps keep working with
servers that are older**, back to a documented *minimum supported server version*.

**Store review.** The default is the hosted service; the custom server is an advanced option, which both
stores accept. Store privacy declarations state that a user may choose a server run by a third party, in
which case that operator processes the data.

## Consequences

- **Positive:** one app for everyone; regular users see nothing new; self-hosters get the official,
  store-updated app; capabilities are explicit instead of guessed.
- **Negative:** the clients need capability checks around features; the app must handle a server that
  is offline, misconfigured or too old with clear messages in DE/EN/FR.
- **Follow-ups:** discovery endpoint on the API; client service that loads and caches it; Advanced →
  Server UI (feature spec + test plan); move `ApiBaseUrl`/`RevealBaseUrl` from build-time constants to
  "build-time default, runtime override"; minimum supported server version in the release notes; store
  privacy declarations updated.
- **New risks:** a user can be talked into pointing the app at a malicious server (phishing their
  password). Mitigations: HTTPS only, the server name and host are always visible, switching requires
  signing out and an explicit confirmation.

## Pros and cons of the options

### One server at a time, advanced setting
- 👍 Simple mental model; small change to the session and sync code.
- 👎 Moving data between servers is manual (export/import).

### Several servers at once
- 👍 Flexible.
- 👎 Every group, circle and sync path would need a server dimension — large change for few users.

### Separate self-host build
- 👍 No risk to the default app.
- 👎 Self-hosters would have to sideload or build the app themselves; iOS makes that very hard.

## Links

- Related ADRs: [ADR-0053](0053-backend-free-first-mobile-release.md), [ADR-0071](0071-default-storage-mode-and-first-run-onboarding.md),
  [ADR-0073](0073-storage-mode-is-authoritative.md), [ADR-0090](0090-backend-stays-compatible-with-released-apps.md),
  [ADR-0010](0010-api-style-and-versioning.md), [ADR-0022](0022-account-deletion-and-data-export.md),
  [ADR-0095](0095-self-host-deployment-topology.md), [ADR-0097](0097-passkeys-and-links-on-self-hosted-servers.md)
- Backlog: [Open source & self-hosting](../roadmap/backlog.md#open-source--self-hosting-2026-11-13) (CS-1…CS-6)
