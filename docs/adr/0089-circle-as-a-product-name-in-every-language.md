---
status: Accepted
date: 2026-10-31
deciders: [Adrian]
tags: [localization, naming, product, circles, ui]
---

# ADR-0089 — "Circle" as a product name in every language

## Context and problem statement

The suite's standing set of people — a family, a household, a group of friends — is a
[Circle](../glossary/circle.md) in code. The UI translated it: **"Kreis"** in German, **"cercle"** in
French, lowercase **"circle"** in English. During the legal-page review (backlog LR-7) the owner found
"Kreis" on its own reads oddly in German ("Erstelle einen Kreis", "Du bist noch in keinem Kreis"), and
the brand is **CircleTools** — the feature already carries the brand's word.

**Should the UI translate "circle", or use "Circle" as a product name in every language?**

## Decision drivers

- **Reads naturally** — "Kreis" alone is not how Swiss German speakers name their family or friends.
- **Brand coherence** — CircleTools (ADR naming, 2026-08-13) is built around the word.
- **One name across languages** — the same choice ADR-0057 made for "Secret Santa" in the app bar.
- **Swiss audience** — English product words are common and understood in DE- and FR-speaking Switzerland.
- **Low cost** — UI strings and two mail lines only; no code names, URLs or data change.

## Considered options

1. **Keep translating** — "Kreis" / "cercle" / "circle".
2. **"Circle" as a product name in every language** — capitalised, with the local grammar around it.
3. **A different local word** — e.g. "Runde" or "Gruppe" in German.

## Decision outcome

**Chosen: Option 2.** The UI calls the feature **Circle** (plural **Circles**) in German, French and
English, always capitalised, because it is a name. The grammar around it stays local:

| Language | Gender / article | Examples |
|----------|------------------|----------|
| DE | masculine, like "Kreis": *der Circle, des Circles, die Circles* | "Deine Circles", "Circle erstellen", "Du bist noch in keinem Circle", "Circle-Mitgliedschaften" |
| FR | masculine, like "cercle": *le Circle, les Circles* | « Vos Circles », « Créer un Circle », « Quitter ce Circle » |
| EN | — | "Your Circles", "Create a Circle", "Leave this Circle" |

The ordinary German word **"Freundeskreis"** is not the feature and is not renamed. A circle's own
**label** ("Familie", "Famille", "Family" — chosen by its members) is unchanged; it stays display data.

Option 3 was rejected: "Gruppe" is already the Secret-Santa [Group](../glossary/group.md), and any
other German word still leaves French and English apart from the brand.

## Consequences

- **Positive:** reads like a product feature; matches the CircleTools brand; one word in all three
  languages, so support and store texts need no translation of it.
- **Negative:** an English word in German and French UI text; German and French plurals with "-s".
- **Follow-ups:** all `.resx` strings in Main, Secret Santa, account and legal pages; the circle
  invitation mail (`MailTemplateRenderer`); the glossary's product display terms. Ships in the release
  after 1.0.1.
- **New risks:** none beyond taste; reversible by changing strings only.

## Pros and cons of the options

### Keep translating
- 👍 Fully local wording.
- 👎 "Kreis" reads oddly on its own; three different words for one feature.

### "Circle" as a product name
- 👍 Natural as a name; ties the feature to the brand; one word everywhere.
- 👎 Anglicism in DE/FR.

### A different local word
- 👍 Local.
- 👎 No good candidate: "Gruppe" collides with the Secret-Santa group; the others are no better than "Kreis".

## Links

- Related ADRs: [ADR-0032](0032-ui-tone-of-voice-and-gender-neutral-terms.md) (tone of voice),
  [ADR-0057](0057-app-store-naming-and-localization.md) (one app name in every language),
  [ADR-0018](0018-circle-and-sharing-model.md) (circle model)
- Glossary: [Circle](../glossary/circle.md)
- Backlog: LR-7 (legal-page text review, 2026-10-31)
