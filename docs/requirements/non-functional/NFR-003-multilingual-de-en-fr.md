---
id: NFR-003
title: Multilingual DE/EN/FR with de-CH default
type: non-functional
status: Implemented
priority: Must
source: UC-SS-001, UC-SS-006
verification: test
related: [UC-SS-001, UC-SS-006, FR-001, FR-006, ADR-0011]
---

# NFR-003 — Multilingual DE/EN/FR with de-CH default

## Description

The Secret-Santa app — including the standalone reveal page — must present its user-facing text in
**German, English, and French**, defaulting to **`de-CH`**. Users must be able to switch language,
and locale-sensitive values ([Budget](../../glossary/budget.md) currency/amount and
[Event details](../../glossary/event-details.md) dates) must be formatted for the active culture.
User-authored content (member names, [Wishlist](../../glossary/wishlist.md) items, notes) is not
translated.

## Rationale

The product is Swiss-first and serves DE/EN/FR audiences. Because the reveal page may be the first
touch-point for a member who never opened the app, it must localize on its own.

## Acceptance criteria

```gherkin
Given no explicit language preference
When the app or the reveal page starts
Then all labels render in de-CH by default

Given the user switches the language to EN or FR
When any MVP screen or the reveal page is shown
Then every label on it renders in the selected language with no missing or placeholder strings

Given the active culture is de-CH
When a budget amount and an event date are displayed
Then they are formatted according to that culture

Given a reveal link is opened
When the reveal page renders
Then its labels follow the selected language while member names and wishlist text remain as authored
```

## Quality attribute *(NFR only)*

- **Attribute:** internationalization / localization (i18n).
- **Metric & target:** **100%** of user-facing strings across all MVP screens and the reveal page
  are resourced in DE, EN, and FR (0 hard-coded or untranslated strings); default culture is
  `de-CH`.
- **Measurement approach:** a resource-completeness check asserts every UI string key has DE/EN/FR
  entries; UI tests verify default culture and language switching, including on the reveal page.

## Dependencies & related

- Derived from: [UC-SS-001](../../use-cases/secret-santa/UC-SS-001-create-group.md), [UC-SS-006](../../use-cases/secret-santa/UC-SS-006-open-reveal-link.md)
- Related: [FR-001](../functional/FR-001-create-secret-santa-group.md), [FR-006](../functional/FR-006-reveal-page-decode.md)
- Decision: [ADR-0011 — Localization format & culture (RESX, `de-CH`)](../../adr/0011-localization-format-and-culture.md)
- Glossary: [Budget](../../glossary/budget.md), [Event details](../../glossary/event-details.md), [Wishlist](../../glossary/wishlist.md)
