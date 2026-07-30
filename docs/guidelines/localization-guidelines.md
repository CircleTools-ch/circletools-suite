# Localization Guidelines (DE / EN / FR)

> **Status: outline.**

## 1. Scope
- The **product UI** is localized in **German, French, English**. **Code and documentation are
  English** and are never translated.
- Default culture: **`de-CH`**. Supported: `de-CH`, `fr-CH`, `en`.

## 2. Where localization lives
- Central `AppSuite.Localization` building block; consumed by every RCL/host via `IStringLocalizer`
  / an `ICultureProvider` abstraction.

## 3. Rules
- No user-facing string hard-coded in components or domain.
- Keys are English, descriptive, and stable; one resource entry per concept.
- Formatting (dates, numbers, currency) via `CultureInfo` — Swiss conventions; never in the domain.
- New feature strings must ship with **DE + EN + FR** (see the feature-spec checklist).
- **A string that interpolates a count needs a second entry for one.** There is no pluralization
  library here, and `string.Format` cannot inflect: DE, EN and FR all change the noun *and* the verb
  between one and many. Give the singular its own key with the `One` suffix — it carries **no**
  placeholder, so the translator writes the number the way that language wants it — and pick it at
  the call site. Two M2 strings shipped without this and read *"1 Personen"*, *"1 people"*,
  *"1 personnes"*: the circle list's size fragment, and FR-022's delete confirmation — both in the
  state every circle is in the moment it is created
  ([FEAT-003](../features/FEAT-003-m2-circles.md)). Test the count at **one**, not only at three —
  the component tests covering both rendered three people and saw nothing.

## 4. Culture selection
- Selected culture is a **user preference**, persisted locally and synced across devices.
- Persist it through `IKeyValueStore` under a **per-app key** (`CultureOptions.StorageKey`), so two
  apps in the suite do not overwrite each other's choice.
- **Changing language must not reload the page**
  ([UC-MAIN-005](../use-cases/main/UC-MAIN-005-manage-profile.md)). A `forceLoad` navigation is a
  regression, not a shortcut. In Blazor WebAssembly this needs three things, and missing any one of
  them fails quietly rather than loudly:
  - Start the runtime with `loadAllSatelliteResources: true`. Otherwise only the startup culture's
    resource assemblies are downloaded and every other language falls back to the neutral strings —
    the picker "works" and shows English.
  - Set `CultureInfo.CurrentCulture`/`CurrentUICulture`, not just the `DefaultThread*` properties.
    The defaults are consulted only by threads that have not already resolved their own.
  - Rebuild the tree, don't just re-render it: wrap the router in `CultureBoundary` and put an
    `@key` on the `Router` bound to the culture name. A `StateHasChanged` above the router does not
    reliably reach the layout.
- **A keyed rebuild discards page state.** Any page that both changes the language and needs to show
  something afterwards (a "saved" confirmation) must keep it somewhere that survives — the query
  string, or a scoped service.
- When a signed-in user's account carries a language, it wins over whatever this browser last
  remembered.

## 5. Tone of voice

Decided in [ADR-0032](../adr/0032-ui-tone-of-voice-and-gender-neutral-terms.md). The register is
**per language**, because German and French do not draw the formal/informal line in the same place.

| Language | Address the reader as | Example |
|----------|-----------------------|---------|
| German (`de-CH`) | **informal `du`** | *"Gib zur Bestätigung dein Passwort ein."* |
| French (`fr-CH`) | **formal `vous`** | *"Saisissez votre mot de passe pour confirmer."* |
| English (`en`) | no distinction — plain second person | *"Enter your password to confirm."* |

This covers **everything the user reads**, transactional mail included — the mail is the same voice
as the screen that triggered it.

**Never imply the reader's gender.** The UI knows nothing about it. German agent nouns take the
colon form (`Besitzer:in`), and a sentence that would need a gendered article is rewritten around it
(*"die einzige Person, die…"*, not *"die Einzige, die…"*). Terms already epicene in German usage —
`Admin` — stay as they are. French `propriétaire` is epicene and needs nothing.

## 6. Glossary vs. UI terms
- The [glossary](../glossary/index.md) holds canonical **English** domain terms (code names). It
  may list DE/EN/FR **display** terms for reference; those are not code identifiers.

## 7. Decided
- Resource format and culture routing: **RESX + `IStringLocalizer`**, cultures `de-CH` (default),
  `en` and `fr-CH` — [ADR-0011](../adr/0011-localization-format-and-culture.md). The shared
  implementation lives in `AppSuite.Localization`
  ([ADR-0025](../adr/0025-shared-ui-building-blocks.md)).
- Key parity across all three cultures is enforced per app by a `LocalizationParityTests` fixture: a
  key present in the neutral resources and missing or empty in any culture fails the build's test
  step, rather than falling back silently on screen.
