# Localization Guidelines (DE / EN / FR)

> **Status: outline.**

## 1. Scope
- The **product UI** is localized in **German, French, English**. **Code and documentation are
  English** and are never translated.
- Default culture: **`de-CH`**. Supported: `de-CH`, `fr-CH`, `en`.

## 2. Where localization lives
- Central `Suite.Localization` building block; consumed by every RCL/host via `IStringLocalizer`
  / an `ICultureProvider` abstraction.

## 3. Rules
- No user-facing string hard-coded in components or domain.
- Keys are English, descriptive, and stable; one resource entry per concept.
- Formatting (dates, numbers, currency) via `CultureInfo` — Swiss conventions; never in the domain.
- New feature strings must ship with **DE + EN + FR** (see the feature-spec checklist).

## 4. Culture selection
- Selected culture is a **user preference**, persisted locally and synced across devices.

## 5. Glossary vs. UI terms
- The [glossary](../glossary/index.md) holds canonical **English** domain terms (code names). It
  may list DE/EN/FR **display** terms for reference; those are not code identifiers.

## 6. Open decision
- Resource format (RESX vs JSON/PO) & culture routing — to be fixed by an ADR.
