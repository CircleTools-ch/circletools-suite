---
status: Accepted
date: 2026-09-17
deciders: [Adrian Moll]
tags: [email, branding, localization, ux]
---

# ADR-0064 — Branded HTML transactional email with per-app identity and source-app link origin

## Context and problem statement

The suite's transactional mails (verification, password reset, email change + notice, sign-up
attempt, circle invitation, member-link) are **plaintext**, built by string concatenation in
`MailTemplateRenderer`, and every one is branded **"CircleTools"** with a bare URL on its own line.
Two problems follow:

1. **They look unprofessional** — no header, no logo, no call-to-action button, a long raw link in
   the body. The product owner asked for branded HTML with a "Click here" button and the full link
   kept only as a fallback.
2. **They mis-brand the source app.** A person who signs up **in Secret-Santa** receives a
   *CircleTools* email and is sent to a *CircleTools*-looking page, even though the account is a
   suite-wide CircleTools account reached from a specific app. This is a recognition gap.

`MailTemplateRenderer` itself carried an explicit open question: do email templates follow ADR-0011
(RESX product strings) or get their own mechanism? Multi-line bodies with placeholders sit awkwardly
in RESX, which is why the copy has lived in code behind `IMailTemplateRenderer`. A redesign forces the
answer. This ADR settles the templating mechanism **and** the branding/link-origin question together.

## Decision drivers

- Professional, on-brand mail that reads correctly in the major clients (Gmail, Outlook, Apple Mail).
- **Recognition:** the mail must look like the app the user actually used, while staying honest that
  the account is a CircleTools account (cross-app SSO — [ADR-0039](0039-cross-app-single-sign-on.md)).
- Reuse the existing architecture: mail is a host/infrastructure concern behind the outbox; the domain
  must not learn about HTML (layering, golden rule 6).
- Localization DE/EN/FR must be preserved ([NFR-003](../requirements/non-functional/NFR-003-multilingual-de-en-fr.md)).
- Security controls must not regress: no link in the enumeration-sensitive notices
  ([NFR-005](../requirements/non-functional/NFR-005-account-enumeration-resistance.md)); the
  has-account invitation must not leak the join secret ([NFR-006](../requirements/non-functional/NFR-006-circle-authorization.md));
  user-supplied text (circle/group/member names) rendered into HTML must be encoded (no injection).
- No new NuGet packages beyond Central Package Management; keep the change reversible.

## Considered options

1. **Shared HTML layout + structured copy in Infrastructure (chosen).** Keep the per-language copy in
   `MailTemplateRenderer` (as today), but have each type return a small structured model; a new
   `MailLayout` renders both a branded HTML body (table-based, inline CSS) **and** a plaintext body.
   `OutboundMail` gains an HTML body; `SmtpMailSender` sends `multipart/alternative`. Branding is a
   config map keyed by a **source app** carried in the outbox payload.
2. **Move copy into RESX (ADR-0011 alignment) + a layout.** Consistent with product strings, but
   multi-line bodies with placeholders are awkward in RESX, and mail copy has different rules from UI
   copy (never re-translate user data, enumeration constraints).
3. **A templating engine (Razor/Scriban/Fluid).** More power than needed, adds a dependency, and
   pulls a rendering runtime into the mail path for what is a handful of small templates.
4. **Per-app colours = no (CircleTools-only).** Rejected: it is the recognition gap the owner asked
   to close.

## Decision outcome

**Chosen: Option 1**, with **per-app co-branding**:

- **Branding is co-branded, app-forward.** Each mail is rendered in the identity of the **source app**
  — its display name, brand colour and (optionally) logo — with a quiet "CircleTools account" line in
  the footer as the honest umbrella. This matches the existing per-app **brand-mark colours** (the app
  icons are already navy for CircleTools, red for Secret-Santa); it is the brand-mark layer, distinct
  from the one neutral in-app chrome that [ADR-0040](0040-ui-component-library-mudblazor.md) /
  [ADR-0054](0054-mobile-native-feel-strategy.md) govern, so it does not reopen those.
- **Links return to the source app's origin.** The account pages (`VerifyEmail`, `ResetPassword`, …)
  live in the shared `CircleTools.Ui.Account` block and are mounted into **each** app, so a link that
  points at `secretsanta.circletools.ch/verify-email` renders in the Secret-Santa shell automatically.
  The single `MailOptions.ClientBaseUrl` is therefore replaced by a **per-source-app origin** resolved
  from the payload (falling back to `ClientBaseUrl`).
- **The `sourceApp` travels in the outbox payload** (like `language` already does), captured at the
  point each mail is enqueued. Unknown/missing → the configured `DefaultApp` (CircleTools/navy), which
  is also the identity for any future app that has not defined its branding yet.
- **Sender display name is co-branded** (e.g. "Secret Santa · CircleTools") — the inbox sender line is
  the strongest anti-confusion lever. Carried per-message on `OutboundMail.FromName`.
- **The templating mechanism** is the shared `MailLayout` helper, **not** RESX and **not** an engine.
  This resolves the open point in `MailTemplateRenderer`.
- **The reusable-login (SSO) is not explained** in mail yet: with only one app live it would confuse
  more than help. Only the subtle "CircleTools account" footer seed is present; a proper explainer is
  deferred to the arrival of app #2 (tracked in the backlog).

## Consequences

- **Positive:** professional, recognisable mail; the sign-up → email → link → page journey stays in
  one app's identity; plaintext ships alongside HTML (better for text clients and spam scoring); one
  small, dependency-free layout; the long-open templating question is closed.
- **Negative:** mail copy now lives in a slightly larger structured form; a new per-app config block
  must be kept in step with the suite origins; HTML introduces an encoding obligation for user data.
- **Follow-ups:**
  - A new **account-deleted / farewell** mail type (there is none today) with its own requirement
    ([FR-047](../requirements/functional/FR-047-account-deletion-farewell-email.md)) — see
    [FEAT-018](../features/FEAT-018-branded-transactional-emails.md).
  - **White-on-transparent PNG logos per app** for the header image (`MailApp.LogoUrl`): email clients
    do not reliably render inline SVG, and the app-icon PNGs are same-colour as the header band. Until
    the asset exists the header is the brand-colour band + white wordmark; when `LogoUrl` is set the
    `<img>` appears with no code change. Tracked in the backlog.
  - The SSO-explainer copy when app #2 ships (backlog).
- **New risks:** an HTML-injection bug if user text is ever emitted unencoded (mitigated: all dynamic
  text is `HtmlEncode`d in `MailLayout`, covered by tests); a mis-configured `Apps` map falling back to
  CircleTools branding (safe by design).

## Pros and cons of the options

### Option 1 — shared layout + structured copy in Infrastructure
- 👍 No new dependency; stays behind the existing `IMailTemplateRenderer`/outbox seam; domain untouched.
- 👍 One place owns both HTML and plaintext, so they cannot drift.
- 👎 Copy is C#, not RESX — it diverges from the product-string convention (deliberately; see ADR-0011).

### Option 2 — RESX
- 👍 Consistent with product strings and the localization tooling.
- 👎 Multi-line placeholder bodies are awkward; mail copy has rules UI copy does not.

### Option 3 — templating engine
- 👍 Powerful for large template sets.
- 👎 A dependency and a runtime for a handful of small mails; overkill.

## Links

- Related ADRs: [ADR-0011](0011-localization-format-and-culture.md) (RESX product strings — mail opts out),
  [ADR-0037](0037-transactional-email-provider.md) (Brevo/SMTP provider), [ADR-0023](0023-local-dev-infrastructure-in-apphost.md)
  (mail dev sink / outbox), [ADR-0039](0039-cross-app-single-sign-on.md) (suite account),
  [ADR-0046](0046-suite-packaging-and-app-composition.md) (federated origins),
  [ADR-0040](0040-ui-component-library-mudblazor.md) / [ADR-0054](0054-mobile-native-feel-strategy.md)
  (neutral in-app chrome), [ADR-0059](0059-brand-mark-ring-and-central-glyph.md) (brand marks).
- arc42 chapters: 05 Building-block view (Infrastructure/Mail), 08 Cross-cutting (localization).
- Requirements / use cases: FR-007, FR-009, FR-013, FR-017, FR-028, FR-047 (new); UC-MAIN-007,
  UC-MAIN-010; NFR-003, NFR-005, NFR-006. Feature: FEAT-018.
