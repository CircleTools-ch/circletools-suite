---
status: Accepted
date: 2026-07-28
deciders: [Adrian]
tags: [architecture, ui, frontend, building-blocks, layering]
---

# ADR-0025 — Shared UI building blocks

## Context and problem statement

[arc42 ch.5](../arc42/05-building-block-view.md) has planned `Ui.Abstractions`, `Ui.Shared` and an
API client since the first draft, but none of them existed: Secret-Santa is local-first and needed no
API client, and with one app there was nothing to share. Its `AppCultures`, its `CultureSelector` and
its `IKeyValueStore` all live inside `CircleTools.SecretSanta.Ui`.

v2's Main client changes both halves of that. It is the first API-consuming client — bearer tokens,
silent refresh, `ProblemDetails` — and it is the second app that needs a language picker, a form
shell and browser storage. Copying Secret-Santa's versions would put the suite's second copy of the
culture logic in the tree on the day the first one stopped being the only one.

There is also a constraint that will not show up for a while and is expensive to retrofit: a **MAUI
Blazor Hybrid head** is a stated target. Its feature code must be the same feature code. Anything the
pages touch that only a browser can do — `localStorage`, `navigator.credentials`, a download — has to
sit behind a seam from the start, because introducing one later means editing every call site.

**What is shared between app clients, where does it live, and what may depend on what?**

## Decision drivers

- The layering rule in the [architecture guidelines](../guidelines/architecture-guidelines.md) §4:
  dependencies point inward, and an app's UI may not reach into another app or into the server.
- A future MAUI head must reuse feature code unchanged (ADR-0005, ADR-0009).
- Two apps is the point at which sharing pays; three is the point at which not sharing hurts.
- Secret-Santa is **shipped and working**. Any restructuring that risks it is not worth it.
- Every user's stored language preference must survive — the key it is stored under is part of the
  data, not an implementation detail.

## Considered options

1. **Four building blocks**: `Ui.Abstractions` (seams), `Localization`, `ApiClient`, `Ui.Shared`
   (components + theme).
2. **One `Ui.Shared` block** containing all of it.
3. **Copy what is needed into `Main.Ui`** and extract later, when a third app makes it obvious.

## Decision outcome

**Chosen: option 1 — four blocks**, under `src/BuildingBlocks/`:

| Block | Contains | May reference |
|---|---|---|
| `CircleTools.Ui.Abstractions` | `IKeyValueStore`, `ICultureProvider`, `IPasskeyCeremony`, `IFileSaver`, `PasskeyCeremonyException` | nothing |
| `CircleTools.Localization` | `AppCultures`, `CultureOptions`, `CultureProvider`, DI extension | `Ui.Abstractions` |
| `CircleTools.ApiClient` | `ApiResult`/`ApiError`, `ApiResponseReader`, token stores, `AuthorizedHttpMessageHandler`, `ISessionRefresher` | `Ui.Abstractions` |
| `CircleTools.Ui.Shared` | `AuthCard`, `BusyButton`, `ProblemAlert`, `CultureSelector`, `CultureBoundary`, `circletools.css` | `Localization`, `Ui.Abstractions` |

And the rules that make them worth separating:

- **`Ui.Abstractions` references nothing.** Not `Microsoft.JSInterop`, not the API client, not a
  package. The moment it references a platform it stops being a seam. This is enforced by an
  architecture test, not by good intentions.
- **`ApiClient` holds no app's DTOs and no routes.** The refresh endpoint belongs to an app, which is
  why `ISessionRefresher` is an interface here and an implementation in `Main.Ui`.
- **A host supplies the platform.** `Main.WebApp` provides `BrowserKeyValueStore`,
  `BrowserPasskeyCeremony`, `BrowserFileSaver` and `BrowserCredentialsHandler`; a MAUI head would
  provide four different classes and change nothing else.
- **`<App>.Ui` may reference these four, plus its own `Contracts`. Never `Identity`, never EF, never
  `Service.Core`.** Also enforced by an architecture test.

Four blocks rather than one, because they have genuinely different dependency needs and different
audiences. A MAUI head implements `Ui.Abstractions` and never touches `Ui.Shared`'s Razor components
if it renders its own; a test project references `ApiClient` alone. Folding them together would give
the Razor SDK and a web-component dependency to code that has no business with either.

**One thing is deliberately not done: Secret-Santa is left alone.** It keeps its own `AppCultures`,
its own `CultureSelector` and its own storage key. Migrating it is a genuine improvement and a
genuine risk to a working app, for no user-visible gain; it is a follow-up, not part of this change.
`CultureOptions.StorageKey` exists precisely so that migration can happen later without anyone losing
their language preference — Secret-Santa keeps `secretsanta.culture`, Main uses `main.culture`.

> **Update (2026-07-28) — the migration has happened.** Secret-Santa now uses
> `CircleTools.Localization` and `CircleTools.Ui.Shared`, and `secretsanta.culture` survived it, as
> `CultureOptions` was designed to allow. Carrying it out forced two amendments recorded as their own
> ADRs: [ADR-0028](0028-browser-platform-adapters-block.md) moves the browser adapters into a shared
> `Ui.Web` block, because "a host supplies the platform" stopped being the right rule once there were
> two web hosts wanting the identical adapter; and
> [ADR-0029](0029-storage-write-failure-contract.md) changes `IKeyValueStore.SetAsync` to report
> whether the write stuck, because this block's swallow-everything contract — correct for a
> preference — would have meant silent data loss for the only copy of a Secret-Santa group.

## Consequences

- **Positive:** arc42 ch.5 now describes the code instead of an intention; the MAUI head becomes a
  matter of four implementations and a composition root; the layering is checked by a test rather
  than by review.
- **Positive:** the account pages are testable without a browser — bUnit composes the real API client
  over a stub transport, because nothing in the path needs JS interop.
- **Negative / accepted:** four projects instead of one, and a solution that is four entries longer.
- **Negative / accepted:** the suite temporarily has **two** culture implementations — the shared one
  and Secret-Santa's. That is the cost of not touching a working app, and it is a real cost: a change
  to language handling has to be made in two places until the migration happens. *(Closed 2026-07-28
  — there is one implementation again.)*
- **Negative / accepted:** `CircleTools.Ui.Shared` trips CA1716 (`Shared` is a Visual Basic keyword).
  The name is fixed by arc42 and the suite is C#-only, so the rule is suppressed in that project with
  the reason recorded there.
- **Follow-ups:** ~~migrate `CircleTools.SecretSanta.Ui` onto `CircleTools.Localization` and
  `CircleTools.Ui.Shared` for `AppCultures`/`CultureSelector`, keeping its existing storage key; move
  Secret-Santa's `IKeyValueStore` to `Ui.Abstractions` at the same time.~~ **Done 2026-07-28** — see
  the update above, [ADR-0028](0028-browser-platform-adapters-block.md) and
  [ADR-0029](0029-storage-write-failure-contract.md).
- **New risks:** `Ui.Shared` can become the place things go when nobody knows where they go. The
  boundary that keeps it honest is stated in [ADR-0024](0024-ui-styling-and-theming.md): shared means
  used by more than one app, and nothing else.

## Pros and cons of the options

### 1. Four blocks (chosen)
- 👍 Each block has an honest dependency set; a MAUI head takes only what it needs; matches arc42.
- 👎 More projects; two culture implementations until Secret-Santa migrates.

### 2. One `Ui.Shared`
- 👍 Fewest projects; nothing to decide about where a thing goes.
- 👎 Forces the Razor SDK and web-component references onto pure abstractions and pure HTTP code;
  makes the "may reference nothing" rule unstateable, and therefore unenforceable.

### 3. Copy into `Main.Ui`, extract later
- 👍 Nothing new today.
- 👎 Two copies of the culture logic on day one, and the platform seams the MAUI head needs never get
  built — which is the part that is expensive to retrofit, because it touches every call site.

## Links

- Related ADRs: [ADR-0005](0005-web-hosting-render-model.md),
  [ADR-0011](0011-localization-format-and-culture.md),
  [ADR-0021](0021-web-auth-token-model.md), [ADR-0024](0024-ui-styling-and-theming.md),
  [ADR-0026](0026-refresh-token-persistence.md)
- arc42: [05 Building-block view](../arc42/05-building-block-view.md)
- Guidelines: [architecture](../guidelines/architecture-guidelines.md) §4,
  [coding](../guidelines/coding-guidelines.md)
- Feature: [FEAT-002](../features/FEAT-002-m1-identity.md)
