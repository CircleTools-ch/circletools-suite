---
date: 2026-09-02
facilitator: Adrian
participants: [Adrian]
topic: Secret-Santa competitor & app-store review research (DE/EN/FR)
status: Draft         # Draft | Concluded
---

# Brainstorming — Secret-Santa competitor & app-store review research (2026-09-02)

## Objective / framing question

**What do the existing Secret-Santa / Wichteln / Père-Noël-secret apps already do, what do their
users complain about, and what must our app do to be clearly better than them?**

A market scan of the Google Play + Apple App Store listings **and their 1–3★ reviews** across all
three product languages — English ("Secret Santa"), German ("Wichteln"), French ("Père Noël
secret") — feeding the mobile feature roadmap
([mobile brainstorm](2026-09-01-secret-santa-mobile.md),
[backlog](../roadmap/backlog.md)). It runs alongside the in-flight **QR-code** work (MNF-5) —
a first read on whether that direction is right.

### Method & confidence

Store listings, review pages, and comparison articles were read in DE/EN/FR. **Apple App Store**
data (ratings, feature copy, individual reviews) came through cleanly; **Google Play** listing
pages were repeatedly truncated to the fetch tooling, so **Play install counts and Play review text
are approximate or missing** and are flagged as such below. Ratings vary by storefront (DE/AT/CH,
FR/BE/CH) and season. Nothing here is invented — where review data was unavailable it is called out.
Marketing participant-count claims (e.g. Tirokdo "7–14M") are treated as unverified.

## Divergent — raw findings

### Apps surveyed (by market)

**English / international**

- **Elfster** — market leader (iOS ~4.9★/~331K; ~1.4M Play installs), but off-store reputation is
  far lower (Trustpilot/PissedConsumer ~3.1–3.8★). Wish lists, "mark as purchased", exclusions,
  multi-event. **Free, monetized via retailer affiliate shopping.**
- **DrawNames** — the privacy-friendly favourite (iOS ~4.9★/~199K; cited ~7.5M+ Play downloads).
  **Browser draw joinable by link, no email / no app required**, exclusions, budget, wish lists,
  recurring events, anonymous questions, group chat. Pitch: *"no ads, no account required."*
- **Giftster** — recurring-family option (iOS ~4.8★/~19K). Year-round lists, gift **claiming**,
  auto draw, reminders. Optional paid tier.
- **Sneaky Santa**, **GiftList**, **Wishlists**, **Joylord "Secret Santa – Gift Exchange"**,
  **Simple Secret Santa Generator** — smaller/newer; several data points are vendor-self-reported
  (low confidence).

**German / DACH ("Wichteln")**

- **drawnames** (iOS 4.7★/9,855) — same product, localized (drawnames.de).
- **Die Wichtel App** (iOS 4.5★/3,263) — device pass-around reveal, round history, email notify.
- **Wichteln Helfer App** (iOS 4.7★/2,508) — link/code sharing, chat w/ images, strong
  accessibility, "no email / ad-free / no account" pitch.
- **Wichtelmania** (iOS 4.8★/347), **Wichtel App – Wichtelmeister** (small; **QR-code sharing,
  offline, no tracking, free capped at 4 people**), **Wichtel App – Secret Santa** (ads + paid
  ad-removal €5.99).
- Web tools **Wichtel-O-Mat / Wichtlify / Wichtel.app** — browser, no install, no account;
  **Wichtlify models Schrottwichteln** (each person draws 2 recipients).

**French / francophone (FR / Suisse romande / BE)**

- **DrawNames** (App Store fr 4.8★/~8,400) with dedicated **.fr / .be** properties.
- **Secret Santa cadeau au hasard** (iOS 4.8★/~2,000) — offline live draw; **free tier capped ~10,
  ads, reveal shown too early**.
- **Père Noël Secret 22** (~13K ratings on main listing; few FR reviews) — group-code invites, PDF
  export; **email delivery unreliable**, free tier ~6.
- Web tools **perenoelsecret.fr / secretsanta.fr / Tirokdo** — browser, no account,
  **email-reveal-link**; Tirokdo has 3 secrecy modes + anonymous messaging, ad-supported.

### What reviewers complain about (verbatim gist, across all three markets)

- **Ads.** *"viel Werbung"*; mandatory ~30s interstitials to do basic actions; sponsored links.
- **Surprise paywalls & broken paid tiers.** The single loudest theme in DE **and** FR: free tiers
  silently cap participants (~4 / ~6 / ~10), discovered **mid-setup after inviting the group**;
  Joylord reveals its charge only past ~6 people; Wichtelmeister's paid *"unlimited"* failed;
  *"Even watching the ad, it's still paid!!!"*
- **Everyone must install the app / create an account.** Repeatedly disliked; the link/QR,
  no-account web tools win on exactly this.
- **Draw correctness bugs.** Non-participants included in the draw (DrawNames); **same pairing
  repeats year after year despite reshuffle** (Die Wichtel App, Secret Santa cadeau, "*c'est plus
  du hasard, c'est de la programmation*"); non-unique assignments (Wichtelmania).
- **Spoiled reveal.** Result shown too early / visible to the organizer; wish-list "chosen" marks
  visible to everyone; drawnames **removed** its giver-only visibility control — actively criticized.
- **Email delivery is fragile.** Invites/results not arriving, timeouts (Die Wichtel App, Père Noël
  Secret 22) — and email-only inviting is itself a limitation.
- **Email spam & no account deletion.** Elfster's signature grievance: *"cannot stop the emails,
  nor delete your account,"* invites to exchanges never joined.
- **Weak localization.** *"Alles auf englisch, was einfach nicht jedes Familienmitglied versteht."*
- **Wish-list friction.** Broken external-link import, wrong auto-fetched product images, no
  size/colour variants, only ~4 shops, sponsored results, data not saving.
- **Update regressions & peak-season instability** — apps break in December when usage spikes.
- **Privacy distrust** — aggressive tracking in minor free apps; affiliate monetization felt as
  pressure.

### What reviewers wish existed

- No account, no ads, no tracking; a genuinely usable free tier with **transparent upfront pricing**.
- **Link / QR / phone-number sharing** instead of fragile email; **no forced install** for guests.
- A draw that **provably respects exclusions and never repeats last year** — surfaced visibly so it
  is *trusted*.
- **Guaranteed-private reveal** the organizer can't see (the per-person link model).
- **Recurring yearly groups** that carry wish lists *and* exclusions forward (lists not transferring
  is a named complaint).
- **Both minimum and maximum budget**; **"do-not-gift" / dietary / lifestyle** lists.
- **Push notifications / reminders done right** — nudge people to fill their wish list.
- **Co-organizers**; **parent-managed child profiles**; **mailing-address** field for long-distance.
- **Richer wish lists** (variants, more retailers, reliable images) that still keep the surprise.
- **Anonymous two-way messaging** between matched giver ↔ giftee (most allow one-way questions only).
- **Reliable offline / in-person live draw** (valued for the physical family gathering).
- DACH-specific: **Schrottwichteln** (each person draws 2) — native apps largely ignore it.

## Clustering / themes

- **A · Trust & no-friction positioning (biggest wedge).** No account, no ads, no tracking, no
  forced install, offline, transparent/no pricing. Demanded in every market; the web tools win here.
- **B · Distribution that isn't email.** Link / QR / native share; email delivery is the most-broken
  channel. *(Directly validates the in-flight MNF-5 QR work + the built share sheet.)*
- **C · Draw correctness you can trust.** Honours exclusions, no reciprocal pairs, **no repeat of a
  prior year**, everyone gets a distinct valid partner — and shown clearly so users believe it.
- **D · Non-spoiling reveal.** Per-person secret the organizer never sees; giver-only wish-list
  visibility.
- **E · Localization & Swiss-first.** Proper DE/EN/FR incl. **de-CH / fr-CH**, region-vs-language
  formatting (ADR-0056), CHF budgets — an under-served niche; Suisse romande has *no* local player.
- **F · Recurring-family depth.** Yearly repeat carrying lists + exclusions forward; richer,
  surprise-preserving wish lists; budget min+max; do-not-gift lists.
- **G · Engagement extras.** Reminders/notifications, anonymous two-way messaging, add-to-calendar,
  co-organizers, mailing address, Schrottwichteln variant.

## Convergent — selection

Impact/effort/fit on 1–5 (5 = best). "Already ours" = the current app already satisfies this and the
finding is **confirmation**, not new work.

| Finding → direction | Impact | Effort (low=better) | Fit | Decision |
|---------------------|:------:|:-------------------:|:---:|----------|
| Themes A/C/D — no-account, offline, exclusion-respecting **non-spoiling per-person reveal** | 5 | — | 5 | **Already ours — confirmed.** This is exactly what reviews reward and incumbents get wrong; keep it front-and-centre in store copy |
| Theme B — **QR per reveal link** (MNF-5, in flight) | 4 | 4 | 5 | **Validated — continue.** Answers the "email fails / everyone must install" complaint directly |
| Theme B — share-all links at once (MNF-7) | 3 | 4 | 4 | Keep (already selected) |
| Theme E — **Swiss-first DE/EN/FR + de-CH/fr-CH, CHF, ADR-0056 region split** | 4 | — | 5 | **Already ours — sharpen as a differentiator** (localization quality is a *named* competitor failing; Suisse romande is unserved) |
| Theme C — draw never repeats prior year (draw history) | 4 | — | 5 | **Already ours — confirmed**, and worth surfacing visibly (a top distrust point elsewhere) |
| Theme G — add-to-calendar (MNF-6) | 3 | 4 | 4 | Keep (already selected) |
| Theme G — **reminders / local notifications** (MNF-8, parked) | 4 | 2 | 4 | **Raise priority** — reviews want "nudge me to fill my wish list"; still needs the scheduling/origin design |
| Theme F — **recurring event carries wish lists + exclusions forward** | 4 | 3 | 5 | **New candidate — strong.** Named complaint ("lists don't transfer"); fits our events model |
| Theme F — **budget min *and* max** | 3 | 5 | 4 | **New candidate — cheap.** Only max exists across competitors |
| Theme F — **"do-not-gift" / dietary / lifestyle** list per member | 3 | 3 | 4 | **New candidate.** Repeatedly wished-for, nobody does it well |
| Theme G — **Schrottwichteln** (each person draws 2) | 3 | 3 | 4 | **New candidate — DACH differentiator.** Native apps ignore it; fits Swiss-first |
| Theme G — anonymous two-way giver↔giftee messaging | 3 | 3 | 3 | Park — needs a channel/origin; most apps only do one-way |
| Theme F — richer wish lists (variant/retailer/image import) | 3 | 2 | 2 | Park/decline — pulls toward affiliate-shop integration we deliberately avoid |
| Theme G — co-organizers | 2 | 2 | 3 | Park — our organizer model is single-owner today |
| Theme G — mailing address for long-distance | 2 | 4 | 3 | Park — niche; revisit if requested |
| Anti-pattern: **ads / surprise paywall / forced account / email spam** | — | — | — | **Explicitly avoid.** These are the top irritants in every market — our positioning already forbids them; make "free, no ads, no account, no spam" an explicit product promise |

## Decisions & selected ideas

- **The research validates the current strategy.** The four things reviewers most reward —
  *no account / offline*, *link-or-QR distribution*, *a draw that respects exclusions and never
  repeats*, and *a reveal that doesn't spoil the surprise or spam anyone* — are **already the
  spine of our app**. The clearest action is to **keep and loudly state** this positioning
  (store copy, onboarding), not to chase competitor feature bloat.
- **Continue the in-flight QR work (MNF-5)** — the research independently confirms link/QR sharing
  is the appreciated fix for the category's most-broken channel (email).
- **Promote three new, well-evidenced, on-strategy candidates** to the backlog:
  1. **Recurring event carries wish lists + exclusions forward** (Theme F) — highest-value new one.
  2. **Budget min *and* max** (Theme F) — cheap, closes a universal gap.
  3. **"Do-not-gift" / dietary / lifestyle restrictions** per member (Theme F).
  and **raise the priority of reminders/notifications (MNF-8)** — reviews specifically want the
  wish-list nudge; the scheduling/origin design it's parked behind is now better motivated.
- **Consider Schrottwichteln** as a Swiss/DACH differentiator (each person draws 2 recipients) —
  a genuine gap in native apps; sits naturally with our Swiss-first stance.
- **Make the negatives an explicit promise:** free, **no ads, no account required, no email spam,
  no surprise paywall, no tracking** — the four loudest complaints in all three markets, which our
  architecture already lets us keep.

## Rejected / parked (for the record)

- **Retailer/affiliate wish-list shopping (Elfster/DrawNames-style)** — deliberately *not* pursued;
  it is the source of the "sponsored results / pressure / wrong images" complaints and conflicts
  with the no-ads, privacy-first positioning that the same reviews reward.
- **Co-organizers, mailing address, anonymous two-way messaging** — parked; either need a new
  origin/channel or a change to the single-owner model; revisit on demand.
- **Richer variant/retailer wish lists** — declined for now (pulls toward shop integration we avoid).

## Follow-up actions

- [x] Add backlog items: **recurring event carries wish lists + exclusions forward** (CR-1);
  **budget min+max** (CR-2); **do-not-gift/dietary list** (CR-3); **Schrottwichteln variant** (CR-4).
  — Adrian, 2026-09-02. **CR-1 taken through the process** →
  [UC-SS-014](../use-cases/secret-santa/UC-SS-014-carry-forward-new-occasion.md) /
  [FR-035](../requirements/functional/FR-035-carry-forward-new-occasion.md) /
  [FEAT-010](../features/FEAT-010-carry-forward-new-occasion.md) /
  [TP-010](../features/TP-010-carry-forward-new-occasion.md); reading the model sharpened it (exclusions already
  carry; added the draw-baseline anti-repeat piece).
- [ ] Re-prioritise **MNF-8 (reminders/notifications)** with the wish-list-nudge motivation from
  this scan; still gated on the scheduling/origin design. — Adrian
- [ ] Fold a **"free · no ads · no account · no spam"** promise into store copy / onboarding when
  the app nears submission. — Adrian
- [ ] Confirm the QR-code work (MNF-5) lands the "join without email/install" benefit this research
  says the market wants. — (in-flight session)
