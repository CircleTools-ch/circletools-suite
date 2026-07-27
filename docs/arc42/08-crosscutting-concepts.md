# 8. Cross-cutting Concepts

> _Proposed_ — concepts that apply across building blocks. Each firms up via ADRs and guidelines.

## 8.1 Domain model & DDD

Tactical patterns (entities, value objects, aggregates, domain events, repositories) in
`SharedKernel` and each `Service.Core`. Ubiquitous language per the [glossary](../glossary/index.md).

## 8.2 Persistence & local-first

Repository **ports** in the domain; **EF Core** behind both adapters — a **Postgres** adapter
(`Service.Repository`) on the server and a **SQLite** adapter (`LocalStore`) on native clients
([ADR-0007](../adr/0007-persistence-approach.md)). Keys are client-generated **GUID v7**
([ADR-0008](../adr/0008-entity-id-strategy.md)), so aggregates can be created offline without a
server round-trip. Aggregates are the unit of persistence and sync.

The **web client** persists to `localStorage` behind `IKeyValueStore` / `IGroupRepository`
([ADR-0015](../adr/0015-mvp-client-offline-storage.md)); under sync the persisted `GroupState` also
carries **sync metadata** — version token, tombstone flag, cursor
([ADR-0016](../adr/0016-sync-and-conflict-resolution.md)). IndexedDB and SQLite-in-OPFS stay parked
behind a concrete trigger.

## 8.3 Synchronization

**Local-first**: the device stays authoritative and usable offline; the server is a relay and backup,
never a gatekeeper ([NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md)). Sync
applies only to a signed-in [User](../glossary/user.md) — without an account there is no server and no
sync.

The mechanism ([ADR-0016](../adr/0016-sync-and-conflict-resolution.md)): the **aggregate is the sync
unit** (never merged field-by-field, which would break its invariants); each carries a **version
token**; deletes leave **tombstones** so they propagate instead of resurrecting; changes travel as a
**delta pull/push** against a client cursor, triggered **on app start and on change**. Conflicts
resolve **last-write-wins per aggregate** by server-recorded time, behind a named **escalation hook**
that detects genuine divergent-edit collisions and emits telemetry — so a conflict UI can be added
later without re-architecting. The accepted cost is that two devices editing the same aggregate
offline will silently lose the earlier edit.

Anonymous local data meets an account through an explicit **claim**: the user chooses **per group**
what to take into the account on first sign-in, and local data is kept as a cache afterwards
([ADR-0017](../adr/0017-anonymous-to-account-claim.md)).

## 8.4 Identity & authentication

`AppSuite.Identity` centralizes the single suite account using **ASP.NET Core Identity** with the
**built-in .NET 10 passkey** support: email+password and optional **passkeys/WebAuthn**
([ADR-0009](../adr/0009-authentication-and-passkeys.md)). Offline mode uses **no** identity, and an
account is optional and additive throughout.

**Token model** ([ADR-0021](../adr/0021-web-auth-token-model.md)): bearer tokens for APIs from both
clients. Native MAUI keeps them in `SecureStorage`. The standalone WASM PWA
([ADR-0005](../adr/0005-web-hosting-render-model.md)) has no server-side host, so there is no BFF and
no `HttpOnly` cookie available — the accepted consequence is that **XSS yields a usable token**. It is
contained, not solved, by: **short-lived access tokens held in memory**, **refresh-token rotation**
(reuse of a consumed refresh token revokes the chain), and a **strict CSP**, which is the primary
control and must stay strict — relaxing it silently removes the main mitigation.

## 8.5 Localization (DE/EN/FR)

Central `AppSuite.Localization` using **RESX + `IStringLocalizer`** ([ADR-0011](../adr/0011-localization-format-and-culture.md));
`de-CH` default culture; culture is a synced user preference. Formatting via `CultureInfo`; no
locale/currency logic in the domain. See the [localization guidelines](../guidelines/localization-guidelines.md).

## 8.6 Configuration sharing

suite-wide settings (branding, theme, locale, feature flags) exposed via an `IAppConfig`
abstraction, consistent across Web and native; user/suite prefs persisted locally and synced.

## 8.7 Observability

`AddServiceDefaults()` wires OpenTelemetry (traces/metrics/logs), health checks, and HTTP
resilience for every service host. Client telemetry is optional and consent-gated
([ADR-0012](../adr/0012-observability-and-telemetry.md)).

## 8.8 Security & privacy

Privacy by design: minimal PII, input validation, secrets never in the repo, Swiss/GDPR
awareness. See the [security & privacy guidelines](../guidelines/security-privacy-guidelines.md).

**Assignment privacy differs by mode**
([NFR-002](../requirements/non-functional/NFR-002-assignment-privacy.md)):

- **Account-free mode** — enforced by **absence**. No [Draw](../glossary/draw.md) result reaches any
  server; assignments live in the organizer's local storage and inside individual
  [reveal links](../glossary/reveal-link.md) ([ADR-0014](../adr/0014-reveal-link-encoding-and-privacy.md)).
  *The link is the secret.* Verified by network capture.
- **Account mode** — enforced by **authorization**
  ([ADR-0020](../adr/0020-server-side-assignment-privacy.md)). Assignments are stored in Postgres so a
  signed-in member can see theirs on any device. The API returns **only the caller's own row**; the
  full mapping goes to the organizer alone. Verified by authorization tests.
  Stated plainly: **the operator can technically read every pairing**. That is the accepted cost of
  the decision; the data is kept minimal, and **per-member envelope encryption is the recorded future
  hardening step**.

**Account-enumeration resistance** ([ADR-0018](../adr/0018-circle-and-sharing-model.md)): an
[Invitation](../glossary/invitation.md) never reveals to the inviter whether the address already has
an account — the UI always says *"invitation sent"* and the branch resolves on the recipient's side.
Invitations expire, can be revoked, and are rate-limited per inviter.

## 8.9 Error handling

Domain exceptions vs. `Result` for expected failures; no control-flow-by-exception; API errors as
`ProblemDetails`.

## 8.10 Circles & sharing

[Circle](../glossary/circle.md) is the standing set of people that apps share
([ADR-0018](../adr/0018-circle-and-sharing-model.md)) — the aggregate root, owning
[memberships](../glossary/circle-membership.md) with roles (owner / admin / member), pending
invitations, and [managed members](../glossary/managed-member.md) who have a profile but no account.

`Circle` is the **canonical code name** — tables, DTOs and URLs — while the name users see is a
**per-circle label they choose**, defaulting to "Family". `Group` stays reserved for the Secret-Santa
aggregate: a circle is the standing set of people, a group is one gift exchange.

The dependency points **one way**: a Secret-Santa group may be created *from* a circle, copying its
people in as [Members](../glossary/member.md) and freezing that set when the draw runs, but the
Secret-Santa domain holds at most a circle identifier and never references the Circles context. A
`Member` may carry an optional `UserId` ([ADR-0019](../adr/0019-member-user-linking.md)); unlinked
members stay first-class and keep their reveal links.
