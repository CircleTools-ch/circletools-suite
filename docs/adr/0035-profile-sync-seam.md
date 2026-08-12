---
status: Accepted
date: 2026-08-12
deciders: [Adrian]
tags: [architecture, sync, offline, account, localization]
---

# ADR-0035 — Profile-sync seam (host-supplied profile writer)

## Context and problem statement

[ADR-0033](0033-which-aggregates-sync.md) fixed the M3 sync catalogue as **the claimed Secret-Santa
[Group](../glossary/group.md) and profile preferences** (display name, preferred language). The group
half is built. The profile half is not, and this ADR decides how it works — closing the criterion
[FR-012](../requirements/functional/FR-012-manage-profile.md) carried out of M1 (an offline language
change, queued and pushed on the next sync) and satisfying
[FR-023](../requirements/functional/FR-023-sync-across-devices.md)'s "display name + language across
devices" for the app that actually works offline.

Two facts shape the decision:

- **Only the Secret-Santa app has a client sync engine** (`AddSecretSantaSyncEngine`). The **Main
  WebApp is online-only** for account + circles; a profile edit there is a direct authorized
  `PUT /account/profile`.
- **The profile screen is shared.** [`Profile.razor`](../../src/BuildingBlocks/AppSuite.Ui.Account/Pages/Profile.razor)
  and the session service live in the shared `AppSuite.Ui.Account` block
  ([ADR-0034](0034-shared-account-session-ui-block.md)), consumed by both apps. Today the page does a
  direct online `UpdateProfileAsync`, so an offline edit returns `Unreachable` and is lost.

A third fact rules out the obvious "make profile just another sync aggregate" reading: **the
authoritative profile lives in the Identity `Users` table**, read and written through
`GET`/`PUT /account/profile`, and the **JWT `locale` claim is derived from `user.PreferredLanguage`**
(`AccountEndpoints.cs`). That claim is what a borrowed-device sign-in (FR-012) and the Main app read.
If a language change were pushed only into the opaque **sync store** (a different table Platform never
projects into Identity), it would reach a second Secret-Santa device but leave the JWT locale, the Main
app, and `GET /profile` stale — an incoherent split of one preference across two stores.

So: **how does a profile edit made on the shared page persist offline and reach the account's other
devices, keeping the authoritative `/profile` endpoint the single source of truth?**

## Decision drivers

- **One source of truth.** The Identity profile drives the JWT locale, the Main app and the
  borrowed-device path; a synced change must land there, not beside it.
- **Layering (`UiAccountLayeringTests`, `SyncLayeringTests`).** The shared account block must not depend
  on `SecretSanta.LocalStore`; the sync store stays opaque and free of app or account schema.
- **Local-first (NFR-001, FR-024).** An offline profile edit must succeed against local storage, be
  readable offline, and never block on a round-trip.
- **Reuse the established seam pattern.** `AddAccountUi` already asks the host to supply what differs
  between hosts (`IKeyValueStore`, `ICultureProvider`, keyed `HttpClient`s). A profile persistence port
  is the same shape of seam.
- **Do not regress the Main app.** Main keeps its direct online write, byte for byte.

## Considered options

1. **Host-supplied profile-writer seam, offline edits flushed to `/profile`.** A port
   (`IProfileWriter`) in `AppSuite.Ui.Account`. Main registers the direct-online implementation; the
   Secret-Santa app registers one that, when offline, queues the edit in a local pending record and
   **`PUT`-flushes it to the authoritative endpoint on the next sync trigger**, and adopts another
   device's change by reading `GET /profile` in the same reconcile.
2. **Profile as an opaque sync-store aggregate.** Push the profile through the `/sync` cycle keyed
   `"profile"`, as the `SyncKinds.Profile` scaffolding anticipates. Rejected: it writes a different
   store than Identity and leaves the JWT locale / Main app / `GET /profile` stale unless the server
   also **bridges** the pushed body into Identity — new server code that makes Platform deserialize the
   body it is designed to keep opaque.
3. **Language-only interception via the culture provider.** Queue only the language change. Rejected: it
   satisfies only FR-012's language clause and leaves FR-023's display-name sync unbuilt.

## Decision outcome

**Chosen: Option 1 — a host-supplied `IProfileWriter` seam whose offline implementation flushes to the
authoritative `/profile` endpoint.**

- **The port** (`IProfileWriter`, in `AppSuite.Ui.Account`) covers the two things the page needs — load
  the current preferences, save an edit — in the same `ProfileResponse`/`UpdateProfileRequest` the page
  already speaks. `Profile.razor` depends on the port, keeping its existing UX (the saved-confirmation
  navigation and the immediate `SetCultureAsync`); only the persistence call changes.
- **Main WebApp** registers the **direct-online** writer (`OnlineProfileWriter`, a thin wrapper over
  `AccountApiClient.GetProfileAsync`/`UpdateProfileAsync`) — behaviour identical to today. It is the
  default `AddAccountUi` supplies, so a host that does nothing extra gets it.
- **Secret-Santa** registers `OfflineProfileWriter` (in `AppSuite.SecretSanta.Ui`, which references
  both the account block and the local store — so no dependency crosses into the account block). It:
  - on **save**, tries the authoritative `PUT`; offline, writes a `SyncedProfileRecord` (display name,
    language, `Dirty`) into the local store and reports success so the page applies the language and
    confirms — the queued change FR-012 asks for;
  - on **load**, returns a pending offline edit if one exists, else reads `GET /profile` (falling back
    to the cached copy while offline);
  - on **reconcile** (run by a `SyncCoordinator` on each sync trigger, before the group cycle), flushes
    a queued edit via `PUT` and, when nothing is pending, pulls `GET /profile` and switches the running
    culture if another device changed the language.
- **The profile never rides the opaque sync-store cycle.** `SyncService` stays groups-only; the local
  `SyncedProfileRecord` is a Platform pending-edit cache, not a sync aggregate, and the authoritative
  Identity profile stays the single source of truth. The `SyncKinds.Profile` wire constant is left
  unused rather than wired to a second store.

## Consequences

- **Positive:** FR-012's offline-language criterion and FR-023's profile sync both close on the
  authoritative endpoint — the JWT locale, the Main app and a borrowed-device sign-in all stay coherent.
  The Main app is unchanged; layering holds (the account block sees only the port; the sync store stays
  opaque).
- **Positive:** the seam matches the existing `AddAccountUi` host-supplies-the-difference pattern.
- **Negative / accepted:** the offline writer lives in `AppSuite.SecretSanta.Ui`, an app project, though
  profile is an account concern — because that is the only project that both works offline and may
  reference the local store. Recorded rather than hidden; extract behind the same port if a second
  offline host appears.
- **Negative / accepted:** cross-device propagation is by the authoritative endpoint, so a running
  second device adopts a change on its next reconcile or sign-in, not instantly — acceptable for M3.
- **Follow-ups:** the `SyncKinds.Profile` constant and the server comments that anticipate a profile
  aggregate are now dead scaffolding; remove or repurpose them if they cause confusion.
- **New risks:** none introduced. The one to watch is a later feature quietly reintroducing option 2's
  split by pushing profile into the sync store; the driver above is the guard.

## Pros and cons of the options

### 1. Host-supplied seam, offline edits flushed to `/profile` (chosen)
- 👍 One source of truth; FR-012 and FR-023 both close; Main unchanged; layering intact; reuses the seam.
- 👎 The offline writer lives in an app project, a layer out from the account concern it serves (recorded).

### 2. Profile as an opaque sync-store aggregate
- 👍 Reuses the existing `/sync` cycle and the `SyncKinds.Profile` scaffolding.
- 👎 Splits one preference across two stores; needs a server bridge into Identity that makes Platform
  deserialize the body it is meant to keep opaque.

### 3. Language-only via the culture provider
- 👍 Smallest change.
- 👎 Satisfies only FR-012's language clause; leaves FR-023's display-name sync unbuilt.

## Links

- Related ADRs: [ADR-0016](0016-sync-and-conflict-resolution.md) (sync mechanism, kept opaque),
  [ADR-0033](0033-which-aggregates-sync.md) (catalogue — Group + profile),
  [ADR-0034](0034-shared-account-session-ui-block.md) (the shared account block the seam extends),
  [ADR-0025](0025-shared-ui-building-blocks.md) (host-supplies-the-platform pattern),
  [ADR-0021](0021-web-auth-token-model.md) (the authorized client and JWT locale claim)
- arc42 chapters: [05 Building-block view §5.3](../arc42/05-building-block-view.md)
- Requirements / use cases:
  [FR-012](../requirements/functional/FR-012-manage-profile.md),
  [FR-023](../requirements/functional/FR-023-sync-across-devices.md),
  [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [UC-MAIN-015](../use-cases/main/UC-MAIN-015-sync-across-devices.md)
- Feature: [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md), test plan
  [TP-004](../features/TP-004-m3-sync-and-claim.md)
- Glossary: [Sync](../glossary/sync.md), [User](../glossary/user.md)
