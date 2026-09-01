---
status: Accepted
date: 2026-07-28
deciders: [Adrian]
tags: [architecture, ui, frontend, building-blocks, layering, maui]
---

# ADR-0028 — Browser platform adapters as a shared block

## Context and problem statement

[ADR-0025](0025-shared-ui-building-blocks.md) created four UI building blocks and set the rule that
**a host supplies the platform**: `Main.WebApp` registered its own `BrowserKeyValueStore`,
`BrowserFileSaver`, `BrowserPasskeyCeremony` and `BrowserCredentialsHandler`, and a MAUI head would
supply four different classes.

That rule was written when there was exactly one web host. Migrating Secret-Santa onto the shared
blocks (ADR-0025's recorded follow-up) made it a **second** web host, and it needs the same
`IKeyValueStore` implementation over `localStorage` that Main already has — for the same reason,
under a different key.

**Where does a platform adapter live once more than one host needs the identical one?**

## Decision drivers

- ADR-0024's boundary rule: *shared means used by more than one app, and nothing else*. A browser
  key/value store now clears that bar; before this migration it did not.
- A MAUI head must be able to reference `Ui.Shared`'s Razor components — the language picker, the
  culture boundary — **without** inheriting browser-only JS interop it cannot satisfy.
- `Ui.Abstractions` references nothing, by rule and by fitness function. The implementations cannot
  go there.
- Secret-Santa is shipped and working; the migration must not put its storage at risk.

## Considered options

1. **A new `CircleTools.Ui.Web` block** holding the browser implementations of the seams.
2. **A `Storage/` folder inside `CircleTools.Ui.Shared`**, which already references
   `Microsoft.AspNetCore.Components.Web` and therefore has JS interop available.
3. **Duplicate the adapter into `SecretSanta.WebApp`**, leaving ADR-0025's rule untouched.

## Decision outcome

**Chosen: option 1 — a new `CircleTools.Ui.Web` block**, referencing `Ui.Abstractions` and
`Microsoft.JSInterop` and nothing else.

| Block | Contains | May reference |
|---|---|---|
| `CircleTools.Ui.Abstractions` | the seams | nothing |
| `CircleTools.Ui.Web` | `BrowserKeyValueStore` | `Ui.Abstractions` |
| `CircleTools.Localization` | `AppCultures`, `CultureProvider` | `Ui.Abstractions` |
| `CircleTools.Ui.Shared` | Razor components + theme | `Localization`, `Ui.Abstractions` |

This **amends** ADR-0025's "a host supplies the platform" rule to: *a host **chooses** the platform;
where two hosts choose the same one, the implementation is shared.* The composition root still names
it explicitly — both `Program.cs` files register
`AddScoped<IKeyValueStore, BrowserKeyValueStore>()` themselves — so the swap point a MAUI head needs
stays exactly one line, and stays visible.

`Ui.Web` is deliberately **not** a Razor class library. Keeping it a plain `Microsoft.NET.Sdk`
project is what stops it from growing components, and keeps `Ui.Shared` — the block a MAUI head
*does* reference — free of browser adapters. That separation is enforced by
`SharedUiBlockLayeringTests`, not by intention.

**Scope note:** only `BrowserKeyValueStore` moves now. `BrowserFileSaver`,
`BrowserPasskeyCeremony` and `BrowserCredentialsHandler` stay in `Main.WebApp` because they still
have exactly one consumer, and two of them are paired with JavaScript files in that host's
`wwwroot`. Moving them means moving static web assets and re-verifying M1's passkey and export
flows in a browser — real work, for no benefit until a second host wants them. They follow when
one does.

## Amendment (2026-09-01) — the share seam

Making the MAUI Android head ready for a phone added a fifth platform seam, **`IShareService`** (the
native OS share sheet, so an organizer sends each reveal link straight into WhatsApp / Messages /
e-mail). It follows this ADR exactly:

- The seam, `IShareService`, is declared in `Ui.Abstractions` (references nothing).
- The browser implementation, `BrowserShareService`, goes through the Web Share API in
  `wwwroot/js/share.js` and lives **host-local** in `SecretSanta.WebApp/Interop` — the same place and
  reason as `BrowserPasskeyCeremony`: it is paired with a JS file and, for now, has one web consumer.
  It moves into `Ui.Web` (with its JS asset) when a second web host needs it, per the follow-ups above.
- The device implementation, `MauiShareService`, wraps `Microsoft.Maui…DataTransfer.Share` in the App
  head's `Platform/` folder, beside `MauiConnectivity`.
- Both composition roots register it inline (`AddScoped<IShareService, …>()`), keeping the swap point
  one visible line — exactly as for the other seams.

Sharing is a convenience, not a gate: the Share page keeps its always-available Copy action, and the
browser implementation reports `IsSupportedAsync() == false` where the Web Share API is absent (most
desktops), so the button simply does not appear there.

## Consequences

- **Positive:** the browser storage implementation exists once for the whole suite; a third web app
  gets it for free.
- **Positive:** the MAUI head's dependency story is now checkable — a component block it can use, an
  adapter block it must not.
- **Negative / accepted:** a fifth building block, and a solution one entry longer. ADR-0025 already
  accepted this shape of cost.
- **Negative / accepted:** the platform adapters are split across two places until the remaining
  three move — `Ui.Web` for storage, `Main.WebApp/Interop` for the rest. Recorded above rather than
  left to be discovered.
- **Follow-ups:** move `BrowserFileSaver`, `BrowserPasskeyCeremony` and `BrowserCredentialsHandler`
  into `Ui.Web` when a second host needs them, taking their `wwwroot/js` assets with them; add an
  `CircleTools.Ui.Maui` block when the MAUI head implements the same seams natively.
- **New risks:** `Ui.Web` could become a dumping ground for "anything browser-ish". The rule that
  keeps it honest: it implements a seam declared in `Ui.Abstractions`, or it does not belong here.

## Pros and cons of the options

### 1. New `CircleTools.Ui.Web` block (chosen)
- 👍 Honest dependency set; `Ui.Shared` stays MAUI-safe; the boundary is enforceable by a test.
- 👎 One more project.

### 2. `Storage/` folder in `Ui.Shared`
- 👍 No new project; JS interop already available there.
- 👎 `Ui.Shared` stops being "components + theme", and a MAUI head referencing it for the language
  picker inherits `localStorage` adapters it cannot run — the exact coupling ADR-0025 split the
  blocks to avoid.

### 3. Duplicate into `SecretSanta.WebApp`
- 👍 ADR-0025 needs no amendment; no new project.
- 👎 A second copy of the storage adapter, in the change whose entire purpose is removing the second
  copy of the culture logic. The next fix to storage error handling would have to be made twice.

## Links

- Related ADRs: [ADR-0024](0024-ui-styling-and-theming.md),
  [ADR-0025](0025-shared-ui-building-blocks.md) (amended by this one),
  [ADR-0026](0026-refresh-token-persistence.md), [ADR-0029](0029-storage-write-failure-contract.md)
- arc42: [05 Building-block view](../arc42/05-building-block-view.md)
- Guidelines: [architecture](../guidelines/architecture-guidelines.md) §4
- Requirements: [NFR-001](../requirements/non-functional/NFR-001-local-first-offline.md),
  [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
