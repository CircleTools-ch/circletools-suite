# Testers Community reports — Play closed test (Secret Santa)

Reports from the paid closed-testing service (backlog **PR-1**, Testers Community *Pro*, submitted
2026-10-29). Received **2026-11-10**, day 12 of the test, dashboard at 71 %. The PDFs are kept as
received; this page summarizes them and records what we do with each point (backlog **PR-17**).

| File | What it is |
|------|------------|
| [`2026-11-10-tester-feedback-report.pdf`](2026-11-10-tester-feedback-report.pdf) | Tester feedback: no crashes or bugs found, five enhancement suggestions, general tips |
| [`2026-11-10-aso-report.pdf`](2026-11-10-aso-report.pdf) | Automated ASO audit of the **English** Play listing: 61/100 (C), 4 failed checks |
| [`2026-11-10-production-access-questionnaire.pdf`](2026-11-10-production-access-questionnaire.pdf) | Suggested answers for Google's production-access form (asked after day 14) |

## Feedback report — findings and decisions

The testers found **no crashes, bugs or broken functions** on any device or SDK.

| # | Suggestion | Decision (proposed 2026-11-10, owner to confirm) |
|---|------------|------|
| F1 | ASO: longer description, more keywords, use-case examples | **Do** — see A1–A4 below. Listing text only, no build. |
| F2 | Screenshots show plain screens; add feature highlights and captions | **Do after production access** (PR-6 follow-up): captioned, framed screenshots for the first three slots, DE/EN/FR. No user testimonials — we have no real reviews to quote. |
| F3 | Password field has no show/hide toggle | **Already done** — PR-13, ships in the R4 + R5 build. |
| F4 | Add Google Sign-in / social logins | **Decline for 1.0.** The app needs no account at all, and invitees never sign in, so the "onboarding barrier" does not apply. Social login would need an ADR (ADR-0009 chose passkey + password), privacy policy and data-safety changes, and on iOS a third-party login requires an equivalent privacy-focused option such as *Sign in with Apple* as well (App Review guideline 4.8). Parked as an idea. |
| F5 | Back on the home screen exits without a confirmation | **Decline.** That is standard Android behaviour, and Android's predictive-back guidance advises against exit dialogs. Nothing is lost: all state is saved as it is made, and Back already closes dialogs and drawers and walks the in-app history first (`MainActivity.WebViewBackCallback`). |
| — | Onboarding tutorial, offline checks, more languages, performance monitoring, regular updates | Generic. First-run onboarding exists (2026-09-17 pass), offline is the default mode, DE/EN/FR is shipped, the release cadence is PR-12. No new work. |

## ASO report — findings and decisions

**Listing text reworked 2026-11-10** (owner go-ahead) in `../../listing-{en,de,fr}.md`: EN title
`Secret Santa – Gift Exchange`; short descriptions lead with *Secret Santa app* / *Wichtel-App* /
*Appli Père Noël secret*; full descriptions gained a key-features heading, a "Perfect for" use-case
block and a Q&A (EN 2,884, DE 3,100, FR 3,407 chars, all ≤ 4,000). Entered in Play Console the
same day (en-US, de-DE, fr-FR; the AI-asset label left as it was) and sent for review the same
day. It needs no build. Managed publishing is on, so after approval it still has to be published. The App Store descriptions in
`store/app-store/listing-*.md` carry the same text, for the next iOS version after 1.0.10.

Primary keyword chosen by the tool: *secret santa app* (alternatives: *gift exchange app*, *draw names
app*, *secret santa generator*). Only the English listing was audited; DE and FR already carry
composite titles (`Secret Santa – Wichteln`, `Secret Santa: Père Noël secret`). Its "localize your
listing" advisory is already met.

| # | Check (points) | Decision (proposed) |
|---|----------------|------|
| A1 | Keyword in the title (5) and title uses only 12 of 30 chars (2.5) | **Do:** EN title `Secret Santa – Gift Exchange` (28 chars), the same "brand, then local term" pattern ADR-0057 already uses for DE/FR. The tool's own example (`Secret Santa: Secret Santa App`) is keyword stuffing and is not used. |
| A2 | Keyword in the short description (4) | **Do:** start it with "Secret Santa app", still ≤ 80 chars. |
| A3 | Description not scannable (3) | **Partly disagree** — it already has `HOW IT WORKS` / `FEATURES` / `PRIVATE BY DESIGN` with bullets. Make the opening shorter and the headings stand out; mirror in DE/FR. |
| A4 | Description only 1,950 of ≥ 3,000 chars (2) | **Do, moderately:** add a "Perfect for" use-case block (family Christmas, office party, friends, school class, Schrottwichteln / white elephant) and a short FAQ. Aim for ~3,000 chars without padding; mirror in DE/FR. |
| — | Advisories: in-app rating prompt, conversion tracking, store-listing experiments, update every 6–8 weeks | Rating prompt: **idea, after launch** (it is a feature, out of scope during the test window). Conversion and A/B tests: **after production access**, when there is real traffic. Update cadence: already PR-12. |

## Production-access questionnaire — do not submit as written

The suggested answers state things that are **not true** for this app: that family and friends were
recruited (PR-1: paid service only), that feedback came "through surveys", and that we **added Google
Sign-in and an exit confirmation**. Google reviews this form, so we answer with what really happened.
Draft to adapt on day 14 (owner to edit; fill in the open points):

1. **Recruitment:** A paid closed-testing service (Testers Community) provided the testers, on real
   devices across Android versions. I also tested on my own devices (Pixel 9 Pro, Galaxy S24).
2. **How easy:** Easy.
3. **Engagement:** Testers installed and used the app over the 14 days and returned a written report.
   It found no crashes or functional bugs and made suggestions on store presence, login and navigation.
   _Add: any emails to `info@circletools.ch` and the opt-in / active-tester numbers from Play Console._
4. **Feedback summary:** Collected through the service's written report and the feedback address
   on the test track. Main points: a show-password toggle, a richer store description and screenshots,
   Google Sign-in, and an exit confirmation on Back.
5. **Audience:** Families, groups of friends, colleagues and school classes who organize a Secret
   Santa gift exchange (Christmas, office parties, family gatherings), primarily in Switzerland and
   the German-, French- and English-speaking markets.
6. **Value:** Draw names fairly with exclusions, give everyone a private link that reveals only their
   own match and that person's wishlist, without anyone needing to install the app or create an
   account. It works offline; an optional account syncs between devices.
7. **Expected installs (year one):** _Owner's call._ The service suggests 10k–100k; for a new,
   seasonal app without marketing, 1k–10k is the more defensible answer.
8. **Changes made:** Several updates during the test (R1–R5 in PR-12): a show-password toggle on all
   password fields, a consistent app name in every language, fixes to the date picker, forms and member
   list, the security page's device list, screen-reader language, French translations, and a reworked,
   keyword-optimized store listing. _List only what has really shipped by then._
9. **Ready for production:** No crashes or functional bugs in the closed test or in my own device
   passes, the fixes found during the test are released, and the service is live and monitored.
10. **What we did differently:** _First app on this account — say so, or leave out._
