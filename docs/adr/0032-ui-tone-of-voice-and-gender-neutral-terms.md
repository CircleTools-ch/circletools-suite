---
status: Accepted
date: 2026-07-30
deciders: [Adrian]
tags: [ui, localization, content, product]
---

# ADR-0032 — UI tone of voice and gender-neutral role terms

## Context and problem statement

The product is translated into German, English and French ([NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
German and French both distinguish a formal and an informal way of addressing the reader; English
does not. Nobody ever decided which one this product uses, so each batch of strings picked one.

The gap became visible on a single screen. M1's account strings were written formally — *"Geben Sie
zur Bestätigung Ihr Passwort ein"* — and all 41 of its German strings are consistent about it. M2's
circle strings were written informally — *"Übergib oder lösche die Kreise, die dir gehören, bevor du
dein Konto löschst"* — and all of its German strings are consistent about that. The sole-owner
refusal ([FR-015](../requirements/functional/FR-015-delete-account.md)) renders the second directly
above the first, in the same red panel, and the register flips mid-panel.

A second, separate defect surfaced in the same review: M2's German role terms are **feminine-only**
agent nouns applied to every user regardless of gender — the owner badge reads `Besitzerin`, and one
refusal reads *"Du bist die Einzige…"*. This is not a style preference; it tells most users something
untrue about themselves.

**How does the product address its users, and how does it name a role held by a person of unknown
gender?**

## Decision drivers

- **What the product is.** A suite for families and groups of friends organising things together.
  The reader is doing something sociable, not filing a tax return.
- **Consistency is not optional.** A register that changes between screens — or within one panel —
  reads as carelessness, and it is the kind of defect a green test run never catches.
- **T-V norms are per-language, not per-product.** German and French do not draw the formal/informal
  line in the same place, and pretending they do produces text that is wrong in one of them.
- **Never misgender a user.** The UI knows nothing about any user's gender and must not imply it.
- **Solo project.** The convention has to be mechanical enough to apply without deliberation on every
  new string.

## Considered options

1. **Formal everywhere** — convert M2's circle strings to `Sie`, keeping M1's convention.
2. **Informal in German, per-language norm elsewhere** — convert M1's account strings to `du`; French
   keeps `vous`.
3. **Informal everywhere, including French `tu`** — one register, applied literally across languages.
4. **Leave it, record as known issue.**

## Decision outcome

**Chosen: Option 2 — informal `du` in German, `vous` in French, and gender-neutral role terms in
German written with the colon form (`Besitzer:in`).**

The product is one families use to organise Christmas presents. `du` is the register that fits it,
and it is the one M2's strings already reached for unprompted. Formality was never chosen for M1; it
was the default of writing account screens, which are the one part of any product that sounds like a
bank. Aligning to `Sie` would have made the whole suite sound like the least characteristic screen in
it.

**French stays `vous`.** This is the point where a single "product register" stops being meaningful.
Swiss French consumer products overwhelmingly keep `vous` even where their German counterparts use
`du`; `tu` from an unfamiliar service reads as presumptuous rather than warm. The product voice is
"warm and direct", and each language realises that in its own idiom. French is already consistently
`vous` and needs no change.

**English is unaffected** — it has no T-V distinction — and stays as written.

For the German role terms, the **colon form** (`Besitzer:in`) is adopted. It is the shortest neutral
form that still reads as German, it fits a badge, and it does not require rewriting every sentence
that names the role around the absence of a noun. `Admin` stays as it is: it is already epicene in
German usage.

## Consequences

- **Positive:** German reads as one voice across the suite. The sole-owner refusal no longer changes
  register mid-panel. No user is told they are a `Besitzerin` on the strength of owning a circle.
- **Positive:** The rule for new strings is mechanical — German `du`, French `vous`, colon form for
  German agent nouns — so it needs no judgement per string.
- **Negative:** 41 of M1's German strings were rewritten. That is churn in a file whose diff is
  already large, and it touches strings M1 shipped and verified.
- **Negative:** The colon form is a typographic convention with detractors, and screen readers
  handle it inconsistently. Accepted because the alternatives are worse: the slash form has the same
  problem, and rewriting every sentence to dodge the noun distorts the German.
- **Follow-ups:** [localization guidelines](../guidelines/localization-guidelines.md) records the
  convention so it is applied without rediscovering this ADR. Any future language with a T-V
  distinction picks its own side of the line and says so here.
- **New risks:** The register is now part of the product's identity, so a later branding decision
  ([naming](../process/naming.md)) could reopen it. Cheap to revisit: it is a resource-file change,
  not a structural one.

## Pros and cons of the options

### Formal everywhere
- 👍 Smallest change — 16 new strings, not 41 existing ones.
- 👍 Keeps strings M1 already shipped and verified untouched.
- 👎 Locks the whole suite to the voice of its least characteristic screens.
- 👎 Wrong register for what the product actually is.

### Informal in German, `vous` in French
- 👍 Matches the product and the reader.
- 👍 Respects that the formal/informal line sits differently in each language.
- 👎 Rewrites 41 existing German strings.
- 👎 "One product, two registers" needs explaining — which is what this ADR is for.

### Informal everywhere, including French `tu`
- 👍 One rule, no per-language exception.
- 👎 `tu` from a service is off-key in Swiss French; consistency with German is not a reason to write
  bad French.

### Leave it, record as known issue
- 👍 Zero effort now.
- 👎 Ships a register that visibly flips inside one panel, and leaves users misgendered.

## Links

- Related ADRs: [ADR-0024](0024-ui-styling-and-theming.md) (how the suite looks — this is how it
  sounds), [ADR-0018](0018-circle-and-sharing-model.md)
- Requirements / use cases: [NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md),
  [FR-015](../requirements/functional/FR-015-delete-account.md)
- Features: [FEAT-003](../features/FEAT-003-m2-circles.md)
- Guidelines: [localization](../guidelines/localization-guidelines.md)
