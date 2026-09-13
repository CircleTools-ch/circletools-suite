---
title: FEAT-013 Implementation Plan — Account-free, link-shared groups
feature: FEAT-013
status: Draft
related: [FEAT-013, TP-013, ADR-0058, FR-038, FR-039, FR-040, FR-041, FR-042, FR-043, NFR-002, NFR-009, NFR-010]
---

# FEAT-013 Implementation Plan — Account-free, link-shared groups (third storage tier)

Companion build plan for [FEAT-013](FEAT-013-account-free-link-shared-groups.md) /
[TP-013](TP-013-account-free-link-shared-groups.md). Build plan for an **Accepted** decision
([ADR-0058](../adr/0058-account-free-link-shared-groups.md)) — not a design re-litigation.
Repo: .NET 10 / Aspire / DDD, local-first Blazor WASM + MAUI.

## 0. Orientation — what exists and what it teaches us

The whole feature is an extension of machinery that already ships. The plan reuses these seams rather than inventing parallel ones:

| Concern | Existing implementation to reuse | Where |
|---|---|---|
| Client domain port | `IGroupRepository` (`ListAsync/GetAsync/SaveAsync/DeleteAsync`) | `SecretSanta.Core/Domain/IGroupRepository.cs` |
| Aggregate memento | `Group.ToState()/FromState()` + `GroupState` (additive-nullable backward-compat pattern) | `Core/Domain/Group.cs`, `Core/Persistence/GroupState.cs` |
| Client sync record | `SyncedGroupRecord(State, Version, AccountScope, Dirty)` — **sync metadata kept out of the domain memento on purpose** (SyncLayeringTests fitness fn) | `LocalStore/Sync/SyncModel.cs` |
| Client sync engine | `SyncService` (push-dirty / pull-above-cursor / LWW), `SyncingGroupRepository`, `ILocalSyncStore`, `ISyncApi`/`HttpSyncApi` | `LocalStore/Sync/*` |
| Client orchestration | `AutoSyncService` (4 triggers, debounced), `SyncCoordinator`, `VisibleGroupsService`, `ClaimableGroupsService` | `Ui/Sync/*`, `LocalStore/*` |
| Server opaque store | `ISyncStore`/`SyncStore`, `SyncDbContext` (schema `sync`), `SyncAggregate/Marker/Device/AccountCounter`, per-account monotonic seq via `account_counters` upsert, LWW by version + `ServerModifiedAt` | `Main.Service.Repository/Sync*`, `Main.Service.Core/Domain/ISyncStore.cs` |
| Server per-caller-authorized store (the "server understands SS schema" precedent) | `ILinkingStore`/`LinkingStore`, `LinkingDbContext` (schema `linking`), `AssignmentRow`/`MemberLinkRow`, **Found/NotDrawn/Denied** privacy pattern | `Main.Service.Repository/Linking/*`, `LinkingEndpoints.cs` |
| Endpoint wiring | `Program.cs` — 4 contexts migrated independently; versioned route groups; `AddRateLimiter` fixed-window per-IP (`SensitiveRateLimitPolicy`); `ConstantTimeFilter` | `Main.Service/Program.cs`, `ConstantTimeFilter.cs` |
| Account deletion | `IAccountDeletionGuard` + N × `IAccountDeletionCleanup`, resolved in `DeleteAccountAsync` | `AccountEndpoints.cs`, `IAccountDeletionCleanup.cs`, `LinkingAccountCleanup.cs` |
| UI reuse | MudBlazor; `ConfirmSheet`/`ShowConfirmSheetAsync`, `TextInputSheet`, `QrDialog`/`ShowQrDialogAsync`, `IShareService`, hub rows (`ct-hub-row`), FAB pattern, `IStringLocalizer<AppStrings>` + `AppStrings.{resx,de-CH,en,fr-CH}` | `Ui/Shared/*`, `Ui/Pages/{Groups,GroupEditor,EventHub,Share}.razor` |
| Deep-link | `DeepLinkService` (Android App Links `https://secretsanta.circletools.ch/r#…`), reveal route | `SecretSanta.App/DeepLinkService.cs`, `Platforms/Android/MainActivity.cs` |
| Host composition | WASM/MAUI register `ISyncApi`/`ILinkingApi` over `AccountApiClients.Authorized`; **`AccountApiClients.Anonymous` already exists** for the token path | `WebApp/Program.cs`, `App/MauiProgram.cs` |

### Two load-bearing architecture decisions (settled here, no new ADR needed)

**D1 — Where storage-mode / share-token / claim state live (layering).**
The existing code deliberately keeps sync metadata (`AccountScope`) **out** of the domain `GroupState` memento; a fitness function (`SyncLayeringTests`) enforces it. Follow that precedent exactly:
- **StorageMode** and the **share tokens the device holds** → new fields on `SyncedGroupRecord` (LocalStore), **not** on `GroupState`/`Group`. Surfaced to UI via a read model (extend `ClaimableGroup`/`VisibleGroupsService`), never through the domain query.
- **Per-member claim state** (free / device-claimed / account-claimed) → **server-side table**, authoritative and token-keyed, exactly as assignment authorization lives server-side in `LinkingStore`, not in the group memento.
- The domain `Group` aggregate and `GroupState` are **unchanged** by this feature (they already carry roster/events/draws/assignments — the whole body that gets uploaded).

> Decision-to-watch: ADR-0058's "domain model impact" lists StorageMode as a per-group enum. Keeping it in the record wrapper (not the aggregate) is the correct reading given the layering fitness function and the `AccountScope` precedent. If a future need arises to branch domain behaviour on mode, revisit — but v1 needs none.

**D2 — Physical home of a shared group + how claim/release-admin work.**
A shared-by-link group lives in a **new token-keyed server store** (`shared` schema), because the account-scoped `sync` store keys everything by `AccountId` and cannot serve token holders. The shared row carries an optional `OwnerAccountId`:
- Shared-by-link: `OwnerAccountId = null`; admin + join tokens active.
- **Claim-admin** (Shared by link → My account): set `OwnerAccountId`, deactivate the admin token (retired-link message), **keep the join token live**. The group stays physically in the `shared` store so join-link participants never lose access; the owning account reads/writes it through a bearer-authorized variant of the same shared endpoints. O(1), no data move.
- **Release-admin** on a group already in the shared store: clear `OwnerAccountId`, mint a fresh admin token. O(1).
- **Release-admin on a *native* account group** (one that has only ever lived in the `sync` store): the one genuine cross-store move — read the account aggregate, insert a `shared` row, mint tokens, then un-claim the `sync` copy (reuse `UnclaimAsync`). Spelled out in Phase G.

This keeps a single physical home whenever any token is live, makes claim/release cheap, and confines the "account-free write path" to exactly the token endpoints NFR-009 governs.

---

## 1. Build order, dependencies, parallelization

```
Phase A (contracts + client mode scaffolding, local-only) ─┐
Phase B (server token-keyed store + upload/fetch, NFR-009) ─┼─► Phase C (client token sync behind IGroupRepository; upgrade/downgrade)
                                                            │        │
Phase H-retention (NFR-010 sweeper) ── depends on B ────────┘        ├─► Phase D (admin link UI + rotate + share seam)
                                                                     ├─► Phase E (join link + person-claim + own-assignment)
                                                                     │        └─► Phase F (admin release)
                                                                     └─► Phase G (claim-admin / release-admin)
                                                                              └─► Phase H-deletion (account-deletion disposition)
```

- **Parallelizable:** A ∥ B (different layers/teams). After C: D ∥ E ∥ G. H-retention can start as soon as B lands.
- **Walking skeleton = A + B + the thin slice of C** that round-trips one group local→shared→local through the real Postgres store behind a feature flag. Everything after is additive vertical slices, each building & testing green.
- **Feature flag:** ship the whole tier behind a flag (per FEAT-013 rollout). The mode picker only offers "Shared by link" when the flag + a configured shared-API base URL are present. Android first (deep links wired); iOS on browser fallback until MNF-10.

Every phase leaves `On this device` untouched and the network-capture invariant (zero server writes when local) green — that test is written in Phase A and must stay green through H.

---

## 2. Phases

### Phase A — Contracts + client storage-mode scaffolding (local-only, ships green)

**Goal:** introduce the three-way `StorageMode`, the create-time picker (default *On this device*), and a mode chip + a stub "Sharing & sync" hub row — with **no server traffic**. Proves the seam and the localization surface.

**Contracts / DTOs** (`SecretSanta.Contracts`, new `SharedGroupDtos.cs`):
- `enum StorageMode { OnThisDevice, SharedByLink, Account }`.
- Placeholder request/response records used from Phase B on: `UploadSharedGroupRequest(long Version, JsonElement Body)`, `SharedGroupResponse(Guid Id, long Version, JsonElement Body, SharedCapability Capability, bool AdminRetired, string? AdminClaimedByName)`, `enum SharedCapability { Admin, Join }`, `MintedLinks(string AdminToken, string JoinToken)`. Additive; versioning is additive to the account-sync API per ADR-0010 (new route group, existing routes unchanged).

**Client (LocalStore):**
- Extend `SyncedGroupRecord` → add `StorageMode Mode` (default `OnThisDevice`) and `SharedGroupTokens? Tokens` (admin/join token strings the device holds, nullable). Bump `SyncGroupDocument.CurrentSchemaVersion` 2→3; add the in-place migration in `LocalSyncStore` (old records read as `OnThisDevice`, null tokens — mirrors the v1→v2 lift). New records default `OnThisDevice`.
- Extend `ClaimableGroup` + `ClaimableGroupsService.Describe` with `Mode` so read surfaces can show it.

**Layering:** all in LocalStore/Contracts; Domain/`GroupState` untouched (D1).

**UI (Ui/Pages, Ui/Shared) + strings:**
- New `Shared/StorageModePicker.razor` (a `MudRadioGroup` of three options, each with a one-line explanation; *On this device* pre-selected). Used at creation and in settings.
- `Groups.razor` `CreateNamedAsync` / `CreateViaFabAsync`: show the picker (inline on wide, bottom sheet on phone). On confirm, create local as today; if a non-local mode is picked, route to the (stubbed until Phase C) upgrade action.
- `GroupEditor.razor`: add a small **mode chip** by the group name and a "Sharing & sync" `ct-hub-row` (stub screen `Pages/SharingAndSync.razor`, route `/groups/{GroupId:guid}/sharing`).
- Strings in all four resx (DE/EN/FR) using glossary display terms: *Über Link geteilt / Shared by link / Partagé par lien*, mode explanations, "Sharing & sync" (`Teilen & Sync` / `Partage et sync`), picker labels.

**Tests (TP-013):**
- Unit: schema v2→v3 migration keeps every group, defaults mode.
- bUnit: picker renders three options, *On this device* preselected (FR-038 AC), DE/EN/FR clean console.
- **E2E network-capture:** create a group without changing the picker → zero group/assignment writes server-side (the invariant to protect for the whole feature).

**Risks:** none material; this is inert scaffolding. Keep StorageMode out of `GroupState` or the layering fitness test fails (D1).

---

### Phase B — Server token-keyed store + upload/fetch by token (walking skeleton, NFR-009 core)

**Goal:** a real Postgres store for token-keyed groups with **unauthenticated-by-identity, token-authorized** upload/fetch, entropy + rate limits + size caps. Driven entirely by tests (no client yet).

**New context + schema** (`Main.Service.Repository`, new folder `Shared/`), mirroring the `sync` context seam:
- `SharedGroupDbContext` (const `Schema = "shared"`, own `__EFMigrationsHistory` in that schema).
- Entities:
  - `SharedGroupRow` — `GroupId (PK)`, `Body (jsonb)`, `Version`, `ServerModifiedAt`, `OwnerAccountId (Guid?, null while account-free)`, `CreatedAt`, `LastActivityAt` (drives NFR-010).
  - `SharedGroupToken` — `Token (PK, string)`, `GroupId`, `Capability (Admin|Join)`, `Active bool`, `CreatedAt`. Rotating = deactivate old, insert new (old stops resolving immediately).
  - `MemberClaim` — `GroupId + MemberId (PK)`, `ClaimKind (Device|Account)`, `DeviceHandle string?`, `AccountId Guid?`, `ClaimedAt`. First-claim-lock enforced by the PK + a guard (Phase E).
- `AddSharedGroupsRepository(connectionString)` (mirror `SyncRepositoryServiceCollectionExtensions`); register `SharedGroupDbContext.Database.Migrate()` in `Program.cs` alongside the other four.
- EF migration under `Migrations/Shared/` (follow the existing `Migrations/Sync|Linking` layout + `*ModelSnapshot`).

**Core port** (`Main.Service.Core/Domain`, new `ISharedGroupStore.cs`):
- `Task<MintedTokens> CreateAsync(SyncChange body, CancellationToken)` — generate group row + admin+join tokens.
- `Task<SharedGroupReadResult> GetAsync(string token, CancellationToken)` — resolve token → capability + body; wrong/inactive/expired token returns the **same** "not found" as a never-existed token (no oracle).
- `Task<SharedWriteResult> PushAsync(string token, long version, JsonElement body, CancellationToken)` — admin-capability write, LWW by version + `ServerModifiedAt` (reuse `SyncStore.ApplyChangeAsync` logic shape), refreshes `LastActivityAt`.
- `Task<MintedToken> RotateAdminAsync(string adminToken, CancellationToken)`; `Task DeleteAsync(string adminToken, CancellationToken)` (owner-less delete, NFR-010).
- Person-claim / release / claim-admin methods are added in Phases E–G (keep the port growing per phase).

**Token generation (NFR-009 + ADR-0050):** `RandomNumberGenerator.GetBytes(32)` → base64url (≥128-bit requirement satisfied at 256-bit). Two distinct tokens per group. Put generation behind a small `IShareTokenFactory` so it is unit-testable for entropy/uniqueness.

**Endpoints** (`Main.Service`, new `SharedGroupEndpoints.cs`), route group `/api/v{version}/shared` — **not** `RequireAuthorization()`; the token is the credential:
- `POST /shared` (create) → returns `MintedLinks`. `.RequireRateLimiting(<new account-free policy>)`.
- `GET /shared/{token}` (fetch; capability from token).
- `PUT /shared/{token}` (push; admin capability). Rate-limited.
- `POST /shared/{token}/rotate-admin`, `DELETE /shared/{token}`.
- Attach `ConstantTimeFilter` (already in the host) to the token-resolving routes so wrong-vs-nonexistent tokens are timing-indistinguishable.

**Anti-abuse (NFR-009):**
- New `AddRateLimiter` policy `AccountFreeWritePolicy` in `Program.cs` — fixed-window per `RemoteIpAddress` (reuse the exact pattern already there), separate ceilings for create vs write, config-driven (`RateLimiting:SharedCreate*`, `SharedWrite*`).
- **Size caps** enforced before store in the endpoint/store: max members, max occasions, max wishlist items, per-field lengths (names, gift notes) — reject (400) oversized/malformed, store nothing. Centralize caps in a `SharedGroupLimits` options record (mirror `CircleLimits`). Note: the body is opaque jsonb, so caps are validated by deserializing to a validation shape or by cheap structural checks (document the chosen approach; deserializing to the SS `GroupState` in this one store is acceptable since — like `LinkingStore` — this is a place the host may understand SS schema; keep it minimal).
- No amplification: one request → bounded work.

**Contracts/versioning:** additive; existing `sync`/`linking` routes untouched; reveal payload unchanged.

**Tests (TP-013):**
- Unit/property: token entropy ≥128 bits; sequential/guessed tokens never resolve (NFR-009 AC).
- Integration (Testcontainers/Aspire Postgres): store keyed by token, no `UserId`; fetch-by-token returns aggregate (FR-039); wrong vs non-existent token indistinguishable; rate-limit ceiling breaches → 429; over-cap payload rejected, nothing stored.
- Contract: admin token can push, join token cannot (403); capability bound to token.
- Architecture: new context/store sits in Repository behind the Core port; Domain I/O-free.

**Risks/watch:** cap-validation vs opaque-body tension (decide deserialize-in-this-store, documented). Ensure the rate-limit partition can't be trivially bypassed (per-IP is the hobby-scale bar ADR-0058 accepts). Keep the 4→5 context migration list in `Program.cs` in sync.

---

### Phase C — Client token-keyed `IGroupRepository` sync + upgrade/downgrade (completes the walking skeleton)

**Goal:** a shared-by-link group syncs to the `shared` store via its tokens, behind the unchanged `IGroupRepository`; local→shared upgrade and shared/account→local downgrade work with consequence sheets.

**Client port + engine** (`LocalStore`, new `Sync/Shared/`):
- `ISharedGroupApi` + `HttpSharedGroupApi` — mirror `ISyncApi`/`HttpSyncApi` but token-addressed, over the **`AccountApiClients.Anonymous`** HttpClient (no bearer). Methods: `CreateAsync`, `GetAsync(token)`, `PushAsync(token, version, body)`, `RotateAdminAsync`, `DeleteAsync`. Unreachable → `ApiFailure.Unreachable` value, never throw (same discipline as `HttpSyncApi`).
- `SharedSyncService` — mirror `SyncService` but scoped to **one group per token**: push if `Dirty`, pull if server `Version` newer, LWW per aggregate (reuse the exact version-compare rules). A single aggregate per token means a trivial cursor; reuse `SyncedGroupRecord.Version` as the client half of LWW. Writes stay local-first-dirty (the existing `SyncingGroupRepository.SaveAsync` already bumps version + dirty — mode-agnostic, unchanged).
- Upgrade/downgrade live in `SharedSyncService` (out-of-cycle actions, like `ClaimAsync`/`UnclaimAsync`):
  - `ShareByLinkAsync(groupId)` → `CreateAsync(body)`; on success set record `Mode=SharedByLink`, store minted `Tokens`, clear dirty. Mirrors `ClaimAsync` (upload-then-flip-scope).
  - `MakeLocalAsync(groupId)` → if account-free shared, `DeleteAsync(adminToken)` (owner-less delete) then set `Mode=OnThisDevice`, clear tokens, **keep local copy** (a delete of the server copy, not a tombstone — mirrors `UnclaimAsync`). If it's a `My account` group, existing `UnclaimAsync` still applies.

**Orchestration** (`Ui/Sync`):
- `AutoSyncService.RunCycleAsync`: after the account cycle, add a shared pass — for every record with `Mode==SharedByLink` and tokens, run `SharedSyncService` per group. Runs signed-in **or out** (a shared group has no account requirement) — so relax the "signed out is a no-op" guard specifically for shared groups. Keep debounce/coalesce.
- `VisibleGroupsService`: shared-by-link groups are visible **regardless of sign-in** (they're not account-scoped) — extend the filter: signed out shows account-free-local **and** shared-by-link groups; account groups still hidden.

**UI + strings:**
- `SharingAndSync.razor` (from Phase A stub): show current mode; buttons to switch. Each switch shows a `ConfirmSheet` (`ShowConfirmSheetAsync`) with a **one-line before→after** consequence (FR-038): e.g. "This device only → Shared by link: your group is uploaded and reachable by link." / downgrade: "Shared by link → This device only: the online copy and its links stop working; your copy stays on this device."
- Wire `Groups.razor` create picker (Phase A) to call `ShareByLinkAsync` when *Shared by link* is chosen at creation.
- Strings: consequence lines (upgrade/downgrade) DE/EN/FR.

**Tests (TP-013):**
- Integration + E2E: two devices holding a link converge LWW per aggregate (FR-039); offline edit then reconnect syncs.
- Integration: downgrade removes server copy, keeps local, links stop resolving (FR-038 AC, NFR-010 explicit-delete).
- E2E: full local flow still zero server writes (invariant).
- Architecture: token sync sits behind `IGroupRepository`; SyncLayeringTests green (mode/tokens on the record, not the memento).

**Risks/watch:** the anonymous client must never attach a bearer even when signed in (a shared group is token-authorized). Ensure a signed-in user who *also* holds a shared group doesn't accidentally double-home it in `sync`. Concurrency of multi-holder writes is LWW (accepted, ADR-0016) — no conflict UI (non-goal).

---

### Phase D — Admin link: capability, rotation, share seam (FR-040, UC-SS-018)

**Goal:** the Sharing & sync screen surfaces the admin link with Share/Copy/QR and disable-&-regenerate.

**Server:** already have `GET/PUT /shared/{token}` (admin capability) and `POST /shared/{token}/rotate-admin` (Phase B). Confirm admin-capability is required for PUT/rotate/delete; join token → 403.

**Client:** `SharedSyncService.RotateAdminAsync(groupId)` → server rotate, replace stored admin token, old stops resolving.

**UI + strings** (`SharingAndSync.razor`), reusing the exact `Share.razor` seam:
- Admin-link section: Share button when `IShareService.IsSupportedAsync()`, Copy everywhere, "Show QR" via `ShowQrDialogAsync` (all already used in `Share.razor`). Build the link URL as `{origin}/join?…` for join and `{origin}/admin#token=…` for admin (token in the fragment, "the link is the secret", like reveal links keep the payload in `#`).
- "Disable & create a new admin link" button → confirm sheet → `RotateAdminAsync`.
- Strings: "Admin link / Admin-Link / Lien admin", "Disable & create new", share/copy/QR labels (mostly reuse existing `Share/Copy/ShowQr`), DE/EN/FR.

**Tests:** integration — two holders of one admin link both manage (FR-040 shared-capability); rotate → old 404, new works. bUnit — section renders Share/Copy/QR. E2E — rotate flow.

**Risks/watch:** never render the raw token where screenshots leak it beyond what the link already exposes (same trust model as reveal links). QR encodes the full admin URL.

---

### Phase E — Join link + person-claim + own-assignment (FR-041, NFR-002, UC-SS-019)

**Goal:** open the join link → "who are you?" (identity × persistence) → claim a member (first-claim-locks) → see only your own assignment.

**Server** (`ISharedGroupStore` + `SharedGroupEndpoints`):
- `GET /shared/{joinToken}/roster` — join-capability read of claimable members (names + claimed/free), never assignments.
- `POST /shared/{joinToken}/claims` `{ memberId?, newMemberName?, persistence: Device|Account, deviceHandle? }` — enforce **first-claim-lock** via the `MemberClaim` PK + guard: a claimed member cannot be re-claimed by anyone (device **or** account); a device claim is **not** overridden by a later account claim. Account persistence reuses member↔user linking (ADR-0019/0043) — record the account on the claim; device persistence records a device handle. Adding a new person mutates the aggregate body (append member) under admin-equivalent write inside the claim, then locks it to the claimer.
- `GET /shared/{joinToken}/my-assignment?claim=…` — returns **only the claimed member's** assignment (reuse the `AssignmentView`/reveal payload shape and the **Found / NotDrawn / Denied** discipline from `LinkingStore`/`LinkingEndpoints`; a claim for a member you don't hold → Denied, never filtered-to-empty). This is the NFR-002 trust-bounded read.
- Admin token retired state (from Phase G) surfaces here; join keeps working.

**Client:**
- `ISharedGroupApi` gains roster/claim/my-assignment calls (anonymous client).
- Persist the device's own claim (which member am I, which token) in the `SyncedGroupRecord.Tokens`/a small claim field so the app shows "your assignment" on return without re-asking.
- Account persistence path: perform the claim, then reuse the existing `SelfLinkAsync`/`LinkResolutionApplier` plumbing where the joiner is signed in.

**UI + strings** (new `Pages/Join.razor`, route `/join`; token from fragment):
- Deep link: add a `/join` App Link intent-filter alongside the existing reveal `/r` in `Platforms/Android/MainActivity.cs` + `DeepLinkService` (forward `j#token=…` / `join?…`). iOS falls back to browser/PWA (MNF-10) — surface a one-line note in the UI (FEAT-013 risk).
- "Who are you?" flow: choose existing member **or** add new person; choose **account** (durable) or **just this device**. Reuse `TextInputSheet` for the new-name case, `ConfirmSheet` for confirmation. All four combinations valid (FR-041).
- After joining: the group opens; the participant edits **their own** wishlist/gift note (existing per-member editors) and, once drawn, sees **their own** assignment (render via reveal payload shape). "Already claimed" state for a locked member.
- The person-claim product phrasing from the glossary: *Das bin ich / This is me / C'est moi*. Strings for the whole flow DE/EN/FR.

**Tests (TP-013):**
- Unit + Integration: first-claim-lock (all four identity×persistence combos land; re-claim refused; device claim not overridden by later account claim) — FR-041 ACs.
- Integration: a claimant's my-assignment carries only their member's assignment, never another's; non-holder → Denied (NFR-002 ACs).
- E2E: join-via-link (device-only) sees own assignment after draw; join link still works after claim-admin (Phase G dependency — test lands with G).
- bUnit: the join flow + "already claimed" state, DE/EN/FR.

**Risks/watch:** device-handle stability across reinstalls (open question in FEAT-013) — if unstable, recovery is admin release (Phase F), which is the intended single mechanism; don't over-engineer the handle. Insider-spoiler is accepted (trust-bounded); locking is the only guard.

---

### Phase F — Admin release of a claimed person (FR-042, UC-SS-020)

**Goal:** an admin frees a locked member so the join link can claim it afresh; wishlist/gift-note/assignment preserved.

**Server:** `DELETE /shared/{adminToken}/claims/{memberId}` — admin capability only; removes the `MemberClaim` row (not the member), immediate. Join token attempting release → 403.

**Client:** `ISharedGroupApi.ReleaseClaimAsync(adminToken, memberId)`.

**UI + strings:** on `Members.razor` for a shared-by-link group (admin), add a per-member "Release" action (overflow menu) with a confirm sheet; hidden for join-only holders. Member reads as "not claimed" after. Strings DE/EN/FR.

**Tests:** Integration + UI — release frees a device/account claim; member re-claimable; wishlist/gift-note/assignment unchanged; non-admin has no action; after re-claim only the new claimant sees the assignment (FR-042 ACs).

**Risks:** none new; this is the sanctioned recovery path (no self-service by design).

---

### Phase G — Claim-admin & release-admin (FR-043a, UC-SS-018)

**Goal:** Shared by link ⇄ My account, per D2, preserving assignments and existing claims.

**Server** (`ISharedGroupStore`):
- `POST /shared/{adminToken}/claim-admin` — **bearer-authorized** (the claimer signs in): set `OwnerAccountId = caller`, deactivate admin token(s), keep join token active, record admin-claimed display name. Reuses the anonymous→account claim shape (ADR-0017/0044).
- `POST /shared/{joinToken|adminToken}/…` GET now returns `AdminRetired=true` + `AdminClaimedByName` when an admin token is presented for an account-owned group (FR-040 retired-link message).
- Owner-authorized read/write variant: when `OwnerAccountId` set, the owning account may PUT via bearer (caller == owner) in addition to token holders' join reads.
- `POST /shared/{…}/release-admin` — owner-authorized: clear `OwnerAccountId`, mint a fresh admin token (join token unchanged), assignments + `MemberClaim` rows preserved.
- **Native account group → Shared by link** (cross-store move): a new owner-authorized action that reads the account aggregate from the `sync` store, inserts a `shared` row (`OwnerAccountId=null`), mints admin+join tokens, then `UnclaimAsync` on the `sync` copy (server copy removed, local kept). This is the only cross-store path; implement carefully in one transaction per store with the sync-copy removal last (fail-safe order, mirroring the deletion-cleanup ordering rationale).

**Client:**
- `claim-admin`: from Sharing & sync while signed in → call, then flip the record to `Mode=Account`, drop the admin token, keep the group syncing (now via the account/owner path). Retire the local admin link.
- `release-admin`: from Sharing & sync (owner) → call, store the new admin token, flip to `Mode=SharedByLink`.
- Route the owning account's writes for a claimed shared group through the owner-authorized shared endpoint (bearer), not the opaque `sync` store, so join holders keep converging.

**UI + strings:** Sharing & sync gains "Claim admin (sign in)" and "Release to shared by link" with consequence sheets; the retired-admin-link screen ("<name> has claimed the admin role for this group" — DE/EN/FR).

**Tests (TP-013):**
- Integration + E2E: claim-admin retires admin link (old admin link shows the message), **join link still works** and participants keep access + assignment (FR-040/FR-041 ACs).
- Integration + E2E: release-admin re-keys to fresh admin link, preserves assignments + person claims (device stays device, another member's account claim untouched) — FR-043 AC.
- Integration: native account group → shared by link cross-store move (assignments preserved, sync copy gone, local kept).

**Risks/watch:** the dual write authority on a claimed shared row (token join reads + owner bearer writes) — keep authorization explicit and tested. Ensure "one owner, no ambiguity" (ADR driver): claim-admin is first-wins on `OwnerAccountId`.

---

### Phase H — Account-deletion disposition (FR-043b) + retention/TTL lifecycle (NFR-010)

**H-retention (can start after B):**
- Background sweeper mirroring `AddCirclesRetention` / `PruneMarkersAsync`: purge `shared` rows whose `LastActivityAt` is older than a generous configured window (`OwnerAccountId==null` owner-less groups). Every read/write refreshes `LastActivityAt`.
- Warned-not-surprised: client surfaces an approaching-expiry notice on open (compute from `LastActivityAt` returned on GET); any activity resets it.
- Local copy never destroyed by server expiry/delete: on the next open of an expired/deleted group, the client flips it to `OnThisDevice` locally (already the downgrade path) — no data loss.
- Tests: advance clock past window → purged; local survives; any admin-link holder can delete online copy → links 404-equivalent (NFR-010 ACs).

**H-deletion (depends on G):**
- Extend `DeleteAccountAsync` (`AccountEndpoints.cs`) via a new `IAccountDeletionCleanup`-style participant, `SharedGroupAccountDisposition`, plus a **per-group disposition** carried on `DeleteAccountRequest` (additive field): for each shared group the account owns (`OwnerAccountId==caller`):
  - **Hand off as shared by link** (default when others joined) → run release-admin (clear owner, mint admin token); group stays online, everyone keeps access.
  - **Keep on this device** (default when owner is sole participant) → remove online copy; local survives.
  - **Delete the online copy** → purge the `shared` row + tokens.
- The deletion confirmation screen (ADR-0022 requirement) must **enumerate** the owned shared groups + chosen disposition. UI: extend the existing account-deletion confirm surface in the account UI to list them (compute "others joined?" from `MemberClaim` count).
- Order: dispositions run before the account row delete (same fail-safe ordering as existing cleanups).

**Tests (TP-013):** Integration + E2E — deletion routes each owned group (hand-off default when others joined; keep-on-device when sole; explicit delete); confirmation enumerates; after any disposition the deleting user's **local copy is intact** (FR-043 / NFR-010 ACs).

**Risks/watch:** the "others joined?" default computation must be reliable (claim count). Retention window value is a product decision (document it in config, generous per NFR-010).

---

## 3. Reuse vs genuinely new

- **Reused (shape or code):** the `IGroupRepository` seam and local-first-dirty write; `SyncedGroupRecord` metadata-wrapper pattern; LWW version + `ServerModifiedAt`; the `SyncStore` transaction/upsert discipline; the `LinkingStore` Found/NotDrawn/Denied privacy pattern and "server may understand SS schema in one place"; the reveal/`AssignmentView` payload for own-assignment; `ConfirmSheet`/`TextInputSheet`/`QrDialog`/`IShareService`/hub-row/FAB UI; `AddRateLimiter` per-IP + `ConstantTimeFilter`; `IAccountDeletionCleanup` fan-out; `DeepLinkService` App-Link path; `AccountApiClients.Anonymous` client; four-context migration + per-schema history convention.
- **Genuinely new:** the `shared` schema/context + `SharedGroupRow/Token/MemberClaim`; token-authorized (identity-free) endpoints + `IShareTokenFactory`; the client `ISharedGroupApi`/`SharedSyncService` token cycle; the join "who are you?" flow + person-claim first-claim-lock; claim-admin/release-admin `OwnerAccountId` flag and the one cross-store move; owner-less retention sweeper; the per-group deletion disposition.

## 4. Follow-up ADRs

**None required.** ADR-0058 plus FR-038..043 and NFR-002/009/010 fully cover the build. The two decisions this plan pins down (D1 metadata layering, D2 physical home + claim/release mechanics) are implementation choices consistent with existing ADRs (0015/0016/0033 for D1; 0017/0020/0022 for D2), not new architectural commitments. Flag only if implementation reveals that device-handle instability (open question) forces a design change beyond admin-release recovery — then a short amendment, not a new ADR.

---

## 5. Critical files for implementation

- `src/Apps/SecretSanta/CircleTools.SecretSanta.LocalStore/Sync/SyncModel.cs` (extend `SyncedGroupRecord`; schema bump) and `LocalStore/Sync/SyncService.cs` / new `Sync/Shared/SharedSyncService.cs` (client token cycle + upgrade/downgrade).
- `src/Apps/Main/CircleTools.Main.Service.Repository/Sync/` → new `Shared/SharedGroupDbContext.cs` + entities + `Migrations/Shared/` (model this on `SyncDbContext.cs`/`SyncEntities.cs`/`SyncStore.cs`).
- `src/Apps/Main/CircleTools.Main.Service/SharedGroupEndpoints.cs` (new; token-authorized) + `Program.cs` (route group, rate-limit policy, 5th context migration).
- `src/Apps/SecretSanta/CircleTools.SecretSanta.Ui/Pages/SharingAndSync.razor` (new) + `Pages/Join.razor` (new) + `Pages/Groups.razor` / `GroupEditor.razor` (mode picker + chip) and `Resources/AppStrings.{resx,de-CH,en,fr-CH}.resx`.
- `src/Apps/Main/CircleTools.Main.Service/AccountEndpoints.cs` + `Main.Contracts/AccountDtos.cs` (per-group deletion disposition, FR-043b) — the ADR-0022 extension point.
