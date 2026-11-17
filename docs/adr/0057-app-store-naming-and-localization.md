---
status: Accepted
date: 2026-09-02
deciders: [Adrian]
tags: [distribution, mobile, branding, localization, aso, naming]
---

# ADR-0057 — App store naming & listing localization

## Context and problem statement

Each app in the [federated suite](0046-suite-packaging-and-app-composition.md) ships as **one store
listing per app** on Google Play (first) and later the Apple App Store, and every listing must be
provided in **DE / EN / FR** ([NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
[NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)). That raised a concrete
question for the first app (Secret-Santa), and by extension for every future app:

**Do we give an app one name across all three languages, or a different name per language — and does
a per-language name force us to own a separate domain per name?**

The question came up while reviewing the [competitor
scan](../brainstorming/2026-09-02-secret-santa-competitor-research.md), which surfaced that the
concept is called different things per market — English *"Secret Santa"*, German *"Wichteln"* (and
the *Schrottwichteln* variant), French *"Père Noël secret"* — and that the market leader **DrawNames**
keeps a single global brand name while running localized listings and separate marketing web
properties (`drawnames.de`, `.fr`, `.be`).

The premise behind the question — that a localized name would require a localized domain — is false,
and untangling it is half the decision. Three identifiers are commonly conflated:

| Identifier | What it is | Localizable? | Tied to the domain? |
|---|---|---|---|
| **Web origin / bundle id / passkey RP** | `secretsanta.circletools.ch`, `ch.circletools.secretsanta`, apex `circletools.ch` | **No — one, permanent** ([ADR-0046](0046-suite-packaging-and-app-composition.md), [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)) | This *is* the domain |
| **Store listing** — title, subtitle, description, keywords, screenshots | The Play / App Store page | **Yes — per locale, free** | **No** |
| **On-device launcher label** — `ApplicationTitle` / `CFBundleDisplayName` | Text under the icon on the phone | Technically per-locale via resource qualifiers | **No** |

The store listing name is a per-locale metadata field on both stores; it is **not** derived from any
domain and needs no additional registration. So the real, open choice is narrow: *is the visible app
name identical across the three locales, or localized — and where does per-market discoverability
(ASO) live?*

## Decision drivers

- **Brand consistency.** `naming.md` commits to **English brand names that read in all three product
  languages**; a single recognizable identity is a more valuable asset than a locally literal one.
- **Discoverability (ASO).** German users search *"Wichteln"*, French users *"Père Noël secret"* —
  being unfindable under the local term is a real cost, and localization quality is a *named*
  competitor failing we intend to beat.
- **No new permanent identifiers.** Domains, bundle ids and RP-IDs are permanent or non-migratable
  ([NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)); a naming choice must
  not spawn any of them.
- **Cost & upkeep.** Every extra domain is a renewal, another CSP origin, another deep-link/App-Link
  surface ([ADR-0053](0053-backend-free-first-mobile-release.md)) — bought for nothing if it isn't the
  app's origin.
- **Per-app repeatability.** The rule must be one the next app inherits, not re-litigates.

## Considered options

1. **One universal visible name; discoverability localized in the listing metadata only.**
2. **Fully localized visible app name per locale** (e.g. launcher label *Wichteln* in DE).
3. **Localized names backed by per-language / per-name domains** (the premise in the question).

## Decision outcome

**Chosen: option 1 — one universal, stable visible name per app; per-market discoverability lives in
the localized listing metadata, not in the name; and no per-language or per-name domains are created.**

Concretely, and as the standing rule for every suite app:

1. **One universal visible name and launcher label**, kept in English, identical in all three store
   locales and on-device. For the first app this is **"Secret Santa"** — the internationally
   recognized term, consistent with the brand's English-reads-everywhere principle. The launcher label
   (`ApplicationTitle`) stays single; it is not forked per locale.

2. **Localize the listing for discoverability, not identity.** This is required anyway and is where DE
   and FR searchers are won:
   - **Google Play** — the localized **title** (30 chars) and description carry the search weight; use
     a composite where it helps, e.g. `Secret Santa – Wichteln` (DE), `Secret Santa – Père Noël
     secret` (FR). Title stays within one brand.
   - **Apple App Store** — put the local terms (`Wichteln`, `Schrottwichteln`, `Père Noël secret`,
     `tirage au sort`, …) in the **localized keyword field** (100 chars, invisible to users); the
     visible **Name** stays "Secret Santa", the **Subtitle** localizes freely. *(Amended 2026-11-17: the
     Name now also carries a localized descriptor after the brand — see the amendment below.)*

3. **No per-language or per-name domains.** The single per-app origin `<app>.circletools.ch` serves
   every locale ([ADR-0046](0046-suite-packaging-and-app-composition.md)). Vanity marketing redirects
   (e.g. a hypothetical `wichteln.ch`) are explicitly **out of scope here** — a later, optional
   marketing decision, decoupled from the app's identity, listing and deep links.

The per-app *brand* name itself is still chosen at each app's idea/branding step
([naming.md](../process/naming.md)); this ADR fixes **how** that one name relates to the three locales
and the domain, not **which** name a given app gets.

## Consequences

- **Positive:** one clean brand identity per app; zero new permanent or paid identifiers; the DE/FR
  discoverability gap is closed through metadata we must fill regardless; the rule is uniform across
  the suite.
- **Negative / accepted:** a purely English visible name slightly underweights local launcher-label
  recognition (a German speaker calls it *Wichteln*). Recovered via the composite localized title and
  the keyword fields, and judged a good trade for a single brand.
- **Follow-ups:**
  - Fill the DE/EN/FR listing title/subtitle/keywords for Secret-Santa as part of the
    [mobile-apps submission checklist](../deployment/mobile-apps.md) (feed from the competitor scan's
    local search terms).
  - Record this rule in [naming.md](../process/naming.md) so each new app inherits it at its branding
    step.
- **New risks:** none. No new identifier is created and nothing here is permanent — listing metadata
  is editable per release.

## Amendment (2026-11-17) — the App Store name gets a localized descriptor, like the Play title

**What changed.** Rule 2 kept the visible App Store **Name** identical in every locale and left the local
terms to the subtitle and keywords. In practice the name became **"Secret Santa - CircleTools"**: plain
"Secret Santa" is taken, and App Store names must be unique, so the record needed a suffix. A search check
on 2026-11-17 ([backlog](../roadmap/backlog.md), *Store search*, ASO-2) found the app at rank **41–56** in
the Swiss App Store for its core terms ("Secret Santa", "Wichtel", "Wichteln"), against rank **4–10** on
Google Play, whose localized title already follows rule 2's composite pattern. The name is the field Apple
weighs most, and 11 of its 30 characters went to "CircleTools", which nobody searches for.

**Amended rule 2, App Store part:** the App Store **Name** follows the same composite pattern as the Play
title — **the brand first, identical in every locale, then a localized descriptor that carries the local
search term**. Apple localizes the name per language (App Store Connect → *App Information*), and every
localized name must be unique on the store. For Secret Santa (from the next iOS version):

| Locale | Name (≤30) | Why this descriptor |
|---|---|---|
| DE | `Secret Santa – Wichtel ziehen` | bare "Wichtel" in the strongest field; "Wichteln" stays in the subtitle |
| EN | `Secret Santa – Draw Names` | "draw names" is what the app does; "gift exchange" moves to the subtitle |
| FR | `Secret Santa – Tirage au sort` | "tirage au sort"; "Père Noël secret" stays in the subtitle |

The obvious first choices ("Secret Santa – Wichtel App", "Secret Santa: Gift Exchange", "Secret Santa: Père
Noël secret") already belong to other apps. The names above had no exact match in the App Store search API;
App Store Connect is the final check and refuses a taken name when it is saved. Fallbacks, in order:
`Secret Santa: Wichteln & Losen` / `Wichteln: Secret Santa Losung` (DE), `Secret Santa Draw & Wishlist`
(EN), `Secret Santa: tirage cadeaux` (FR).

**Unchanged:** the brand stays "Secret Santa", first in every locale, and the **on-device name**
(`ApplicationTitle` / `CFBundleDisplayName`) stays the single "Secret Santa"; the store name still starts with it, so the
two stay recognizably the same app. Rule 3 (no per-language domains) is
untouched. The App Store's developer line shows the account holder; showing "CircleTools" there needs an
organization account (backlog ASO-3), independent of this ADR.

**Consequence for the suite rule.** Every app's store name, on both stores, is *brand + localized
descriptor*; only the brand part must be identical across locales. The keyword field must not repeat words
from the name or subtitle (Apple indexes them once), so the keywords are rebalanced with each name change.

## Pros and cons of the options

### 1. One universal name; discoverability in localized metadata (chosen)
- 👍 Single, recognizable brand identity; matches the English-reads-everywhere naming principle.
- 👍 Full DE/FR ASO via listing title/keywords we must author anyway; no new identifiers, no cost.
- 👍 Mirrors the market leader (DrawNames): one name, localized listings.
- 👎 Visible name isn't the literal local term (mitigated by composite titles / keywords).

### 2. Fully localized visible name per locale
- 👍 The launcher label reads natively in each market.
- 👎 Fractures one brand into three; harder word-of-mouth and cross-locale support.
- 👎 More build/asset surface (per-locale launcher resources, per-locale icon copy) for a gain the
  keyword field already delivers.

### 3. Localized names on per-language / per-name domains
- 👍 Nothing — rests on a false premise: store names are not derived from domains.
- 👎 Every domain is a renewal plus a new CSP origin and App-Link/deep-link surface
  ([ADR-0053](0053-backend-free-first-mobile-release.md)), bought for no functional gain.
- 👎 Multiplies permanent/again-permanent identifiers against
  [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md)'s minimisation.

## Links

- Related ADRs: [ADR-0046](0046-suite-packaging-and-app-composition.md) (federated per-app origins &
  bundle ids), [ADR-0053](0053-backend-free-first-mobile-release.md) (App Links / `RevealBaseUrl`),
  [ADR-0030](0030-mobile-store-publisher-identity.md) (store accounts)
- Requirements: [NFR-007](../requirements/non-functional/NFR-007-store-distributability.md) (store
  distributability), [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)
  (DE/EN/FR)
- Process: [naming](../process/naming.md) (brand & domain; per-app branding step)
- Input: [Secret-Santa competitor research](../brainstorming/2026-09-02-secret-santa-competitor-research.md)
- Runbook: [Deployment — mobile apps](../deployment/mobile-apps.md) (submission checklist)
