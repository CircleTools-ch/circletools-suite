---
status: Accepted
date: 2026-09-02
deciders: [adrian-moll]
tags: [localization, i18n, ux]
---

# ADR-0056 — Decouple UI language from formatting region

## Context and problem statement

[ADR-0011](0011-localization-format-and-culture.md) routes everything through a single **selected
culture**: the chosen language drives both the UI strings *and* the date/number/currency formatting.
`CultureProvider.Apply` sets `CurrentCulture` and `CurrentUICulture` to the same value, and
`AppCultures` supports `de-CH`, `en`, `fr-CH`.

That conflates two different things. Language (which words) is a *content* choice; formatting
(dd.MM.yyyy vs MM/dd/yyyy, `1'234.50` vs `1,234.50`) is a *regional* one. The coupling surfaced as a
concrete defect: the English option maps to bare `en`, which .NET formats as **en-US** — so a
Swiss user reading the app in English saw **American** dates (`12/17/2026`), while the German and
French options (mapped to `de-CH` / `fr-CH`) showed Swiss dates. For a Swiss-first product that is the
wrong default, and it is visible on the event Details date field.

**How should the app decide date/number/currency formatting, given the UI language is a separate,
synced preference?**

## Decision drivers

- Swiss-first product; a Swiss user should get Swiss formatting regardless of the UI language they read.
- Language is a synced account preference (ADR-0011/ADR-0035); regional format is a property of the
  device the person is holding, not of their account.
- Keep the "switch language without a page reload" mechanism (ADR-0011) intact.
- Minimise domain impact: formatting already lives only in the UI, never in the domain.

## Considered options

1. **Map the English option to a Swiss-formatting culture (`en-CH`).** Keeps one culture per language;
   makes all three languages format the Swiss way.
2. **Decouple language from region.** UI strings follow the chosen language (`CurrentUICulture`);
   date/number/currency formatting follows the **device/OS region** (`CurrentCulture`), captured from
   the platform at startup and independent of the language choice.
3. **Leave as-is.** English stays American; German/French stay Swiss.

## Decision outcome

**Chosen: Option 2 (decouple).** Language and locale are genuinely different concerns, and tying
format to language only ever produces the right answer when the mapped region happens to match the
user's. Option 1 fixes the immediate English case but still forces every English reader worldwide onto
Swiss formatting, and every German reader onto Swiss formatting even on a non-Swiss device — the same
category of wrong answer, just less visible. Option 2 gives each concern its own source of truth:

- **UI strings** (`IStringLocalizer`, i.e. `CurrentUICulture` / `DefaultThreadCurrentUICulture`) follow
  the **chosen language** — the synced preference, still one of `de-CH`, `en`, `fr-CH`.
- **Formatting** (`CurrentCulture` / `DefaultThreadCurrentCulture`) follows the **device region**,
  captured once at startup from the platform's initial culture and never overwritten by a language
  switch.

`CultureProvider.CurrentCulture` continues to report the **language** (its only consumers key the
render tree and compare against the synced account language on it); components that format via the
static `CultureInfo.CurrentCulture` now get the region automatically. No call sites change.

## Consequences

- **Positive:** a Swiss user reading the app in English now sees Swiss dates/numbers; formatting is
  correct on any device without a per-language culture table; the domain is untouched; the
  language-switch-without-reload mechanism is unchanged (only the UI culture flips on a switch).
- **Negative / accepted:** on the **web** the "device region" is the browser's locale
  (`navigator.language`), which reflects the browser's *language* setting, not the OS regional-format
  panel — the closest signal a browser exposes. A user whose browser is "English (US)" but whose
  Windows region is Swiss will still see US formatting until they adjust the browser locale. On the
  device head the OS region is authoritative, so this gap is web-only.
- **Follow-ups:** none required. A future explicit "region/format" setting (independent of both
  language and device) is possible if the browser-locale proxy proves insufficient, but is out of scope.
- **New risks:** the captured region may be a culture whose data is thin in the trimmed WASM ICU; the
  runtime falls back to available data, and an empty/invariant region falls back to the `de-CH` default
  so formatting is never culture-less.

## Pros and cons of the options

### Option 1 — English → `en-CH`
- 👍 One-line-ish change; keeps the simple one-culture-per-language model; fixes the visible English case.
- 👎 Still couples format to language: forces Swiss formatting on all English/German/French readers
  regardless of where they are — the same class of wrong answer.

### Option 2 — Decouple language from region
- 👍 Correct separation of concerns; right formatting on any device; no domain or call-site changes.
- 👎 On the web the region is the browser locale, not the OS format panel (documented limitation).

### Option 3 — Leave as-is
- 👎 English readers of a Swiss-first app get American dates; inconsistent with German/French.

## Links

- Related ADRs: [ADR-0011](0011-localization-format-and-culture.md) (refines its formatting-culture
  routing; the RESX/`IStringLocalizer` decision is unchanged), [ADR-0035](0035-profile-sync-seam.md)
  (language remains the synced preference).
- arc42 chapters: 08 Cross-cutting concepts (localization).
- Guidelines: [localization-guidelines](../guidelines/localization-guidelines.md).
