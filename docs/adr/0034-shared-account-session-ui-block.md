---
status: Accepted
date: 2026-08-11
deciders: [Adrian]
tags: [architecture, ui, auth, building-blocks, sync]
---

# ADR-0034 — Shared account/session UI building block (`Ui.Account`)

## Context and problem statement

M3's claim/un-claim slice ([FEAT-004](../features/FEAT-004-m3-sync-and-claim.md)) makes the
local-first Secret-Santa app sign in against the **Main service** so a signed-in account can claim its
groups. Signing in and holding a session is not new code — the Main app's Blazor client already does
all of it: `AccountSession` (the one place that writes the token stores and follows the account's
language), `AccountApiClient` (the typed client over `/api/v1/account`), `TokenAuthStateProvider` +
`AccountClaims` (the principal derived from the bearer token, ADR-0021), and `SessionRefresher` (the
ADR-0021/0026 refresh). All of it lives in **`AppSuite.Main.Ui`** today.

Secret-Santa needs exactly this behaviour against the same backend. **Where should the account/session
client code live so both frontends share one implementation, without inverting the layering the
fitness functions guard?** The options considered at plan time were "reference `AppSuite.Main.Ui`
directly from `AppSuite.SecretSanta.Ui`" (rejected — it couples two app UI assemblies) versus
"extract a shared building block"; this ADR records the extraction and, crucially, **where its
boundary falls**.

Two facts discovered while tracing the code shape the decision:

- The account/session plumbing is **host-neutral**: it depends only on building blocks
  (`AppSuite.ApiClient`, `AppSuite.Localization`, `AppSuite.Ui.Abstractions`) plus
  **`AppSuite.Main.Contracts`** — the shared backend contract that `AppSuite.SecretSanta.LocalStore`
  **already references** for the M3 sync engine. Nothing in it touches Blazor WebAssembly or JS
  interop. It is a clean building-block candidate.
- The account **Razor pages** (`Login`, `Register`, `ForgotPassword`, …) additionally bind to
  `AppSuite.Main.Ui`'s own `AppStrings` resources and to `ApiErrorText`. The shared UI components they
  use (`AuthCard`, `ProblemAlert`, `BusyButton`) are **already** in the `AppSuite.Ui.Shared` building
  block, so those do not move — only the pages' strings and their host page would.

## Decision drivers

- **Respect the layering (CLAUDE.md rule 6; ADR-0025/0028).** Dependencies point inward. A shared
  block must not depend on an app; the fitness functions in `SharedUiBlockLayeringTests` state each
  block's allowed dependency set explicitly.
- **One implementation of session/token handling.** `AccountSession`'s whole reason to exist is that
  "a page that does three of the four session steps is a bug that only shows up on someone else's
  screen." Two copies of that across two apps is precisely the drift it was built to prevent.
- **Do not disturb the shipped Main app more than necessary.** Main's account pages and their strings
  are working and tested (FEAT-002, `Reviewed`). Moving live UI and its RESX out of a shipped app is
  real risk that this slice does not need to take.
- **Match the precedent already set.** `LocalStore` referencing `AppSuite.Main.Contracts` established
  that the Main service's contract assembly is the shared backend contract, referenced across apps.
  A new UI block depending on it is the same accepted edge, not a new kind of coupling.
- **Keep each app's screens its own.** The Secret-Santa login screen is a handful of fields; owning it
  in `SecretSanta.Ui` lets the app phrase and place it its own way, and keeps the account **pages** out
  of the cross-app dependency even while the account **plumbing** is shared.

## Considered options

1. **Extract the plumbing only into `AppSuite.Ui.Account`; each app keeps its own thin login page.**
   The new block holds `AccountSession`, `AccountApiClient`, `TokenAuthStateProvider`/`AccountClaims`,
   `SessionRefresher`/`ISessionRefresher`, the API-client keys/routes (renamed from `MainApiClients`),
   `ApiErrorText`, and the DI extension. It references `ApiClient` + `Main.Contracts` + `Localization`
   + `Ui.Abstractions`. `Main.Ui` re-points its `using`s and delegates `AddMainAccount()` to the
   block; its account pages are unchanged. `SecretSanta.Ui` references the block and writes a small
   `Login.razor` (reusing `AuthCard`/`ProblemAlert`/`BusyButton` from `Ui.Shared`).
2. **Extract the plumbing *and* the account pages + their strings into `AppSuite.Ui.Account`.** Both
   apps render the identical `Login`/`Register`/… screens from the block. Maximises reuse but migrates
   live pages and RESX out of the shipped Main app.
3. **Reference `AppSuite.Main.Ui` directly from `AppSuite.SecretSanta.Ui`.** No new block; couples the
   two apps' UI assemblies. (The rejected plan-time option, restated for completeness.)

## Decision outcome

**Chosen: Option 2.** Create a new building block **`src/BuildingBlocks/AppSuite.Ui.Account`** holding
both the account/session client plumbing **and** the account Razor pages with their own RESX strings,
referencing `AppSuite.ApiClient`, `AppSuite.Main.Contracts`, `AppSuite.Localization`,
`AppSuite.Ui.Abstractions`, and `AppSuite.Ui.Shared` (for `AuthCard`/`ProblemAlert`/`BusyButton`).
`AppSuite.Main.Ui` consumes the block instead of owning these types and pages: its `AddMainAccount()`
becomes a thin wrapper over the block's registration, and it keeps only its Main-portal surface
(dashboard, nav, circles/invitations). `AppSuite.SecretSanta.Ui` references the block, and the
Secret-Santa WebApp registers the two keyed `HttpClient`s (anonymous/authorized) plus the browser
passkey seam, exactly as the Main WebApp does (ADR-0013/0021/0026). Both apps therefore render the
**identical** account screens (`Login`, `Register`, `ForgotPassword`, `ResetPassword`, `VerifyEmail`,
`ConfirmEmailChange`, `Profile`, `Security`, `AccountData`) from one source.

The account subset of Main.Ui's `AppStrings` moves into the block's own `AccountStrings` RESX
(DE/EN/FR); Main-portal strings (nav, dashboard, circles) stay in `Main.Ui.AppStrings`. `ApiErrorText`
moves with the block (both apps need it); `CircleErrorText` and the circle/invitation route constants
stay in `Main.Ui`.

Option 1 (plumbing only, per-app login pages) is set aside in favour of one source of truth for every
account screen. Option 3 is rejected as it couples two app UI assemblies, which is what a building
block exists to avoid.

### Layering and naming

- The block's namespace is `AppSuite.Ui.Account` — **not** matched by the `^AppSuite\.(Main|SecretSanta)`
  "app code" pattern, so it is not app code. Its dependency on `AppSuite.Main.Contracts` is the same
  edge `LocalStore` already has; a new `Ui.AccountLayeringTests` fitness function will state the
  block's allowed set (ApiClient, Localization, Ui.Abstractions, Main.Contracts) and forbid Blazor
  WebAssembly / JS interop, mirroring `SharedUiBlockLayeringTests`.
- The API-client keys/routes currently named `MainApiClients` are renamed to a neutral
  `AccountApiClients` in the block (the constants are unchanged values); `Main.Ui`'s
  `CirclesApiClient`/`CircleErrorText` and the circle/invitation route constants stay in `Main.Ui`,
  since circles are Main-only (ADR-0033).
- No separate ADR is needed for "how Secret-Santa authenticates against Main": it is a second instance
  of the WASM↔API wiring already decided in [ADR-0013](0013-wasm-api-dev-wiring.md),
  [ADR-0021](0021-web-auth-token-model.md) and [ADR-0026](0026-refresh-token-persistence.md). This ADR
  only records where the shared *client code* lives.

## Consequences

- **Positive:** one implementation of session/token/refresh behaviour, shared by both frontends and by
  a future MAUI head; the Secret-Santa app gains real sign-in without duplicating security-adjacent
  code. `AccountSession`'s "one place" invariant holds across apps.
- **Positive:** every account screen has a single source of truth — a fix to sign-in, verification or
  the export page lands in both frontends and a future MAUI head at once; no per-app login page to
  drift.
- **Negative / accepted:** migrating live pages and their RESX out of the shipped, tested Main app is
  real churn with real risk. It must land with the full test suite (incl. `AppSuite.Main.Ui.Test`) and
  the fitness functions green before any claim UI is built, and the account strings must split cleanly
  from the Main-portal strings that stay behind.
- **Negative / accepted:** the block now carries routable `@page` pages, so both hosts expose the same
  `/account/*` routes; each app's router must include the block's assembly.
- **Follow-ups:** update [arc42 ch. 5](../arc42/05-building-block-view.md) to add the block to the
  building-block view; `Ui.AccountLayeringTests` is added with this slice; move `AppSuite.Main.Ui.Test`'s
  account-page tests alongside the block (they stay in `Main.Ui.Test` for now, re-pointed at the block's
  namespaces); the MAUI head can adopt the block in a later slice.
- **Follow-up (string pruning):** the block owns the authoritative account strings (`AccountStrings`).
  `Main.Ui.AppStrings` still contains the now-unused account keys as **dead entries** — deliberately
  left in place for this slice rather than surgically removed from a live 130-key resource mid-refactor
  (exactly the kind of edit that silently drops a key the shipped Circles/AccountData/nav paths still
  read). `AccountData`, `CircleErrorText`'s `ApiErrorText` fallback, and the nav still legitimately use
  `Error*`/`Password*`/`Nav*` from `Main.Ui.AppStrings`, so only the purely-moved keys are prunable; that
  cleanup is a separate, test-guarded follow-up.
- **New risks:** the block depends on `Main.Contracts`, so a change to the account DTOs now ripples to
  a shared block. Bounded — those DTOs are the backend's account contract, which both frontends must
  track regardless.

## Pros and cons of the options

### 1. Extract plumbing only; each app keeps its own login page
- 👍 Shares the session/token code that matters; leaves the shipped Main app's pages/strings untouched.
- 👍 Reuses the precedented `Main.Contracts` edge; clean, statable fitness set for the new block.
- 👎 Small duplication: two login pages/screens rather than one, which can drift apart.

### 2. Extract plumbing *and* the account pages + strings (chosen)
- 👍 Pixel-identical account screens from one source; zero page duplication; every fix lands everywhere.
- 👎 Migrates live pages and RESX out of a shipped, tested app — real risk; enlarges the blast radius
  beyond the minimum the claim slice strictly needs.

### 3. Reference `AppSuite.Main.Ui` directly from `SecretSanta.Ui`
- 👍 Zero new projects; immediate reuse.
- 👎 Couples two app UI assemblies — the coupling a building block exists to prevent; drags Main's
  circles/dashboard surface into Secret-Santa's dependency graph.

## Links

- Related ADRs: [ADR-0025](0025-shared-ui-building-blocks.md) (the shared-block set this extends),
  [ADR-0028](0028-browser-platform-adapters-block.md), [ADR-0021](0021-web-auth-token-model.md),
  [ADR-0026](0026-refresh-token-persistence.md), [ADR-0013](0013-wasm-api-dev-wiring.md),
  [ADR-0033](0033-which-aggregates-sync.md)
- arc42 chapters: [05 Building-block view](../arc42/05-building-block-view.md)
- Requirements / use cases:
  [FR-025](../requirements/functional/FR-025-claim-local-groups.md),
  [FR-026](../requirements/functional/FR-026-remove-group-from-account.md),
  [FR-008](../requirements/functional/FR-008-sign-in-with-password.md)
- Feature: [FEAT-004](../features/FEAT-004-m3-sync-and-claim.md)
