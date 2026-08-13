---
status: Accepted
date: 2026-07-28
deciders: [Adrian]
tags: [architecture, ui, frontend, design]
---

# ADR-0024 — UI styling & theming

## Context and problem statement

Until v2 the suite had exactly one screen family — Secret-Santa — styled with whatever the Blazor
WASM template shipped: Bootstrap 5, the template's `app.css`, and per-component CSS isolation. That
was never a decision; it was a default nobody had needed to question.

The Main app's account screens change that. Sign-up, sign-in, verification, reset, profile, security
and data now have to look like **one product** alongside Secret-Santa, and a second app is coming.
The working name is a placeholder and the brand is deliberately deferred
([naming](../process/naming.md)), so whatever is chosen must not bake a visual identity in.

**How is the suite styled, and where does an app's own styling stop?**

## Decision drivers

- Solo hobby project. Time spent on a design system is time not spent on features.
- The brand is **not decided**. Anything expensive to change later is the wrong choice now.
- Three languages (NFR-003): German and French text runs materially longer than English, so layouts
  have to tolerate it. Fixed-width components are a recurring source of clipped labels.
- Accessibility is a stated criterion in [TP-002](../features/TP-002-m1-identity.md), not a
  later polish pass.
- Two apps today, more later; screens must be recognisably the same product without every app
  re-deciding spacing and colour.
- ADR-0005's standalone PWA means CSS is a static asset — no server-side theming.

## Considered options

1. **Bootstrap 5 + a thin suite theme layer + per-component CSS isolation.**
2. **A hand-written design system** (custom CSS/utility layer, no framework).
3. **A component library** (MudBlazor, FluentUI-Blazor, Radzen …).

## Decision outcome

**Chosen: option 1 — Bootstrap 5, a thin suite theme, and CSS isolation for anything
component-local.**

Concretely, three layers, in this order:

| Layer | What lives there | Where |
|---|---|---|
| Bootstrap 5 | Grid, forms, buttons, alerts, utilities | `wwwroot/lib/bootstrap` (per host) |
| Suite theme | Brand custom properties, the auth-card shell, list rows, danger areas, form rhythm | `CircleTools.Ui.Shared/wwwroot/css/circletools.css` |
| Component styles | Anything belonging to exactly one component | `<Component>.razor.css` |

The theme is deliberately small — a handful of custom properties and about six layout classes. It
carries **no brand assets**: no logo, no typeface, one neutral brand colour behind a CSS custom
property (`--circletools-brand`). Rebranding is then a variable change and an asset drop, not a rewrite.

Rules that follow from the layering:

- A style used by more than one app goes in the theme; a style used by one component goes in that
  component's `.razor.css`. Nothing app-specific goes in the theme.
- **No inline `style` attributes, and no inline `<script>`.** The CSP
  ([ADR-0021](0021-web-auth-token-model.md)) has no `'unsafe-inline'`, and an inline attribute or
  block is the easiest way to discover that at runtime instead of at review time. This is also why
  asset fingerprinting is off in the Main host — see
  [FEAT-002](../features/FEAT-002-m1-identity.md), "Content-Security-Policy".
- Colour never carries meaning alone — a destructive area is marked by its heading as well as its
  border, which is both an accessibility requirement and a courtesy to anyone in bright sunlight.

Why not a component library, which is the tempting option: it would give more polish per hour, and it
would put a third party's visual identity on a product whose own identity is undecided. Migrating off
one later means touching every screen, because the markup *is* the library. Bootstrap's markup is
plain HTML with classes, so the exit cost is a stylesheet, not a rewrite. For a project that has
explicitly deferred branding, keeping that exit cheap is worth more than the polish.

## Consequences

- **Positive:** no new dependency; the existing Secret-Santa screens keep working untouched; screens
  are plain HTML, so they stay readable and testable with bUnit; rebranding stays cheap.
- **Positive:** Bootstrap's form and alert components already carry the ARIA semantics TP-002 asks
  for, so accessibility starts from a reasonable baseline rather than from nothing.
- **Negative / accepted:** the result looks like Bootstrap. That is the honest trade for a placeholder
  brand, and it is revisited when the brand is.
- **Negative / accepted:** each host carries its own copy of Bootstrap under `wwwroot/lib`. Two
  copies is not worth a shared-asset mechanism yet; at four apps it will be.
- **Follow-ups:** revisit when the brand is chosen ([naming](../process/naming.md)) — at that point
  the theme layer is where the brand lands, and this ADR gets an amendment rather than a successor.
- **New risks:** the theme can accrete app-specific rules if the "shared vs component" line is not
  held. The check is cheap: anything in `circletools.css` that names a single app's concept is misplaced.

## Pros and cons of the options

### 1. Bootstrap + thin theme + CSS isolation (chosen)
- 👍 Already in the repo; plain HTML markup; cheap to rebrand; nothing new to learn or maintain.
- 👎 Generic appearance; the theme layer needs discipline to stay thin.

### 2. Hand-written design system
- 👍 Exactly the product's own identity, no framework weight.
- 👎 Months of work for a solo hobby project, to arrive at a worse version of what Bootstrap already
  does — including the accessibility details that are easy to miss and expensive to retrofit.

### 3. Component library (MudBlazor / FluentUI / Radzen)
- 👍 Polished components quickly; consistent by construction.
- 👎 Adopts a third party's visual identity while ours is undecided; the markup becomes the library,
  so leaving means rewriting every screen; larger payload for a PWA that wants to stay small.

## Links

- Related ADRs: [ADR-0005](0005-web-hosting-render-model.md),
  [ADR-0021](0021-web-auth-token-model.md) (the CSP that rules out inline styles),
  [ADR-0025](0025-shared-ui-building-blocks.md) (where the theme physically lives)
- arc42: [05 Building-block view](../arc42/05-building-block-view.md)
- Requirements: [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
- Feature: [FEAT-002](../features/FEAT-002-m1-identity.md),
  [TP-002](../features/TP-002-m1-identity.md)
- Process: [naming](../process/naming.md)
