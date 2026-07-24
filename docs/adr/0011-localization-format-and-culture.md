---
status: Accepted
date: 2026-07-24
deciders: [Adrian]
tags: [architecture, localization, i18n]
---

# ADR-0011 — Localization format & culture routing

## Context and problem statement

The product is multilingual (**German, French, English**), Swiss-first, while code and docs are English.
We need a resource format and a culture-selection approach that works across the WASM web client and the
MAUI native apps.

## Decision drivers

- Standard .NET localization tooling; designer/translator friendly.
- Swiss conventions; a sensible default culture.
- Culture must roam with the user across devices.

## Considered options

1. **RESX + `IStringLocalizer`** with a central `AppSuite.Localization`
2. JSON/PO resource files + a custom loader
3. A third-party localization service/SDK

## Decision outcome

**Chosen: RESX resource files with `IStringLocalizer`**, centralized in `AppSuite.Localization` and consumed
by every RCL/host. Default culture **`de-CH`**; supported cultures **`de-CH`, `fr-CH`, `en`**. The selected
culture is a **user preference** persisted locally and synced. Date/number/currency formatting uses
`CultureInfo` (Swiss conventions); **no locale/format logic lives in the domain**.

## Consequences

- **Positive:** First-party tooling, strongly-typed access, broad translator support; consistent across
  Web and native; clean separation from the domain.
- **Negative:** RESX editing is XML-based (mitigated by IDE editors); satellite assemblies add a little size.
- **Follow-ups:** Establish a key-naming convention; wire an `ICultureProvider` abstraction in
  `AppSuite.Ui.Abstractions`; ensure new feature strings ship in all three languages (feature-spec checklist).

## Pros and cons of the options

### 1. RESX + IStringLocalizer
- 👍 Standard, typed, tooling-rich, cross-platform.
- 👎 XML format; satellite assemblies.

### 2. JSON/PO + custom loader
- 👍 Easy diffs; translator-familiar (PO).
- 👎 Custom infrastructure; less first-party support.

### 3. Third-party service
- 👍 Managed translation workflow.
- 👎 External dependency/cost; overkill at this scale.

## Links

- arc42: [08 Cross-cutting concepts](../arc42/08-crosscutting-concepts.md)
- Guideline: [localization-guidelines.md](../guidelines/localization-guidelines.md)
